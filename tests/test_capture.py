from pathlib import Path
from tempfile import TemporaryDirectory
import unittest

from gameperf.__main__ import analyze


EXAMPLE = Path(__file__).resolve().parents[1] / "examples" / "synthetic.csv"


class CaptureTests(unittest.TestCase):
    def test_known_sequence(self):
        report = analyze(EXAMPLE)
        self.assertEqual(report["intervals"], 10)
        self.assertEqual(report["p50_ms"], 4)
        self.assertEqual(report["p99_ms"], 20)
        self.assertEqual(report["one_percent_low_fps"], 50)
        self.assertEqual(report["duration_ms"], 60)
        self.assertEqual(report["above_7.14_ms_per_min"], 2000)
        self.assertEqual(report["above_16.67_ms_per_min"], 1000)

    def test_rejected_inputs(self):
        example = EXAMPLE.read_text()
        variants = {
            "mixed": example + "demo.exe,100,0x02,4\n",
            "missing_column": example.replace("MsBetweenPresents", "CPUFrameTime"),
            "nonfinite": example + "demo.exe,100,0x01,nan\n",
            "empty": example.splitlines()[0] + "\n",
            "late_zero": example + "demo.exe,100,0x01,0\n",
        }
        with TemporaryDirectory() as temp:
            path = Path(temp) / "sample.csv"
            for name, content in variants.items():
                with self.subTest(name=name):
                    path.write_text(content, encoding="utf-8")
                    with self.assertRaises(ValueError):
                        analyze(path)
