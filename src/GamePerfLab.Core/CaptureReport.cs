namespace GamePerfLab.Core;

public record CaptureReport(
    string Application,
    string ProcessId,
    string SwapChainAddress,
    string Metric,
    int Intervals,
    double DurationMs,
    double MeanFps,
    double OnePercentLowFps,
    double P50Ms,
    double P95Ms,
    double P99Ms,
    double Above714MsPerMin,
    double Above1667MsPerMin,
    string? FilePath = null
);
