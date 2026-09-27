using PrintAI.Domain;
using PrintAI.Layout;
using PrintAI.Rendering;
using PrintAI.Web;

var builder = WebApplication.CreateBuilder(args);
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "PrintAI.Web",
    utc = DateTimeOffset.UtcNow
}));

app.MapGet("/api/demo-layout", () =>
{
    var spec = DemoJob();
    var result = LayoutEngine.Layout(spec);

    return Results.Ok(new
    {
        spec.JobName,
        paper = new { spec.Paper.WidthMm, spec.Paper.HeightMm },
        result.Columns,
        result.Rows,
        result.CapacityPerPage,
        result.Rotated,
        pages = result.Placements.Count == 0 ? 0 : result.Placements.Max(p => p.Page) + 1,
        placements = result.Placements
    });
});

app.MapGet("/api/demo-preview.png", () =>
{
    var spec = DemoJob();
    var layout = LayoutEngine.Layout(spec);
    using var source = DemoSourceFactory.Create();
    var png = A4PreviewRenderer.RenderPng(spec, layout, source, page: 0, dpi: 120);

    return Results.File(png, "image/png");
});

app.MapFallbackToFile("index.html");
app.Run();

static PrintJobSpec DemoJob() => new(
    "Demo 4x6cm",
    [new SourceSpec("generated-demo.png", 20)],
    new PaperSpec(),
    new LayoutSpec(
        LayoutMode.Grid,
        ItemWidthMm: 40,
        ItemHeightMm: 60,
        GapMm: 3,
        MarginMm: 5,
        AllowRotate: true,
        CutMarks: true,
        Fit: FitMode.Cover),
    new PrintSettings(Quality: PrintQuality.High),
    new PolicySpec());
