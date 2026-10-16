"""Export unretouched native fixture renders and exact-size portfolio derivatives."""
from pathlib import Path
import hashlib
import json
import platform
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET

from PIL import Image, __version__ as pillow_version


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


SOURCES = {
    "screenshot.png": "documentation.png",
    "portfolio-native.png": "portfolio.png",
    "screenshot-compact.png": "compact.png",
    "screenshot-stacked.png": "stacked.png",
    **{f"theme-{theme}.png": f"theme-{theme}.png" for theme in ("paper", "ocean", "violet")},
}


def validate_captures(captures, source_version, version, gui, core):
    receipt = json.loads((captures / "capture.json").read_text(encoding="utf-8-sig"))
    expected_build = f"{version}+{source_version}"
    for key, assembly in (("gui", gui), ("core", core)):
        if receipt[f"{key}Build"] != expected_build or receipt[f"{key}AssemblySha256"] != sha256(assembly):
            raise ValueError(f"Stale {key} capture: source/build hashes must match the current committed build.")
    for filename in SOURCES.values():
        recorded = receipt["captures"][filename]
        path = captures / filename
        with Image.open(path) as image:
            if sha256(path) != recorded["sha256"] or image.size != (recorded["width"], recorded["height"]):
                raise ValueError(f"Capture receipt does not match {filename}.")
            if recorded["deviceDpi"] < 96:
                raise ValueError("Expected native DPI of at least 96.")
            if filename == "portfolio.png" and (image.width < 1200 or image.width * 5 != image.height * 8):
                raise ValueError("Portfolio must be a native 8:5 window at least 1200x750; never stretch a capture.")
    return receipt


def export(captures, output):
    dirty = subprocess.check_output(["git", "status", "--porcelain", "--", "src", "test", "Directory.Build.props", "scripts/capture-showcase.ps1", "scripts/compose-showcase.py"], text=True)
    if dirty:
        raise ValueError("Implementation and capture tools must be committed before exporting public evidence.")
    source_version = subprocess.check_output(["git", "rev-parse", "HEAD"], text=True).strip()
    version = ET.parse("Directory.Build.props").findtext(".//Version")
    captured = validate_captures(captures, source_version, version,
        Path("src/LumenDeck/bin/Release/net10.0-windows/LumenDeck.dll"),
        Path("src/LumenDeck.Core/bin/Release/net10.0-windows/LumenDeck.Core.dll"))
    # Validate the entire run before replacing any public assets.
    output.mkdir(parents=True, exist_ok=True)
    for destination, source in SOURCES.items():
        shutil.copyfile(captures / source, output / destination)
    with Image.open(output / "portfolio-native.png") as portfolio:
        for width, height in ((1200, 750), (480, 300), (320, 200)):
            portfolio.resize((width, height), Image.Resampling.LANCZOS).save(output / f"portfolio-{width}x{height}.png")
    names = list(SOURCES) + ["portfolio-1200x750.png", "portfolio-480x300.png", "portfolio-320x200.png"]
    receipt = {
        "sourceVersion": source_version,
        "sourceDirty": False,
        "build": "Release, development source; not a released-version preview",
        "assemblyVersion": version,
        "guiAssemblySha256": captured["guiAssemblySha256"],
        "coreAssemblySha256": captured["coreAssemblySha256"],
        "dotnetSdk": subprocess.check_output(["dotnet", "--version"], text=True).strip(),
        "python": platform.python_version(),
        "pillow": pillow_version,
        "windowsVersion": platform.version(),
        "nativeCaptures": captured["captures"],
        "scaling": "Actual per-capture native DPI and pixel dimensions are recorded below; no upscaling",
        "fixture": "DeskRemoteChecks.DeskDisplays, synthetic stable identities, private test desktop and temporary settings",
        "displays": [
            {"name": "Studio 27", "position": "left, primary", "orientation": "landscape", "brightness": 62, "contrast": 50},
            {"name": "Reading 24", "position": "right", "orientation": "portrait", "brightness": 45, "contrast": "unsupported"},
        ],
        "warmth": "neutral gamma tint target; not a measured colour preview",
        "estimates": "Generic profiles; 203 and 160 estimated nits, not measured light",
        "method": "Actual MainForm.DrawToBitmap, complete native window bounds on a private desktop; DWM composition is not captured",
        "composition": "None. Portfolio native master is the complete window at its recorded DPI; no crop, repaint, stretching or invented controls.",
        "stackedFixture": "Two landscape fixtures share a left coordinate; Reading 24 is below Studio 27 and selected.",
        "derivatives": "Uniform Pillow LANCZOS downsampling from portfolio-native.png to 1200x750, 480x300 and 320x200",
        "artifacts": {},
    }
    for name in names:
        with Image.open(output / name) as image:
            receipt["artifacts"][name] = {"width": image.width, "height": image.height, "sha256": sha256(output / name)}
    (output / "screenshot.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print("Exported ten native/derived images and their source-bound receipt.")


if __name__ == "__main__":
    export(Path(sys.argv[1]), Path(sys.argv[2]))
