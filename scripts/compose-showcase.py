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


def export(captures, output):
    dirty = subprocess.check_output(["git", "status", "--porcelain", "--", "src", "test", "scripts/capture-showcase.ps1", "scripts/compose-showcase.py"], text=True)
    if dirty:
        raise ValueError("Implementation and capture tools must be committed before exporting public evidence.")
    output.mkdir(parents=True, exist_ok=True)
    sources = {
        "screenshot.png": "desk-2-1000-690.png",
        "portfolio-1200x750.png": "desk-2-1184-711.png",
        "screenshot-compact.png": "desk-2-604-441.png",
        "screenshot-stacked.png": "desk-stacked.png",
    }
    for destination, source in sources.items():
        shutil.copyfile(captures / source, output / destination)
    with Image.open(output / "portfolio-1200x750.png") as portfolio:
        if portfolio.size != (1200, 750):
            raise ValueError("Expected the 96-DPI native 1200x750 fixture. Do not stretch a different-size capture.")
        for width, height in ((480, 300), (320, 200)):
            portfolio.resize((width, height), Image.Resampling.LANCZOS).save(output / f"portfolio-{width}x{height}.png")
    names = list(sources) + ["portfolio-480x300.png", "portfolio-320x200.png"]
    receipt = {
        "sourceVersion": subprocess.check_output(["git", "rev-parse", "HEAD"], text=True).strip(),
        "sourceDirty": False,
        "build": "Release, development source; not a released-version preview",
        "assemblyVersion": ET.parse("Directory.Build.props").findtext(".//Version"),
        "guiAssemblySha256": sha256(Path("src/LumenDeck/bin/Release/net10.0-windows/LumenDeck.dll")),
        "coreAssemblySha256": sha256(Path("src/LumenDeck.Core/bin/Release/net10.0-windows/LumenDeck.Core.dll")),
        "dotnetSdk": subprocess.check_output(["dotnet", "--version"], text=True).strip(),
        "python": platform.python_version(),
        "pillow": pillow_version,
        "windowsVersion": platform.version(),
        "deviceDpi": 96,
        "scaling": "100%; original native pixel sizes, not upscaled",
        "fixture": "DeskRemoteChecks.DeskDisplays, synthetic stable identities, private test desktop and temporary settings",
        "displays": [
            {"name": "Studio 27", "position": "left, primary", "orientation": "landscape", "brightness": 62, "contrast": 50},
            {"name": "Reading 24", "position": "right", "orientation": "portrait", "brightness": 45, "contrast": "unsupported"},
        ],
        "warmth": "neutral gamma tint target; not a measured colour preview",
        "estimates": "Generic profiles; 203 and 160 estimated nits, not measured light",
        "method": "Actual MainForm.DrawToBitmap, complete native window bounds on a private desktop; DWM composition is not captured",
        "composition": "None. Portfolio master is the complete native 1200x750 window; no crop, repaint, stretching or invented controls.",
        "nativeClientSizes": {"documentation": [1000, 690], "portfolio": [1184, 711], "compact": [604, 441], "stacked": [1000, 690]},
        "stackedFixture": "Two landscape fixtures share a left coordinate; Reading 24 is below Studio 27 and selected.",
        "derivatives": "Uniform Pillow LANCZOS downsampling from the 1200x750 master to 480x300 and 320x200",
        "artifacts": {},
    }
    for name in names:
        with Image.open(output / name) as image:
            receipt["artifacts"][name] = {"width": image.width, "height": image.height, "sha256": sha256(output / name)}
    (output / "screenshot.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print("Exported six native/derived images and their source-bound receipt.")


if __name__ == "__main__":
    export(Path(sys.argv[1]), Path(sys.argv[2]))
