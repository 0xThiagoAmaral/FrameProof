using System.Collections.ObjectModel;
using GamePerfLab.Core;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;

namespace GamePerfLab.App;

public sealed partial class MainWindow : Window
{
    public record MetricRowViewModel(
        string Name,
        string BaselineText,
        string ComparisonText,
        string DiffText,
        string DirectionText,
        Brush DirectionBackgroundBrush,
        Brush DirectionForegroundBrush
    );

    private readonly ObservableCollection<MetricRowViewModel> _metricsList = new();

    public MainWindow()
    {
        InitializeComponent();
        ListMetrics.ItemsSource = _metricsList;

        // Populate sample paths if available in working directory or repo root
        string? repoRoot = FindRepoRoot();
        if (repoRoot != null)
        {
            string sampleA = Path.Combine(repoRoot, "examples", "synthetic.csv");
            string sampleB = Path.Combine(repoRoot, "examples", "synthetic_optimized.csv");
            if (File.Exists(sampleA)) TxtCaptureA.Text = sampleA;
            if (File.Exists(sampleB)) TxtCaptureB.Text = sampleB;
        }
    }

    private async void BtnBrowseA_Click(object sender, RoutedEventArgs e)
    {
        string? path = await PickCsvFileAsync();
        if (!string.IsNullOrEmpty(path))
        {
            TxtCaptureA.Text = path;
        }
    }

    private async void BtnBrowseB_Click(object sender, RoutedEventArgs e)
    {
        string? path = await PickCsvFileAsync();
        if (!string.IsNullOrEmpty(path))
        {
            TxtCaptureB.Text = path;
        }
    }

    private void BtnSampleA_Click(object sender, RoutedEventArgs e)
    {
        string? repoRoot = FindRepoRoot();
        if (repoRoot != null)
        {
            string path = Path.Combine(repoRoot, "examples", "synthetic.csv");
            if (File.Exists(path)) TxtCaptureA.Text = path;
        }
    }

    private void BtnSampleB_Click(object sender, RoutedEventArgs e)
    {
        string? repoRoot = FindRepoRoot();
        if (repoRoot != null)
        {
            string path = Path.Combine(repoRoot, "examples", "synthetic_optimized.csv");
            if (File.Exists(path)) TxtCaptureB.Text = path;
        }
    }

    private void BtnIncompatible_Click(object sender, RoutedEventArgs e)
    {
        string? repoRoot = FindRepoRoot();
        if (repoRoot != null)
        {
            string path = Path.Combine(repoRoot, "examples", "synthetic_incompatible_app.csv");
            if (File.Exists(path)) TxtCaptureB.Text = path;
        }
    }

    private async void BtnCompare_Click(object sender, RoutedEventArgs e)
    {
        string pathA = TxtCaptureA.Text.Trim();
        string pathB = TxtCaptureB.Text.Trim();

        ErrorInfoBar.IsOpen = false;

        if (string.IsNullOrEmpty(pathA) || string.IsNullOrEmpty(pathB))
        {
            ShowError("Por favor, selecione ambos os arquivos CSV para realizar a comparação A/B.");
            return;
        }

        if (!File.Exists(pathA))
        {
            ShowError($"Arquivo da Captura A não encontrado: {pathA}");
            return;
        }

        if (!File.Exists(pathB))
        {
            ShowError($"Arquivo da Captura B não encontrado: {pathB}");
            return;
        }

        LoadingRing.Visibility = Visibility.Visible;
        LoadingRing.IsActive = true;
        BtnCompare.IsEnabled = false;

        try
        {
            // Coleta/análise executada em thread de trabalho fora do processo da UI
            var result = await Task.Run(() => PresentMonAnalyzer.Compare(pathA, pathB));

            DisplayResult(result);
        }
        catch (Exception ex)
        {
            ResultsCard.Visibility = Visibility.Collapsed;
            ShowError(ex.Message);
        }
        finally
        {
            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;
            BtnCompare.IsEnabled = true;
        }
    }

