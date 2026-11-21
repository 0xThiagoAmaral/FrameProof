using GamePerfLab.Core;
using Xunit;

namespace GamePerfLab.Tests;

public class PresentMonAnalyzerTests
{
    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "README.md")) && Directory.Exists(Path.Combine(dir, "examples")))
            {
                return dir;
            }
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new InvalidOperationException("Não foi possível localizar a raiz do repositório.");
    }

    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string SyntheticCsv = Path.Combine(RepoRoot, "examples", "synthetic.csv");
    private static readonly string OptimizedCsv = Path.Combine(RepoRoot, "examples", "synthetic_optimized.csv");
    private static readonly string IncompatibleAppCsv = Path.Combine(RepoRoot, "examples", "synthetic_incompatible_app.csv");

    [Fact]
    public void Analyze_MatchesPythonReferenceValuesExactly()
    {
        var report = PresentMonAnalyzer.Analyze(SyntheticCsv);

        Assert.Equal("demo.exe", report.Application);
        Assert.Equal("100", report.ProcessId);
        Assert.Equal("0x01", report.SwapChainAddress);
        Assert.Equal("MsBetweenPresents", report.Metric);
        Assert.Equal(10, report.Intervals);
        Assert.Equal(60.0, report.DurationMs, precision: 6);
        Assert.Equal(166.66666666666666, report.MeanFps, precision: 6);
        Assert.Equal(50.0, report.OnePercentLowFps, precision: 6);
        Assert.Equal(4.0, report.P50Ms, precision: 6);
        Assert.Equal(20.0, report.P95Ms, precision: 6);
        Assert.Equal(20.0, report.P99Ms, precision: 6);
        Assert.Equal(2000.0, report.Above714MsPerMin, precision: 6);
        Assert.Equal(1000.0, report.Above1667MsPerMin, precision: 6);
    }

    [Fact]
    public void Compare_MatchesPythonReferenceComparisonAndAcceptsPidChange()
    {
        var result = PresentMonAnalyzer.Compare(SyntheticCsv, OptimizedCsv);

        Assert.Equal("demo.exe", result.Application);
        Assert.Equal("MsBetweenPresents", result.Metric);
        Assert.Equal("100", result.Baseline.ProcessId);
        Assert.Equal("200", result.Comparison.ProcessId);
        Assert.Equal("0x01", result.Baseline.SwapChainAddress);
        Assert.Equal("0x02", result.Comparison.SwapChainAddress);

        // P99: 20 ms -> 15 ms (-5.0 ms, -25.0%, melhora)
        var p99 = result.Metrics["p99_ms"];
        Assert.Equal(20.0, p99.Baseline, precision: 6);
        Assert.Equal(15.0, p99.Comparison, precision: 6);
        Assert.Equal(-5.0, p99.Diff, precision: 6);
        Assert.Equal(-25.0, p99.PercentChange!.Value, precision: 4);
        Assert.Equal("melhora", p99.Direction);

        // 1% Low: 50.0 FPS -> 66.666667 FPS (+16.67 FPS, +33.3%, melhora)
        var oneLow = result.Metrics["one_percent_low_fps"];
        Assert.Equal(50.0, oneLow.Baseline, precision: 6);
        Assert.Equal(66.66666666666667, oneLow.Comparison, precision: 6);
        Assert.Equal(16.66666666666667, oneLow.Diff, precision: 6);
        Assert.Equal(33.33333333333334, oneLow.PercentChange!.Value, precision: 4);
        Assert.Equal("melhora", oneLow.Direction);

        // > 7.14 ms / min: 2000.0 -> 1132.0754716981132 (-867.92, -43.4%, melhora)
        var above7 = result.Metrics["above_7.14_ms_per_min"];
        Assert.Equal(2000.0, above7.Baseline, precision: 6);
        Assert.Equal(1132.0754716981132, above7.Comparison, precision: 6);
        Assert.Equal(-867.9245283018868, above7.Diff, precision: 6);
        Assert.Equal(-43.39622641509434, above7.PercentChange!.Value, precision: 4);
        Assert.Equal("melhora", above7.Direction);

        // > 16.67 ms / min: 1000.0 -> 0.0 (-1000.0, -100.0%, melhora)
        var above16 = result.Metrics["above_16.67_ms_per_min"];
        Assert.Equal(1000.0, above16.Baseline, precision: 6);
        Assert.Equal(0.0, above16.Comparison, precision: 6);
        Assert.Equal(-1000.0, above16.Diff, precision: 6);
        Assert.Equal(-100.0, above16.PercentChange!.Value, precision: 4);
        Assert.Equal("melhora", above16.Direction);

        // P50: 4 ms -> 4 ms (0.0 ms, estável)
        var p50 = result.Metrics["p50_ms"];
        Assert.Equal(4.0, p50.Baseline, precision: 6);
        Assert.Equal(4.0, p50.Comparison, precision: 6);
        Assert.Equal(0.0, p50.Diff, precision: 6);
        Assert.Equal("estável", p50.Direction);
    }

    [Fact]
    public void Compare_RejectsIncompatibleApplications()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            PresentMonAnalyzer.Compare(SyntheticCsv, IncompatibleAppCsv));

        Assert.Contains("aplicações diferentes", ex.Message);
        Assert.Contains("demo.exe", ex.Message);
        Assert.Contains("other_game.exe", ex.Message);
    }

    [Fact]
    public void Compare_RejectsIncompatibleMetrics()
    {
        var reportA = PresentMonAnalyzer.Analyze(SyntheticCsv);
        var reportB = reportA with { Metric = "CPUFrameTime" };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            PresentMonAnalyzer.Compare(reportA, reportB));

        Assert.Contains("métricas diferentes", ex.Message);
    }

    [Fact]
    public void FormatTable_OutputsFormattedSideBySideTable()
    {
        var result = PresentMonAnalyzer.Compare(SyntheticCsv, OptimizedCsv);
        string table = result.FormatTable();

        Assert.Contains("Comparação A/B de Desempenho", table);
        Assert.Contains("demo.exe", table);
        Assert.Contains("MsBetweenPresents", table);
        Assert.Contains("P99", table);
        Assert.Contains("1% low", table);
        Assert.Contains("Intervalos > 7,14 ms / min", table);
        Assert.Contains("Intervalos > 16,67 ms / min", table);
        Assert.Contains("Linha de base (A)", table);
        Assert.Contains("Ajuste (B)", table);
        Assert.Contains("Diferença", table);
        Assert.Contains("Direção", table);
        Assert.Contains("melhora", table);
    }

    [Fact]
    public void Analyze_RejectsMalformedInputs()
    {
        string raw = File.ReadAllText(SyntheticCsv);
        var tempFile = Path.GetTempFileName();
        try
        {
            // Mixed application / pid / swapchain
            File.WriteAllText(tempFile, raw + "demo.exe,100,0x02,4\n");
            Assert.Throws<FormatException>(() => PresentMonAnalyzer.Analyze(tempFile));

            // Missing required column
            File.WriteAllText(tempFile, raw.Replace("MsBetweenPresents", "CPUFrameTime"));
            Assert.Throws<FormatException>(() => PresentMonAnalyzer.Analyze(tempFile));

            // Non-finite
            File.WriteAllText(tempFile, raw + "demo.exe,100,0x01,nan\n");
            Assert.Throws<FormatException>(() => PresentMonAnalyzer.Analyze(tempFile));

            // Empty
            File.WriteAllText(tempFile, "Application,ProcessID,SwapChainAddress,MsBetweenPresents\n");
            Assert.Throws<FormatException>(() => PresentMonAnalyzer.Analyze(tempFile));

            // Late zero
            File.WriteAllText(tempFile, raw + "demo.exe,100,0x01,0\n");
            Assert.Throws<FormatException>(() => PresentMonAnalyzer.Analyze(tempFile));
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
