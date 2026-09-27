using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;

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
            var action = message.RootElement.GetProperty("action").GetString();

            switch (action)
            {
                case "ready":
                    await PublishStateAsync();
                    break;
                case "pickFiles":
                    _session.PickFiles(this);
                    await PublishStateAsync();
                    break;
                case "pickFolder":
                    _session.PickFolder(this);
                    await PublishStateAsync();
                    break;
                case "selectPrinter":
                    _session.SelectPrinter(
                        message.RootElement.GetProperty("printer").GetString());
                    await PublishStateAsync();
                    break;
                case "print":
                    _session.Print();
                    await PublishStateAsync();
                    break;
                case "clear":
                    _session.Clear();
                    await PublishStateAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            await PostAsync(new
            {
                type = "error",
                message = ex.Message
            });
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
}
