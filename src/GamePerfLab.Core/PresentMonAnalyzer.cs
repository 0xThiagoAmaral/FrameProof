using System.Globalization;

namespace GamePerfLab.Core;

public static class PresentMonAnalyzer
{
    public static CaptureReport Analyze(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Arquivo não encontrado: {filePath}", filePath);
        }

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, true);

        string? headerLine = reader.ReadLine();
        if (headerLine == null)
        {
            throw new FormatException("Captura sem intervalos válidos.");
        }

        var headerFields = ParseCsvLine(headerLine);
        var fieldSet = new HashSet<string>(headerFields, StringComparer.Ordinal);
        if (headerFields.Count != fieldSet.Count)
        {
            throw new FormatException("Esquema incompatível. Use o CSV do PresentMon com --v1_metrics.");
        }

        int appIndex = headerFields.IndexOf("Application");
        int pidIndex = headerFields.IndexOf("ProcessID");
        int swapChainIndex = headerFields.IndexOf("SwapChainAddress");
        int metricIndex = headerFields.IndexOf("MsBetweenPresents");

        if (appIndex < 0 || pidIndex < 0 || swapChainIndex < 0 || metricIndex < 0)
        {
            throw new FormatException("Esquema incompatível. Use o CSV do PresentMon com --v1_metrics.");
        }

        string? app = null;
        string? pid = null;
        string? swapChain = null;
        var samples = new List<double>();
        int lineNum = 1;

        while (!reader.EndOfStream)
        {
            string? line = reader.ReadLine();
            lineNum++;
            if (line == null) break;
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = ParseCsvLine(line);
            if (parts.Count != headerFields.Count)
            {
                throw new FormatException($"Linha {lineNum} incompleta ou com colunas extras.");
            }

            string currentApp = parts[appIndex].Trim();
            string currentPid = parts[pidIndex].Trim();
            string currentSwapChain = parts[swapChainIndex].Trim();
            string rawValue = parts[metricIndex].Trim();

            if (string.IsNullOrEmpty(currentApp) || string.IsNullOrEmpty(currentPid) || string.IsNullOrEmpty(currentSwapChain))
            {
                throw new FormatException($"Linha {lineNum} incompleta ou com colunas extras.");
            }

            bool isFirstSample = (app == null);
            if (!isFirstSample && (currentApp != app || currentPid != pid || currentSwapChain != swapChain))
            {
                throw new FormatException("A captura contém mais de uma aplicação, PID ou swap chain.");
            }

            app = currentApp;
            pid = currentPid;
            swapChain = currentSwapChain;

            if (isFirstSample && (string.IsNullOrEmpty(rawValue) ||
                                  string.Equals(rawValue, "NA", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(rawValue, "N/A", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (!double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double interval))
            {
                throw new FormatException($"Intervalo inválido na linha {lineNum}.");
            }

            if (isFirstSample && interval == 0)
            {
                continue;
            }

            if (!double.IsFinite(interval) || interval <= 0)
            {
                throw new FormatException($"Intervalo inválido na linha {lineNum}.");
            }

            samples.Add(interval);
        }

        if (samples.Count == 0 || app == null || pid == null || swapChain == null)
        {
            throw new FormatException("Captura sem intervalos válidos.");
        }

        samples.Sort();
        int count = samples.Count;
        double totalMs = samples.Sum();

        int slowCount = (int)Math.Ceiling(count / 100.0);
        double slowSum = 0;
        for (int i = count - slowCount; i < count; i++)
        {
            slowSum += samples[i];
        }

        double meanFps = 1000.0 * count / totalMs;
        double onePercentLowFps = 1000.0 * slowCount / slowSum;

        double p50Ms = samples[(int)Math.Ceiling(count * 50 / 100.0) - 1];
        double p95Ms = samples[(int)Math.Ceiling(count * 95 / 100.0) - 1];
        double p99Ms = samples[(int)Math.Ceiling(count * 99 / 100.0) - 1];

        int hits714 = samples.Count(x => x > 7.14);
        double above714MsPerMin = hits714 * 60000.0 / totalMs;

        int hits1667 = samples.Count(x => x > 16.67);
        double above1667MsPerMin = hits1667 * 60000.0 / totalMs;

        return new CaptureReport(
            Application: app,
            ProcessId: pid,
            SwapChainAddress: swapChain,
            Metric: "MsBetweenPresents",
            Intervals: count,
            DurationMs: totalMs,
            MeanFps: meanFps,
            OnePercentLowFps: onePercentLowFps,
            P50Ms: p50Ms,
            P95Ms: p95Ms,
            P99Ms: p99Ms,
            Above714MsPerMin: above714MsPerMin,
            Above1667MsPerMin: above1667MsPerMin,
            FilePath: filePath
        );
    }

    public static ComparisonResult Compare(string baselinePath, string comparisonPath)
    {
        var baseline = Analyze(baselinePath);
        var comparison = Analyze(comparisonPath);
        return Compare(baseline, comparison);
    }

    public static ComparisonResult Compare(CaptureReport baseline, CaptureReport comparison)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(comparison);

        string appA = (baseline.Application ?? "").Trim();
        string appB = (comparison.Application ?? "").Trim();
        if (string.IsNullOrEmpty(appA) || string.IsNullOrEmpty(appB))
        {
            throw new FormatException("Aplicação não informada no relatório.");
        }

        if (!string.Equals(appA, appB, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Comparações incompatíveis: aplicações diferentes ('{appA}' != '{appB}').");
        }

        string metricA = (baseline.Metric ?? "").Trim();
        string metricB = (comparison.Metric ?? "").Trim();
        if (string.IsNullOrEmpty(metricA) || string.IsNullOrEmpty(metricB))
        {
            throw new FormatException("Métrica não informada no relatório.");
        }

        if (!string.Equals(metricA, metricB, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Comparações incompatíveis: métricas diferentes ('{metricA}' != '{metricB}').");
        }

        var metrics = new Dictionary<string, MetricComparison>();

        void CompareMetric(string key, double valA, double valB)
        {
            if (!MetricDefinitions.Specs.TryGetValue(key, out var spec)) return;

            double diff = valB - valA;
            double? percentChange = null;
            if (valA != 0)
            {
                percentChange = (diff / valA) * 100.0;
            }
            else if (valB == 0)
            {
                percentChange = 0.0;
            }

            string direction;
            if (Math.Abs(diff) < 1e-9)
            {
                direction = "estável";
            }
            else if (spec.Better == BetterDirection.Lower)
            {
                direction = diff < 0 ? "melhora" : "piora";
            }
            else
            {
                direction = diff > 0 ? "melhora" : "piora";
            }

            metrics[key] = new MetricComparison(
                Key: key,
                Name: spec.Name,
                Unit: spec.Unit,
                Source: spec.Source,
                Method: spec.Method,
                Kind: spec.Kind,
                Baseline: valA,
                Comparison: valB,
                Diff: diff,
                PercentChange: percentChange,
                Direction: direction
            );
        }

        CompareMetric("p99_ms", baseline.P99Ms, comparison.P99Ms);
        CompareMetric("one_percent_low_fps", baseline.OnePercentLowFps, comparison.OnePercentLowFps);
        CompareMetric("above_7.14_ms_per_min", baseline.Above714MsPerMin, comparison.Above714MsPerMin);
        CompareMetric("above_16.67_ms_per_min", baseline.Above1667MsPerMin, comparison.Above1667MsPerMin);
        CompareMetric("p50_ms", baseline.P50Ms, comparison.P50Ms);
        CompareMetric("p95_ms", baseline.P95Ms, comparison.P95Ms);
        CompareMetric("mean_fps", baseline.MeanFps, comparison.MeanFps);

        return new ComparisonResult(
            Application: appA,
            Metric: metricA,
            Source: "PresentMon (MsBetweenPresents)",
            Distinction: MetricDefinitions.SourceDistinction,
            Baseline: baseline,
            Comparison: comparison,
            Metrics: metrics
        );
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        int length = line.Length;
        int i = 0;
        while (i < length)
        {
            if (line[i] == '"')
            {
                i++;
                int start = i;
                while (i < length)
                {
                    if (line[i] == '"')
                    {
                        if (i + 1 < length && line[i + 1] == '"')
                        {
                            i += 2;
                        }
                        else
                        {
                            break;
                        }
                    }
                    else
                    {
                        i++;
                    }
                }
                string val = line.Substring(start, i - start).Replace("\"\"", "\"");
                result.Add(val);
                if (i < length && line[i] == '"') i++;
                if (i < length && line[i] == ',') i++;
            }
            else
            {
                int start = i;
                while (i < length && line[i] != ',') i++;
                result.Add(line.Substring(start, i - start));
                if (i < length && line[i] == ',') i++;
            }
        }
        return result;
    }
}
