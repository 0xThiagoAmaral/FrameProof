import argparse
import csv
import json
import math
from pathlib import Path


def analyze(path):
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
    report = {"application": identity[0], "metric": "MsBetweenPresents", "intervals": count,
              "duration_ms": total_ms, "mean_fps": 1000 * count / total_ms,
              "one_percent_low_fps": 1000 * len(slow) / math.fsum(slow)}
    for percentile in (50, 95, 99):
        report[f"p{percentile}_ms"] = samples[math.ceil(count * percentile / 100) - 1]
    for threshold in (7.14, 16.67):
        hits = sum(interval > threshold for interval in samples)
        report[f"above_{threshold}_ms_per_min"] = hits * 60000 / total_ms
    return report


def main():
    parser = argparse.ArgumentParser(description="Analisa um fluxo de apresentação do PresentMon.")
    parser.add_argument("capture", type=Path)
    args = parser.parse_args()
    try:
        report = analyze(args.capture)
    except (OSError, ValueError, csv.Error) as exc:
        parser.exit(2, f"Erro: {exc}\n")
    print(json.dumps(report, indent=2, allow_nan=False))


if __name__ == "__main__":
    main()
