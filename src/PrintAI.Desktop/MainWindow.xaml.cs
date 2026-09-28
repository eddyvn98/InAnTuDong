using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using PrintAI.Domain;

namespace PrintAI.Desktop;

public partial class MainWindow : Window
{
    private DesktopSession? _session;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await WebView.EnsureCoreWebView2Async();

            var uiFolder = Path.Combine(AppContext.BaseDirectory, "ui");
            WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "app.printai",
                uiFolder,
                CoreWebView2HostResourceAccessKind.DenyCors);

            _session = new DesktopSession();
            WebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            WebView.Source = new Uri("https://app.printai/index.html");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Print AI startup error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void OnWebMessageReceived(
        object? sender,
        CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (_session is null)
            return;

        try
        {
            using var message = JsonDocument.Parse(e.WebMessageAsJson);
            var root = message.RootElement;
            var action = root.GetProperty("action").GetString();

            switch (action)
            {
                case "ready":
                    break;
                case "pickFiles":
                    _session.PickFiles(this);
                    break;
                case "pickFolder":
                    _session.PickFolder(this);
                    break;
                case "selectPrinter":
                    _session.SelectPrinter(root.GetProperty("printer").GetString());
                    break;
                case "selectScanner":
                    _session.SelectScanner(root.GetProperty("scanner").GetString());
                    break;
                case "scan":
                    await _session.ScanAsync(
                        root.GetProperty("dpi").GetInt32(),
                        root.GetProperty("colorMode").GetString() ?? "Color",
                        root.GetProperty("outputFormat").GetString() ?? "Png",
                        root.GetProperty("autoCrop").GetBoolean(),
                        root.GetProperty("autoDeskew").GetBoolean());
                    break;
                case "selectPage":
                    _session.SelectPage(root.GetProperty("index").GetInt32());
                    break;
                case "selectOutputPage":
                    _session.SelectOutputPage(root.GetProperty("index").GetInt32());
                    break;
                case "configurePlanner":
                    _session.ConfigurePlanner(
                        root.GetProperty("endpoint").GetString() ?? "",
                        root.GetProperty("model").GetString() ?? "",
                        root.TryGetProperty("apiKey", out var key)
                            ? key.GetString()
                            : null);
                    break;
                case "plan":
                    await _session.PlanAsync(
                        root.GetProperty("request").GetString() ?? "",
                        root.GetProperty("mode").GetString() ?? "Safe");
                    break;
                case "applyJobSettings":
                    _session.ApplyJobEdits(ReadEdits(root));
                    break;
                case "saveRecipe":
                    _session.SaveRecipe(
                        root.GetProperty("name").GetString() ?? "",
                        root.GetProperty("directPrintEligible").GetBoolean());
                    break;
                case "applyRecipe":
                    _session.ApplyRecipe(root.GetProperty("recipeId").GetString() ?? "");
                    break;
                case "applyBuiltInWorkflow":
                    _session.ApplyBuiltInWorkflow(
                        root.GetProperty("workflowId").GetString() ?? "");
                    break;
                case "generateAutoLayouts":
                    _session.GenerateAutoLayouts(
                        ReadCompositionItems(root),
                        root.GetProperty("preference").GetString() ?? "Balanced");
                    break;
                case "applyAutoLayout":
                    _session.ApplyAutoLayout(
                        root.GetProperty("candidateId").GetString() ?? "");
                    break;
                case "composeMixedPages":
                    _session.ComposeMixedPages(
                        ReadCompositionItems(root),
                        root.GetProperty("itemWidthMm").GetDouble(),
                        root.GetProperty("itemHeightMm").GetDouble(),
                        root.GetProperty("gapMm").GetDouble(),
                        root.GetProperty("marginMm").GetDouble(),
                        root.GetProperty("allowRotate").GetBoolean(),
                        root.GetProperty("cutMarks").GetBoolean(),
                        root.GetProperty("fit").GetString() ?? "Contain");
                    break;
                case "composeCccdFrontBack":
                    _session.ComposeCccdFrontBack(
                        root.GetProperty("frontPageIndex").GetInt32(),
                        root.GetProperty("backPageIndex").GetInt32());
                    break;
                case "applyCustomLabelSheet":
                    _session.ApplyCustomLabelSheet(
                        root.GetProperty("itemWidthMm").GetDouble(),
                        root.GetProperty("itemHeightMm").GetDouble(),
                        root.GetProperty("copies").GetInt32(),
                        root.GetProperty("gapMm").GetDouble(),
                        root.GetProperty("marginMm").GetDouble(),
                        root.GetProperty("allowRotate").GetBoolean(),
                        root.GetProperty("cutMarks").GetBoolean(),
                        root.GetProperty("fit").GetString() ?? "Contain");
                    break;
                case "deleteRecipe":
                    _session.DeleteRecipe(root.GetProperty("recipeId").GetString() ?? "");
                    break;
                case "print":
                    _session.PrintCurrent();
                    break;
                case "printJob":
                    _session.PrintJob();
                    break;
                case "printAllSources":
                    _session.PrintAllSources();
                    break;
                case "clearHistory":
                    _session.ClearHistory();
                    break;
                case "refreshReadiness":
                    _session.RefreshReadiness();
                    break;
                case "exportSupportReport":
                    _session.ExportSupportReport(this);
                    break;
                case "clear":
                    _session.Clear();
                    break;
            }

            await PublishStateAsync();
        }
        catch (Exception ex)
        {
            await PostAsync(new
            {
                type = "error",
                message = ex.Message
            });

            await PublishStateAsync();
        }
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (_session is null ||
            !e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            _session.AddPaths(paths);
            await PublishStateAsync();
        }
    }

    private Task PublishStateAsync() =>
        PostAsync(new
        {
            type = "state",
            data = _session!.BuildState()
        });

    private Task PostAsync(object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        WebView.CoreWebView2.PostWebMessageAsJson(json);
        return Task.CompletedTask;
    }

    private static IReadOnlyList<DesktopCompositionItem> ReadCompositionItems(
        JsonElement root)
    {
        if (!root.TryGetProperty("items", out var items) ||
            items.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException("Danh sách source/page không hợp lệ.");
        }

        return items
            .EnumerateArray()
            .Select(item => new DesktopCompositionItem(
                item.GetProperty("pageIndex").GetInt32(),
                item.GetProperty("copies").GetInt32()))
            .ToArray();
    }

    private static DesktopJobEdits ReadEdits(JsonElement root)
    {
        if (!Enum.TryParse<LayoutMode>(
                root.GetProperty("mode").GetString(),
                ignoreCase: true,
                out var mode))
        {
            throw new ArgumentException("Invalid layout mode.");
        }

        if (!Enum.TryParse<FitMode>(
                root.GetProperty("fit").GetString(),
                ignoreCase: true,
                out var fit))
        {
            throw new ArgumentException("Invalid fit mode.");
        }

        return new(
            Mode: mode,
            ItemWidthMm: root.GetProperty("itemWidthMm").GetDouble(),
            ItemHeightMm: root.GetProperty("itemHeightMm").GetDouble(),
            GapMm: root.GetProperty("gapMm").GetDouble(),
            MarginMm: root.GetProperty("marginMm").GetDouble(),
            Copies: root.GetProperty("copies").GetInt32(),
            AllowRotate: root.GetProperty("allowRotate").GetBoolean(),
            CutMarks: root.GetProperty("cutMarks").GetBoolean(),
            Fit: fit);
    }
}