    private void DisplayResult(ComparisonResult result)
    {
        TxtResultGame.Text = $"Jogo: {result.Application}";
        TxtResultMetric.Text = $"Métrica: {result.Metric} (tempo entre chamadas Present da aplicação)";

        bool pidChanged = result.Baseline.ProcessId != result.Comparison.ProcessId;
        TxtPidChangeNote.Text = pidChanged
            ? $"PID A: {result.Baseline.ProcessId} → PID B: {result.Comparison.ProcessId} (PID mudou entre partidas - aceito)"
            : $"PID: {result.Baseline.ProcessId} (mesmo PID)";

        _metricsList.Clear();

        string[] order = ["p99_ms", "one_percent_low_fps", "above_7.14_ms_per_min", "above_16.67_ms_per_min", "p50_ms", "mean_fps"];
        foreach (var key in order)
        {
            if (!result.Metrics.TryGetValue(key, out var m)) continue;

            string unitLabel = m.Unit switch
            {
                "ms" => "ms",
                "FPS" => "FPS",
                _ => "/min"
            };

            string valA = $"{m.Baseline:F2} {unitLabel}";
            string valB = $"{m.Comparison:F2} {unitLabel}";
            string diffSign = m.Diff > 0 ? "+" : "";
            string diffStr = $"{diffSign}{m.Diff:F2} {unitLabel}";
            if (m.PercentChange.HasValue)
            {
                string pctSign = m.PercentChange.Value > 0 ? "+" : "";
                diffStr += $" ({pctSign}{m.PercentChange.Value:F1}%)";
            }

            Brush bgBrush;
            Brush fgBrush;

            if (m.Direction == "melhora")
            {
                bgBrush = new SolidColorBrush(ColorHelper.FromArgb(40, 0, 160, 40));
                fgBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 0, 140, 30));
            }
            else if (m.Direction == "piora")
            {
                bgBrush = new SolidColorBrush(ColorHelper.FromArgb(40, 220, 40, 40));
                fgBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 200, 30, 30));
            }
            else
            {
                bgBrush = new SolidColorBrush(ColorHelper.FromArgb(30, 128, 128, 128));
                fgBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 128, 128, 128));
            }

            _metricsList.Add(new MetricRowViewModel(
                Name: m.Name,
                BaselineText: valA,
                ComparisonText: valB,
                DiffText: diffStr,
                DirectionText: m.Direction.ToUpperInvariant(),
                DirectionBackgroundBrush: bgBrush,
                DirectionForegroundBrush: fgBrush
            ));
        }

        TxtSummaryA.Text = $"Arquivo: {result.Baseline.FilePath}\nPID: {result.Baseline.ProcessId} | SwapChain: {result.Baseline.SwapChainAddress}\nDuração: {result.Baseline.DurationMs:F2} ms | Intervalos: {result.Baseline.Intervals}";
        TxtSummaryB.Text = $"Arquivo: {result.Comparison.FilePath}\nPID: {result.Comparison.ProcessId} | SwapChain: {result.Comparison.SwapChainAddress}\nDuração: {result.Comparison.DurationMs:F2} ms | Intervalos: {result.Comparison.Intervals}";

        ResultsCard.Visibility = Visibility.Visible;
    }

    private void ShowError(string message)
    {
        ErrorInfoBar.Title = "Erro na Comparação";
        ErrorInfoBar.Message = message;
        ErrorInfoBar.IsOpen = true;
    }

    private async Task<string?> PickCsvFileAsync()
    {
        try
        {
            var picker = new FileOpenPicker();
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

            picker.ViewMode = PickerViewMode.List;
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add(".csv");

            var file = await picker.PickSingleFileAsync();
            return file?.Path;
        }
        catch
        {
            return null;
        }
    }

    private static string? FindRepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "README.md")) && Directory.Exists(Path.Combine(dir, "examples")))
            {
                return dir;
            }
            dir = Directory.GetParent(dir)?.FullName;
        }
        return null;
    }
}
