namespace GamePerfLab.Core;

public enum BetterDirection
{
    Lower,
    Higher
}

public record MetricSpec(
    string Key,
    string Name,
    string Unit,
    string Source,
    string Method,
    BetterDirection Better,
    string Kind = "present_call_interval"
);

public static class MetricDefinitions
{
    public const string SourceDistinction =
        "Intervalos entre chamadas Present da aplicação (CPU/API). " +
        "Diferem de quadros exibidos pelo compositor do Windows e de métricas do OBS Studio.";

    public static readonly IReadOnlyDictionary<string, MetricSpec> Specs = new Dictionary<string, MetricSpec>
    {
        ["p99_ms"] = new(
            Key: "p99_ms",
            Name: "P99",
            Unit: "ms",
            Source: "PresentMon (MsBetweenPresents)",
            Method: "Percentil 99 por posto mais próximo",
            Better: BetterDirection.Lower
        ),
        ["one_percent_low_fps"] = new(
            Key: "one_percent_low_fps",
            Name: "1% low",
            Unit: "FPS",
            Source: "PresentMon (MsBetweenPresents)",
            Method: "1000 dividido pela média do 1% de intervalos mais lentos (teto de N/100)",
            Better: BetterDirection.Higher
        ),
        ["above_7.14_ms_per_min"] = new(
            Key: "above_7.14_ms_per_min",
            Name: "Intervalos > 7,14 ms / min",
            Unit: "intervalos/min",
            Source: "PresentMon (MsBetweenPresents)",
            Method: "Contagem de intervalos > 7,14 ms normalizada por minuto de duração acumulada",
            Better: BetterDirection.Lower
        ),
        ["above_16.67_ms_per_min"] = new(
            Key: "above_16.67_ms_per_min",
            Name: "Intervalos > 16,67 ms / min",
            Unit: "intervalos/min",
            Source: "PresentMon (MsBetweenPresents)",
            Method: "Contagem de intervalos > 16,67 ms normalizada por minuto de duração acumulada",
            Better: BetterDirection.Lower
        ),
        ["p50_ms"] = new(
            Key: "p50_ms",
            Name: "P50",
            Unit: "ms",
            Source: "PresentMon (MsBetweenPresents)",
            Method: "Percentil 50 por posto mais próximo (mediana)",
            Better: BetterDirection.Lower
        ),
        ["p95_ms"] = new(
            Key: "p95_ms",
            Name: "P95",
            Unit: "ms",
            Source: "PresentMon (MsBetweenPresents)",
            Method: "Percentil 95 por posto mais próximo",
            Better: BetterDirection.Lower
        ),
        ["mean_fps"] = new(
            Key: "mean_fps",
            Name: "FPS médio",
            Unit: "FPS",
            Source: "PresentMon (MsBetweenPresents)",
            Method: "1000 multiplicado pela quantidade de amostras dividido pela duração acumulada em ms",
            Better: BetterDirection.Higher
        ),
    };
}
