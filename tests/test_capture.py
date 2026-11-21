import io
from pathlib import Path
from tempfile import TemporaryDirectory
import unittest
from unittest.mock import patch

from gameperf import (
    analyze,
    compare,
    format_comparison,
)
from gameperf.__main__ import main


EXAMPLES_DIR = Path(__file__).resolve().parents[1] / "examples"
EXAMPLE = EXAMPLES_DIR / "synthetic.csv"
OPTIMIZED = EXAMPLES_DIR / "synthetic_optimized.csv"
INCOMPATIBLE_APP = EXAMPLES_DIR / "synthetic_incompatible_app.csv"


class CaptureTests(unittest.TestCase):
    def test_known_sequence(self):
        report = analyze(EXAMPLE)
        self.assertEqual(report["application"], "demo.exe")
        self.assertEqual(report["process_id"], "100")
        self.assertEqual(report["swap_chain_address"], "0x01")
        self.assertEqual(report["metric"], "MsBetweenPresents")
        self.assertEqual(report["intervals"], 10)
        self.assertEqual(report["p50_ms"], 4)
        self.assertEqual(report["p99_ms"], 20)
        self.assertEqual(report["one_percent_low_fps"], 50)
        self.assertEqual(report["duration_ms"], 60)
        self.assertEqual(report["above_7.14_ms_per_min"], 2000)
        self.assertEqual(report["above_16.67_ms_per_min"], 1000)

    def test_rejected_inputs(self):
        example = EXAMPLE.read_text(encoding="utf-8")
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

    def test_compare_improvement_and_pid_change(self):
        result = compare(EXAMPLE, OPTIMIZED)
        self.assertEqual(result["application"], "demo.exe")
        self.assertEqual(result["metric"], "MsBetweenPresents")
        self.assertEqual(result["baseline"]["process_id"], "100")
        self.assertEqual(result["comparison"]["process_id"], "200")
        self.assertEqual(result["baseline"]["swap_chain_address"], "0x01")
        self.assertEqual(result["comparison"]["swap_chain_address"], "0x02")

        metrics = result["metrics"]
        # P99 melhora: de 20.0 para 15.0 ms
        p99 = metrics["p99_ms"]
        self.assertEqual(p99["baseline"], 20.0)
        self.assertEqual(p99["comparison"], 15.0)
        self.assertEqual(p99["diff"], -5.0)
        self.assertEqual(p99["direction"], "melhora")

        # 1% low melhora: de 50.0 para ~66.67 FPS
        one_low = metrics["one_percent_low_fps"]
        self.assertEqual(one_low["baseline"], 50.0)
        self.assertAlmostEqual(one_low["comparison"], 66.66666666666667)
        self.assertGreater(one_low["diff"], 0)
        self.assertEqual(one_low["direction"], "melhora")

        # Intervalos > 7.14 ms / min melhora: de 2000 para ~1132
        above_7 = metrics["above_7.14_ms_per_min"]
        self.assertEqual(above_7["baseline"], 2000.0)
        self.assertLess(above_7["comparison"], 2000.0)
        self.assertLess(above_7["diff"], 0)
        self.assertEqual(above_7["direction"], "melhora")

        # Intervalos > 16.67 ms / min melhora: de 1000 para 0
        above_16 = metrics["above_16.67_ms_per_min"]
        self.assertEqual(above_16["baseline"], 1000.0)
        self.assertEqual(above_16["comparison"], 0.0)
        self.assertEqual(above_16["diff"], -1000.0)
        self.assertEqual(above_16["direction"], "melhora")

    def test_compare_incompatible_applications(self):
        with self.assertRaises(ValueError) as ctx:
            compare(EXAMPLE, INCOMPATIBLE_APP)
        self.assertIn("aplicações diferentes", str(ctx.exception))
        self.assertIn("demo.exe", str(ctx.exception))
        self.assertIn("other_game.exe", str(ctx.exception))

    def test_compare_incompatible_metrics(self):
        report_a = analyze(EXAMPLE)
        report_b = analyze(EXAMPLE)
        report_b["metric"] = "CPUFrameTime"
        with self.assertRaises(ValueError) as ctx:
            compare(report_a, report_b)
        self.assertIn("métricas diferentes", str(ctx.exception))

    def test_compare_missing_identifiers(self):
        report_a = analyze(EXAMPLE)
        report_b = analyze(EXAMPLE)
        report_b["application"] = ""
        with self.assertRaises(ValueError) as ctx:
            compare(report_a, report_b)
        self.assertIn("Aplicação não informada", str(ctx.exception))

    def test_format_comparison_content(self):
        result = compare(EXAMPLE, OPTIMIZED)
        text = format_comparison(result)
        self.assertIn("demo.exe", text)
        self.assertIn("MsBetweenPresents", text)
        self.assertIn("PresentMon (v1 metrics)", text)
        self.assertIn("Intervalos entre chamadas Present", text)
        self.assertIn("P99", text)
        self.assertIn("1% low", text)
        self.assertIn("Intervalos > 7,14 ms / min", text)
        self.assertIn("Intervalos > 16,67 ms / min", text)
        self.assertIn("Linha de base (A)", text)
        self.assertIn("Ajuste (B)", text)
        self.assertIn("Diferença", text)
        self.assertIn("Direção", text)
        self.assertIn("melhora", text)

    def test_cli_single_capture(self):
        f = io.StringIO()
        with patch("sys.stdout", f):
            main([str(EXAMPLE)])
        output = f.getvalue()
        self.assertIn('"application": "demo.exe"', output)
        self.assertIn('"metric": "MsBetweenPresents"', output)
        self.assertIn('"p99_ms": 20.0', output)

    def test_cli_ab_compare_table(self):
        f = io.StringIO()
        with patch("sys.stdout", f):
            main([str(EXAMPLE), str(OPTIMIZED)])
        output = f.getvalue()
        self.assertIn("Comparação A/B de Desempenho", output)
        self.assertIn("demo.exe", output)
        self.assertIn("P99", output)
        self.assertIn("1% low", output)
        self.assertIn("melhora", output)

    def test_cli_ab_compare_json(self):
        f = io.StringIO()
        with patch("sys.stdout", f):
            main([str(EXAMPLE), str(OPTIMIZED), "--json"])
        output = f.getvalue()
        self.assertIn('"application": "demo.exe"', output)
        self.assertIn('"metrics"', output)
        self.assertIn('"direction": "melhora"', output)

    def test_cli_incompatible_exit(self):
        with self.assertRaises(SystemExit) as ctx:
            main([str(EXAMPLE), str(INCOMPATIBLE_APP)])
        self.assertEqual(ctx.exception.code, 2)


if __name__ == "__main__":
    unittest.main()
