"""Build editorial artwork around unchanged, receipt-verified UI references.

Requires Pillow and Playwright with Chromium. No network, desktop capture or
monitor access is used. SVG files remain the editable composition sources.
"""
from pathlib import Path
import base64
import hashlib
import html
import io
import json
import math
import os
from importlib.metadata import version as package_version
import xml.etree.ElementTree as ET

from PIL import Image, ImageChops, __version__ as pillow_version
from playwright.sync_api import sync_playwright


ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "docs/assets/promotion"
ET.register_namespace("", "http://www.w3.org/2000/svg")


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def data_uri(path):
    return "data:image/png;base64," + base64.b64encode(path.read_bytes()).decode("ascii")


def text(x, y, value, size=24, fill="#e8e5dd", family="Segoe UI", extra=""):
    return f'<text x="{x}" y="{y}" font-family="{family}" font-size="{size}" fill="{fill}" {extra}>{html.escape(value)}</text>'


def image(path, x, y, width):
    with Image.open(path) as source:
        height = width * source.height / source.width
    return f'<image href="{data_uri(path)}" x="{x}" y="{y}" width="{width}" height="{height}"/>', height


def poster(width, height, social=False):
    """The composition uses only uniform scaling of the entire native client."""
    scale = width / 1600
    # Social gets its own composition; it is never a crop of the README poster.
    canvas_height = height / scale
    shot_width = 780 if not social else 620
    shot_x = 1530 - 12 - shot_width
    shot_y = 139 if not social else 130
    shot, shot_height = image(ROOT / "docs/assets/material-smoke.png", shot_x, shot_y, shot_width)
    pieces = [
        '<rect width="1600" height="1250" fill="#171b1e"/>',
        '<path d="M70 108H1530" stroke="#55534b" stroke-width="1"/>',
        text(70, 77, "A DESK REMOTE", 17, extra='letter-spacing="3"'),
        text(1530, 77, "WINDOWS DISPLAY CONTROL", 15, "#bfb9ab", extra='letter-spacing="3" text-anchor="end"'),
        '<path d="M70 197H127" stroke="#efba69" stroke-width="3"/>',
        text(70, 293 if not social else 283, "LumenDeck", 106, "#f5eee0", "Georgia"),
        text(70, 390 if not social else 380, "Set the light.", 60, "#efba69", "Georgia", 'font-style="italic"'),
        text(70, 460 if not social else 450, "Keep your rhythm.", 60, "#efba69", "Georgia", 'font-style="italic"'),
        text(73, 550 if not social else 540, "Brightness, contrast and warmth", 27, "#d7d2c8"),
        text(73, 590 if not social else 580, "for supported Windows displays.", 27, "#d7d2c8"),
        text(73, 714, "YOUR DISPLAYS. YOUR SETTINGS.", 14, "#bfb9ab", extra='letter-spacing="2"'),
        text(73, 758, "Tune a screen. Save a mode.", 25),
        text(73, 796, "Return to it when you need it.", 25),
        ''.join(f'<path d="M{x + dx * 20} {y}H{x}V{y + dy * 20}" fill="none" stroke="#8c8576"/>' for x, y, dx, dy in ((shot_x - 12, shot_y - 12, 1, 1), (shot_x + shot_width + 12, shot_y - 12, -1, 1), (shot_x - 12, shot_y + shot_height + 12, 1, -1), (shot_x + shot_width + 12, shot_y + shot_height + 12, -1, -1))),
        shot,
        f'<path d="M70 {canvas_height - 72}H1530" stroke="#55534b"/>',
        text(70, canvas_height - 34, "OPEN SOURCE  /  MIT  /  GUI + CLI", 15, "#c8c2b6", extra='letter-spacing="1.8"'),
        text(1530, canvas_height - 34, "Development UI · sample displays · opaque reference", 16, "#c8c2b6", extra='text-anchor="end"'),
    ]
    if social:
        # The tall poster's supporting paragraph would compete with the footer.
        pieces = [p for p in pieces if not any(f'y="{y}"' in p for y in (714, 758, 796))]
    return svg(width, height, f'<g transform="scale({scale})">' + "".join(pieces) + "</g>")


