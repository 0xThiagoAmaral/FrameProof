import argparse
import csv
import json
import math
from pathlib import Path


SOURCE_DISTINCTION = (
    "Intervalos entre chamadas Present da aplicação (CPU/API). "
    "Diferem de quadros exibidos pelo compositor do Windows e de métricas do OBS Studio."
)

METRIC_SPECS = {
    "p99_ms": {
        "name": "P99",
        "unit": "ms",
        "source": "PresentMon (MsBetweenPresents)",
        "method": "Percentil 99 por posto mais próximo",
        "better": "lower",
        "kind": "present_call_interval",
    },
    "one_percent_low_fps": {
        "name": "1% low",
        "unit": "FPS",
        "source": "PresentMon (MsBetweenPresents)",
        "method": "1000 dividido pela média do 1% de intervalos mais lentos (teto de N/100)",
        "better": "higher",
        "kind": "present_call_interval",
    },
    "above_7.14_ms_per_min": {
        "name": "Intervalos > 7,14 ms / min",
        "unit": "intervalos/min",
        "source": "PresentMon (MsBetweenPresents)",
        "method": "Contagem de intervalos > 7,14 ms normalizada por minuto de duração acumulada",
        "better": "lower",
        "kind": "present_call_interval",
    },
    "above_16.67_ms_per_min": {
        "name": "Intervalos > 16,67 ms / min",
        "unit": "intervalos/min",
        "source": "PresentMon (MsBetweenPresents)",
        "method": "Contagem de intervalos > 16,67 ms normalizada por minuto de duração acumulada",
        "better": "lower",
        "kind": "present_call_interval",
    },
    "p50_ms": {
        "name": "P50",
        "unit": "ms",
        "source": "PresentMon (MsBetweenPresents)",
        "method": "Percentil 50 por posto mais próximo (mediana)",
        "better": "lower",
        "kind": "present_call_interval",
    },
    "p95_ms": {
        "name": "P95",
        "unit": "ms",
        "source": "PresentMon (MsBetweenPresents)",
        "method": "Percentil 95 por posto mais próximo",
        "better": "lower",
        "kind": "present_call_interval",
    },
    "mean_fps": {
        "name": "FPS médio",
        "unit": "FPS",
        "source": "PresentMon (MsBetweenPresents)",
        "method": "1000 multiplicado pela quantidade de amostras dividido pela duração acumulada em ms",
        "better": "higher",
        "kind": "present_call_interval",
    },
}


def analyze(path):
    path = Path(path)
    identity = None
    samples = []
    with path.open(encoding="utf-8-sig", newline="") as stream:
        reader = csv.DictReader(stream)
        keys = ("Application", "ProcessID", "SwapChainAddress")
        fields = reader.fieldnames or []
        required = {*keys, "MsBetweenPresents"}
        if not required.issubset(fields) or len(fields) != len(set(fields)):
            raise ValueError("Esquema incompatível. Use o CSV do PresentMon com --v1_metrics.")
        for row in reader:
            current = tuple((row[k] or "").strip() for k in keys)
            if not all(current) or None in row:
                raise ValueError(f"Linha {reader.line_num} incompleta ou com colunas extras.")
            first = identity is None
            if not first and current != identity:
                raise ValueError("A captura contém mais de uma aplicação, PID ou swap chain.")
            identity = current
            value = (row["MsBetweenPresents"] or "").strip()
            if first and value.upper() in {"", "NA", "N/A"}:
                continue
            try:
                interval = float(value)
            except ValueError:
                raise ValueError(f"Intervalo inválido na linha {reader.line_num}.") from None
            if first and interval == 0:
                continue
            if not math.isfinite(interval) or interval <= 0:
                raise ValueError(f"Intervalo inválido na linha {reader.line_num}.")
            samples.append(interval)
    if not samples:
        raise ValueError("Captura sem intervalos válidos.")
    samples.sort()
    count = len(samples)
    total_ms = math.fsum(samples)
    slow = samples[-math.ceil(count / 100):]
    report = {
        "application": identity[0],
        "process_id": identity[1],
        "swap_chain_address": identity[2],
        "metric": "MsBetweenPresents",
        "intervals": count,
        "duration_ms": total_ms,
        "mean_fps": 1000 * count / total_ms,
        "one_percent_low_fps": 1000 * len(slow) / math.fsum(slow),
    }
    for percentile in (50, 95, 99):
        report[f"p{percentile}_ms"] = samples[math.ceil(count * percentile / 100) - 1]
    for threshold in (7.14, 16.67):
        hits = sum(interval > threshold for interval in samples)
        report[f"above_{threshold}_ms_per_min"] = hits * 60000 / total_ms
    return report


