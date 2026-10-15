"""Reject stale or altered native captures before updating public evidence."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

from PIL import Image

spec = importlib.util.spec_from_file_location("showcase", Path(__file__).parents[1] / "scripts/compose-showcase.py")
showcase = importlib.util.module_from_spec(spec)
spec.loader.exec_module(showcase)


class CaptureChecks(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.gui, self.core = self.root / "gui.dll", self.root / "core.dll"
        self.gui.write_bytes(b"synthetic GUI assembly")
        self.core.write_bytes(b"synthetic core assembly")
        self.receipt = {
            "guiBuild": "1.4.0+" + "a" * 40, "coreBuild": "1.4.0+" + "a" * 40,
            "guiAssemblySha256": showcase.sha256(self.gui), "coreAssemblySha256": showcase.sha256(self.core),
            "captures": {},
        }
        for filename in showcase.SOURCES.values():
            self.capture(filename, (1200, 750), 96)

    def capture(self, filename, size, dpi):
        path = self.root / filename
        Image.new("RGB", size, "#191d23").save(path)
        self.receipt["captures"][filename] = {
            "width": size[0], "height": size[1], "deviceDpi": dpi,
            "clientSize": [size[0] - 16, size[1] - 39], "sha256": showcase.sha256(path),
        }

    def validate(self):
        (self.root / "capture.json").write_text(json.dumps(self.receipt), encoding="utf-8")
        return showcase.validate_captures(self.root, "a" * 40, "1.4.0", self.gui, self.core)

    def test_native_dpi_and_uniform_aspect_ratio(self):
        for dpi, size in ((96, (1200, 750)), (120, (1504, 940)), (144, (1800, 1125))):
            with self.subTest(dpi=dpi):
                self.capture("portfolio.png", size, dpi)
                self.assertEqual(self.validate()["captures"]["portfolio.png"]["deviceDpi"], dpi)

    def test_stale_source(self):
        self.receipt["guiBuild"] = "1.4.0+" + "b" * 40
        with self.assertRaisesRegex(ValueError, "Stale gui"):
            self.validate()

    def test_changed_assembly(self):
        self.core.write_bytes(b"different compiled source")
        with self.assertRaisesRegex(ValueError, "Stale core"):
            self.validate()

    def test_changed_pixels(self):
        Image.new("RGB", (1200, 750), "white").save(self.root / "compact.png")
        with self.assertRaisesRegex(ValueError, "does not match compact"):
            self.validate()

    def test_false_dimensions(self):
        self.receipt["captures"]["stacked.png"]["height"] = 480
        with self.assertRaisesRegex(ValueError, "does not match stacked"):
            self.validate()

    def test_nonuniform_or_upscaled_portfolio(self):
        for size in ((1500, 940), (800, 500)):
            with self.subTest(size=size):
                self.capture("portfolio.png", size, 120)
                with self.assertRaisesRegex(ValueError, "native 8:5"):
                    self.validate()


if __name__ == "__main__":
    unittest.main()