def portfolio():
    shot, _ = image(ROOT / "docs/assets/material-smoke.png", 458, 64, 678)
    pieces = ['<rect width="1200" height="750" fill="#171b1e"/>',
              text(60, 100, "WINDOWS 10 / 11", 17, "#bfb9ab", extra='letter-spacing="2"'),
              text(60, 222, "LumenDeck", 68, "#f5eee0", "Georgia"),
              '<path d="M60 265H116" stroke="#efba69" stroke-width="3"/>',
              text(60, 337, "Brightness", 28), text(60, 383, "Contrast", 28), text(60, 429, "Warmth", 28),
              text(60, 638, "DEVELOPMENT UI", 15, "#c8c2b6", extra='letter-spacing="1"'),
              text(60, 667, "SAMPLE DISPLAYS", 15, "#c8c2b6", extra='letter-spacing="1"'),
              text(60, 696, "OPAQUE REFERENCE", 15, "#c8c2b6", extra='letter-spacing="1"'), shot]
    return svg(1200, 750, "".join(pieces))


def finishes():
    pieces = ['<rect width="1600" height="920" fill="#eee8dc"/>',
              text(64, 70, "LumenDeck / APPEARANCE", 17, "#5e574b", extra='letter-spacing="2"'),
              text(64, 154, "Three finishes. One desk.", 64, "#282b2d", "Georgia")]
    for i, (key, label, note) in enumerate((('smoke', 'Smoke', 'Dark and quiet'), ('frost', 'Frost', 'Light and clear'), ('ceramic', 'Ceramic', 'Warm and opaque'))):
        x = 64 + i * 508
        pieces.extend([text(x, 222, label, 27, "#282b2d"), text(x, 252, note, 19, "#615b52")])
        shot, _ = image(ROOT / f"docs/assets/material-{key}.png", x, 280, 456)
        pieces.append(shot)
    pieces.extend([text(64, 850, "Same controls. Choose the surface that suits your desk.", 23, "#383934"),
                   text(64, 888, "Opaque development references with sample displays; native backdrop and window frame are not captured.", 17, "#615b52")])
    return svg(1600, 920, "".join(pieces))


def svg(width, height, body):
    return f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}" role="img"><title>LumenDeck — Windows display control</title>{body}</svg>\n'