def compare(baseline, comparison):
    baseline_file = None
    comparison_file = None

    if isinstance(baseline, (str, Path)):
        baseline_file = str(baseline)
        baseline_report = analyze(baseline)
    elif isinstance(baseline, dict):
        baseline_report = baseline
        baseline_file = baseline.get("file", "Linha de base (A)")
    else:
        raise TypeError("Linha de base deve ser um Path, str ou dict de relatório.")

    if isinstance(comparison, (str, Path)):
        comparison_file = str(comparison)
        comparison_report = analyze(comparison)
    elif isinstance(comparison, dict):
        comparison_report = comparison
        comparison_file = comparison.get("file", "Ajuste (B)")
    else:
        raise TypeError("Comparação deve ser um Path, str ou dict de relatório.")

    app_a = (baseline_report.get("application") or "").strip()
    app_b = (comparison_report.get("application") or "").strip()
    if not app_a or not app_b:
        raise ValueError("Aplicação não informada no relatório.")
    if app_a.casefold() != app_b.casefold():
        raise ValueError(
            f"Comparações incompatíveis: aplicações diferentes ('{app_a}' != '{app_b}')."
        )

    metric_a = (baseline_report.get("metric") or "").strip()
    metric_b = (comparison_report.get("metric") or "").strip()
    if not metric_a or not metric_b:
        raise ValueError("Métrica não informada no relatório.")
    if metric_a != metric_b:
        raise ValueError(
            f"Comparações incompatíveis: métricas diferentes ('{metric_a}' != '{metric_b}')."
        )

    metrics_diff = {}
    for key, spec in METRIC_SPECS.items():
        if key not in baseline_report or key not in comparison_report:
            continue
        val_a = float(baseline_report[key])
        val_b = float(comparison_report[key])
        diff = val_b - val_a
        if val_a != 0:
            percent_change = (diff / val_a) * 100.0
        elif val_b == 0:
            percent_change = 0.0
        else:
            percent_change = None

        if abs(diff) < 1e-9:
            direction = "estável"
        elif spec["better"] == "lower":
            direction = "melhora" if diff < 0 else "piora"
        else:
            direction = "melhora" if diff > 0 else "piora"

        metrics_diff[key] = {
            "name": spec["name"],
            "unit": spec["unit"],
            "source": spec["source"],
            "method": spec["method"],
            "kind": spec["kind"],
            "baseline": val_a,
            "comparison": val_b,
            "diff": diff,
            "percent_change": percent_change,
            "direction": direction,
        }

    return {
        "application": app_a,
        "metric": metric_a,
        "source": "PresentMon (MsBetweenPresents)",
        "distinction": SOURCE_DISTINCTION,
        "baseline": {
            "file": baseline_file,
            **baseline_report,
        },
        "comparison": {
            "file": comparison_file,
            **comparison_report,
        },
        "metrics": metrics_diff,
    }


def format_comparison(result):
    app = result["application"]
    metric = result["metric"]
    distinction = result.get("distinction", SOURCE_DISTINCTION)

    base = result["baseline"]
    comp = result["comparison"]
    metrics = result["metrics"]

    lines = [
        "=" * 106,
        "Comparação A/B de Desempenho",
        f"Aplicação (Jogo): {app}",
        f"Métrica:          {metric} (tempo entre chamadas Present da aplicação)",
        "Fonte dos dados:  PresentMon (v1 metrics)",
        f"Diferenciação:    {distinction}",
        "-" * 106,
        f"Captura A (Linha de base): {base.get('file', 'A')}",
        f"  PID: {base.get('process_id', 'N/D')} | SwapChain: {base.get('swap_chain_address', 'N/D')} | Duração: {base.get('duration_ms', 0):.2f} ms | Intervalos: {base.get('intervals', 0)}",
        f"Captura B (Ajuste):        {comp.get('file', 'B')}",
        f"  PID: {comp.get('process_id', 'N/D')} | SwapChain: {comp.get('swap_chain_address', 'N/D')} | Duração: {comp.get('duration_ms', 0):.2f} ms | Intervalos: {comp.get('intervals', 0)}",
        "-" * 106,
        f"{'Métrica':<30} {'Linha de base (A)':<20} {'Ajuste (B)':<20} {'Diferença':<24} {'Direção':<10}",
        "-" * 106,
    ]

    primary_keys = ("p99_ms", "one_percent_low_fps", "above_7.14_ms_per_min", "above_16.67_ms_per_min")
    secondary_keys = ("p50_ms", "mean_fps")

    def _format_row(key):
        item = metrics.get(key)
        if not item:
            return ""
        name = item["name"]
        unit = item["unit"]
        unit_label = "ms" if unit == "ms" else ("FPS" if unit == "FPS" else "/min")
        val_a = f"{item['baseline']:.2f} {unit_label}"
        val_b = f"{item['comparison']:.2f} {unit_label}"
        diff_val = item["diff"]
        diff_sign = "+" if diff_val > 0 else ""
        diff_str = f"{diff_sign}{diff_val:.2f} {unit_label}"
        pct = item["percent_change"]
        if pct is not None:
            pct_sign = "+" if pct > 0 else ""
            diff_str += f" ({pct_sign}{pct:.1f}%)"
        direction = item["direction"]
        return f"{name:<30} {val_a:<20} {val_b:<20} {diff_str:<24} {direction:<10}"

    for k in primary_keys:
        row = _format_row(k)
        if row:
            lines.append(row)

    lines.append("-" * 106)
    for k in secondary_keys:
        row = _format_row(k)
        if row:
            lines.append(row)

    lines.append("=" * 106)
    return "\n".join(lines)


def main(argv=None):
    parser = argparse.ArgumentParser(description="Analisa e compara fluxos de apresentação do PresentMon.")
    parser.add_argument(
        "captures",
        nargs="+",
        type=Path,
        metavar="CSV",
        help="Caminho para 1 CSV (análise individual) ou 2 CSVs (comparação A/B).",
    )
    parser.add_argument(
        "--json",
        action="store_true",
        help="Exibe a saída em formato JSON.",
    )
    args = parser.parse_args(argv)

    if len(args.captures) == 1:
        try:
            report = analyze(args.captures[0])
        except (OSError, ValueError, csv.Error) as exc:
            parser.exit(2, f"Erro: {exc}\n")
        print(json.dumps(report, indent=2, allow_nan=False))
    elif len(args.captures) == 2:
        try:
            result = compare(args.captures[0], args.captures[1])
        except (OSError, ValueError, csv.Error) as exc:
            parser.exit(2, f"Erro: {exc}\n")
        if args.json:
            print(json.dumps(result, indent=2, allow_nan=False, ensure_ascii=False))
        else:
            print(format_comparison(result))
    else:
        parser.exit(2, "Erro: Forneça 1 arquivo para análise individual ou 2 para comparação A/B.\n")
