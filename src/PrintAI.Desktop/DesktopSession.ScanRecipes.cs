using System.IO;
using PrintAI.Domain;
using PrintAI.Planning;
using PrintAI.Recipes;
using PrintAI.Scanning;
using PrintAI.Windows.Scanning;

namespace PrintAI.Desktop;

public sealed partial class DesktopSession
{
    private readonly WiaScannerAdapter _scanner = new();
    private readonly PrintRecipeStore _recipeStore = new(
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrintAI",
            "recipes.json"));

    private string? _selectedScanner;
    private string? _selectedRecipeId;

    public void SelectScanner(string? scannerId)
    {
        if (!string.IsNullOrWhiteSpace(scannerId))
            _selectedScanner = scannerId;
    }

    public async Task ScanAsync(
        int dpi,
        string colorMode,
        string outputFormat,
        bool autoCrop,
        bool autoDeskew,
        CancellationToken cancellationToken = default)
    {
        var scanners = _scanner.Enumerate();
        if (scanners.Count == 0)
            throw new InvalidOperationException("Không tìm thấy scanner WIA.");

        var scanner = scanners.FirstOrDefault(x =>
                string.Equals(x.Id, _selectedScanner, StringComparison.OrdinalIgnoreCase))
            ?? scanners[0];

        _selectedScanner = scanner.Id;

        if (!Enum.TryParse<ScanColorMode>(colorMode, true, out var color))
            throw new ArgumentException("Scan color must be Color, Grayscale or BlackAndWhite.");

        if (!Enum.TryParse<ScanOutputFormat>(outputFormat, true, out var format))
            throw new ArgumentException("Scan output must be Png or Pdf.");

        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var rawPath = Path.Combine(_workDir, $"scan-{stamp}-raw.png");
        var processedPath = Path.Combine(_workDir, $"scan-{stamp}.png");

        _status = $"Đang scan từ {scanner.Name}…";

        await _scanner.ScanPageAsync(
            scanner.Id,
            new ScanCaptureSettings(dpi, color),
            rawPath,
            cancellationToken);

        var processed = ScanImageProcessor.Process(
            rawPath,
            processedPath,
            new ScanProcessingSettings(
                AutoCrop: autoCrop,
                AutoDeskew: autoDeskew));

        var sourcePath = processed.OutputPath;
        if (format == ScanOutputFormat.Pdf)
        {
            sourcePath = Path.Combine(_workDir, $"scan-{stamp}.pdf");
            ScanPdfWriter.Write([processed.OutputPath], sourcePath);
        }

        AddPaths([sourcePath]);

        var scannedPageIndex = _pages.FindIndex(x =>
            string.Equals(
                x.SourcePath,
                Path.GetFullPath(sourcePath),
                StringComparison.OrdinalIgnoreCase));

        if (scannedPageIndex >= 0)
        {
            _selectedPage = scannedPageIndex;
            _selectedOutputPage = 0;
            RebuildPreview();
        }

        _status =
            $"Scan xong {processed.PixelWidth}×{processed.PixelHeight}px" +
            (processed.Cropped ? " · auto-crop" : "") +
            (Math.Abs(processed.DeskewDegrees) >= 0.15
                ? $" · deskew {processed.DeskewDegrees:+0.##;-0.##;0}°"
                : "") +
            $" · {format}.";
    }

    public void SaveRecipe(string name, bool directPrintEligible)
    {
        var page = CurrentPage()
            ?? throw new InvalidOperationException("Không có source page để lưu recipe.");

        var job = CurrentJob(page);
        var existing = !string.IsNullOrWhiteSpace(_selectedRecipeId)
            ? _recipeStore.Read().FirstOrDefault(x =>
                string.Equals(x.Id, _selectedRecipeId, StringComparison.OrdinalIgnoreCase))
            : null;

        var saved = _recipeStore.Save(new PrintRecipe(
            existing?.Id ?? "",
            name,
            job.Layout,
            job.Print,
            directPrintEligible
                ? new PolicySpec(PreviewPolicy.Direct)
                : job.Policy,
            job.Sources.FirstOrDefault()?.Copies ?? 1,
            directPrintEligible));

        _selectedRecipeId = saved.Id;
        _status = $"Đã lưu recipe: {saved.Name}.";
    }

    public void ApplyRecipe(string recipeId)
    {
        var page = CurrentPage()
            ?? throw new InvalidOperationException("Không có source page để áp dụng recipe.");

        var recipe = _recipeStore.Read().FirstOrDefault(x =>
            string.Equals(x.Id, recipeId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Recipe không tồn tại.");

        _selectedRecipeId = recipe.Id;
        _activeJob = recipe.CreateJob(page.SourcePath, recipe.Name);
        _planResult = null;
        _lastRequest = $"recipe:{recipe.Name}";
        _selectedOutputPage = 0;
        RebuildPreview();

        var outcome = new PlanningOutcome(
            _activeJob,
            1.0,
            [],
            [],
            "{}");

        var decision = PrintPolicyEngine.Decide(
            outcome,
            new PolicyContext(
                SafetyMode.Smart,
                IsKnownRecipe: recipe.DirectPrintEligible,
                WasPreviouslyApproved: recipe.DirectPrintEligible,
                IsVerifiedPrinterProfile: IsVerifiedPrinter()));

        _status =
            $"Recipe {recipe.Name}: {decision.Kind}. {decision.Reason}";

        if (recipe.DirectPrintEligible &&
            recipe.Policy.Preview == PreviewPolicy.Direct &&
            decision.Kind == PolicyDecisionKind.Direct)
        {
            PrintJob();
        }
    }

    public void DeleteRecipe(string recipeId)
    {
        if (_recipeStore.Delete(recipeId))
        {
            if (string.Equals(
                    _selectedRecipeId,
                    recipeId,
                    StringComparison.OrdinalIgnoreCase))
            {
                _selectedRecipeId = null;
            }

            _status = "Đã xóa recipe.";
        }
    }

    private IReadOnlyList<DesktopScanner> GetScanners()
    {
        try
        {
            var scanners = _scanner.Enumerate();

            if (string.IsNullOrWhiteSpace(_selectedScanner) ||
                scanners.All(x => !string.Equals(
                    x.Id,
                    _selectedScanner,
                    StringComparison.OrdinalIgnoreCase)))
            {
                _selectedScanner = scanners.FirstOrDefault()?.Id;
            }

            return scanners
                .Select(x => new DesktopScanner(x.Id, x.Name))
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private IReadOnlyList<DesktopRecipe> GetRecipes() =>
        _recipeStore.Read()
            .Select(x => new DesktopRecipe(
                x.Id,
                x.Name,
                x.DirectPrintEligible,
                x.UpdatedAt))
            .ToArray();
}