def main():
    fonts = Path(os.environ.get("WINDIR", "C:/Windows")) / "Fonts"
    font_hashes = {name: digest(fonts / name) for name in ("georgia.ttf", "georgiai.ttf", "segoeui.ttf")}
    receipt_path = ROOT / "docs/assets/material-reference.json"
    receipt = json.loads(receipt_path.read_text(encoding="utf-8-sig"))
    for item in receipt["images"]:
        if digest(ROOT / "docs/assets" / item["file"]) != item["sha256"]:
            raise ValueError(f"Reference hash mismatch: {item['file']}")
    OUT.mkdir(parents=True, exist_ok=True)
    compositions = {"readme-1600x1000": poster(1600, 1000), "social-1280x640": poster(1280, 640, social=True), "portfolio-1200x750": portfolio(), "finishes-1600x920": finishes()}
    artifacts = {}
    plate_deltas = {}
    with sync_playwright() as play:
        browser = play.chromium.launch()
        chromium_version = browser.version
        page = browser.new_page(device_scale_factor=1)
        page.route("**/*", lambda route: route.abort() if route.request.url.startswith(("https:", "http:")) else route.continue_())
        for name, source in compositions.items():
            tree = ET.fromstring(source)
            page.set_viewport_size({"width": int(tree.attrib["width"]), "height": int(tree.attrib["height"])})
            vector = OUT / f"{name}.svg"
            vector.write_text(source, encoding="utf-8")
            page.set_content('<html><head><style>html,body{margin:0;background:#171b1e}svg{display:block}</style></head><body>' + source + '</body></html>')
            page.evaluate("document.fonts.ready")
            # Check the actual rendered font metrics, not estimates from source.
            conflicts = page.evaluate('''() => {
              const box = e => e.getBoundingClientRect();
              const images = [...document.querySelectorAll('svg image')].map(box);
              const canvas = box(document.querySelector('svg'));
              return [...document.querySelectorAll('svg text')].filter(e => {
                const t = box(e);
                return t.left < 0 || t.top < 0 || t.right > canvas.right || t.bottom > canvas.bottom ||
                  images.some(i => t.left < i.right && t.right > i.left && t.top < i.bottom && t.bottom > i.top);
              }).map(e => e.textContent);
            }''')
            if conflicts:
                raise ValueError(f"Artwork text clipped or overlaps UI: {conflicts}")
            png = OUT / f"{name}.png"
            page.locator("svg").screenshot(path=str(png), animations="disabled")
            placements = page.locator("svg image").evaluate_all('elements => elements.map(e => {const b=e.getBoundingClientRect(); return [b.x,b.y,b.width,b.height]})')
            image_layers = list(tree.iter("{http://www.w3.org/2000/svg}image"))
            # Independently render just each source image at the exact same SVG
            # transform. Compare its interior with the complete finished poster.
            # Typography, masks or effects painted over the plate would differ.
            for layer, bounds in zip(image_layers, placements, strict=True):
                body = ET.tostring(layer, encoding="unicode")
                if name.startswith(("readme", "social")):
                    body = f'<g transform="scale({float(tree.attrib["width"]) / 1600})">{body}</g>'
                reference_only = svg(tree.attrib["width"], tree.attrib["height"], body)
                page.set_content('<html><head><style>html,body{margin:0}svg{display:block}</style></head><body>' + reference_only + '</body></html>')
                expected_bytes = page.locator("svg").screenshot(animations="disabled")
                x, y, w, h = bounds
                crop = (math.ceil(x), math.ceil(y), math.floor(x + w), math.floor(y + h))
                with Image.open(png) as actual, Image.open(io.BytesIO(expected_bytes)) as expected:
                    difference = ImageChops.difference(actual.convert("RGB").crop(crop), expected.convert("RGB").crop(crop))
                    maximum_delta = max(high for _, high in difference.getextrema())
                    plate_deltas.setdefault(name, []).append(maximum_delta)
                    # Chromium's independent raster passes can round an encoded
                    # channel by one. Larger differences indicate altered art.
                    if maximum_delta > 1:
                        debug = ROOT / "artifacts/promotion"
                        debug.mkdir(parents=True, exist_ok=True)
                        expected.save(debug / "expected-plate.png")
                        print("Plate difference bounds:", difference.getbbox(), "crop:", crop)
                        raise ValueError(f"UI pixels altered by composition: {name}")
            artifacts[vector.name] = {"sha256": digest(vector)}
        browser.close()
    with Image.open(OUT / "portfolio-1200x750.png") as master:
        for width, height in ((480, 300), (320, 200)):
            master.resize((width, height), Image.Resampling.LANCZOS).save(OUT / f"portfolio-{width}x{height}.png")
        master.save(OUT / "portfolio-1200x750.webp", lossless=True, method=6)
    for path in sorted(OUT.iterdir()):
        if path.suffix in (".png", ".webp"):
            with Image.open(path) as raster:
                artifacts[path.name] = {"width": raster.width, "height": raster.height, "sha256": digest(path)}
    manifest = {"kind": "Editorial compositions, not native screenshots", "generator": "scripts/build-promotion.py", "referenceSourceCommit": receipt["sourceCommit"], "referenceReceiptSha256": digest(receipt_path), "composition": "Unchanged complete opaque WPF client images, uniformly downscaled, surrounded by authored typography. No crop, perspective, invented controls or compositor effects.", "fonts": ["Segoe UI", "Georgia"], "fontSha256": font_hashes, "browser": f"Playwright Chromium {chromium_version}; local system fonts; no network", "playwright": package_version("playwright"), "pillow": pillow_version, "checks": ["Reference SHA-256 verified", "Rendered text fits canvas and never overlaps UI", "Each master UI plate compared with an independent same-scale source render; at most one encoded-channel rounding unit allowed"], "plateMaximumChannelDeltas": plate_deltas, "artifacts": artifacts}
    (OUT / "receipt.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"Built {len(artifacts)} artwork sources and derivatives; reference hashes verified.")


if __name__ == "__main__":
    main()
