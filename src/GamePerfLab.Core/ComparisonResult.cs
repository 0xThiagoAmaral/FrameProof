namespace GamePerfLab.Core;

public record MetricComparison(
    string Key,
    string Name,
    string Unit,
    string Source,
    string Method,
    string Kind,
    double Baseline,
    double Comparison,
    double Diff,
    double? PercentChange,
    string Direction
);

public record ComparisonResult(
    string Application,
    string Metric,
    string Source,
    string Distinction,
    CaptureReport Baseline,
    CaptureReport Comparison,
    IReadOnlyDictionary<string, MetricComparison> Metrics
)
{
    public string FormatTable()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(new string('=', 106));
        sb.AppendLine("Comparação A/B de Desempenho");
        sb.AppendLine($"Aplicação (Jogo): {Application}");
        sb.AppendLine($"Métrica:          {Metric} (tempo entre chamadas Present da aplicação)");
        sb.AppendLine("Fonte dos dados:  PresentMon (v1 metrics)");
        sb.AppendLine($"Diferenciação:    {Distinction}");
        sb.AppendLine(new string('-', 106));
        sb.AppendLine($"Captura A (Linha de base): {Baseline.FilePath ?? "A"}");
        sb.AppendLine($"  PID: {Baseline.ProcessId} | SwapChain: {Baseline.SwapChainAddress} | Duração: {Baseline.DurationMs:F2} ms | Intervalos: {Baseline.Intervals}");
        sb.AppendLine($"Captura B (Ajuste):        {Comparison.FilePath ?? "B"}");
        sb.AppendLine($"  PID: {Comparison.ProcessId} | SwapChain: {Comparison.SwapChainAddress} | Duração: {Comparison.DurationMs:F2} ms | Intervalos: {Comparison.Intervals}");
        sb.AppendLine(new string('-', 106));
        sb.AppendLine(string.Format("{0,-30} {1,-20} {2,-20} {3,-24} {4,-10}", "Métrica", "Linha de base (A)", "Ajuste (B)", "Diferença", "Direção"));
        sb.AppendLine(new string('-', 106));

        string[] primary = ["p99_ms", "one_percent_low_fps", "above_7.14_ms_per_min", "above_16.67_ms_per_min"];
        string[] secondary = ["p50_ms", "mean_fps"];

        void AppendRow(string k)
        {
            if (Metrics.TryGetValue(k, out var item))
            {
                string unitLabel = item.Unit switch
                {
                    "ms" => "ms",
                    "FPS" => "FPS",
                    _ => "/min"
                };

                string valA = $"{item.Baseline:F2} {unitLabel}";
                string valB = $"{item.Comparison:F2} {unitLabel}";
                string diffSign = item.Diff > 0 ? "+" : "";
                string diffStr = $"{diffSign}{item.Diff:F2} {unitLabel}";
                if (item.PercentChange.HasValue)
                {
                    string pctSign = item.PercentChange.Value > 0 ? "+" : "";
                    diffStr += $" ({pctSign}{item.PercentChange.Value:F1}%)";
                }
                sb.AppendLine(string.Format("{0,-30} {1,-20} {2,-20} {3,-24} {4,-10}", item.Name, valA, valB, diffStr, item.Direction));
            }
        }

        foreach (var k in primary) AppendRow(k);
        sb.AppendLine(new string('-', 106));
        foreach (var k in secondary) AppendRow(k);
        sb.AppendLine(new string('=', 106));

        return sb.ToString();
    }
}
