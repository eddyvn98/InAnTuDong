using PrintAI.Domain;

namespace PrintAI.Workflows;

public enum WorkflowCategory
{
    IdentityDocument,
    IdPhoto,
    LabelSticker
}

public sealed record BuiltInWorkflowPreset(
    string Id,
    string Name,
    WorkflowCategory Category,
    string Description,
    LayoutSpec Layout,
    int SourceCopies,
    PrintSettings Print,
    PolicySpec Policy)
{
    public PrintJobSpec CreateJob(
        string sourcePath,
        string? jobName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        return new(
            jobName ?? Name,
            [new SourceSpec(sourcePath, SourceCopies)],
            new PaperSpec(),
            Layout,
            Print,
            Policy);
    }
}

public static class BuiltInWorkflowCatalog
{
    public const string CccdOneToOne = "cccd-1to1";
    public const string IdPhoto3x4 = "id-photo-3x4";
    public const string IdPhoto4x6 = "id-photo-4x6";
    public const string Label40x60 = "label-40x60";

    private static readonly BuiltInWorkflowPreset[] Presets =
    [
        new(
            CccdOneToOne,
            "CCCD 1:1",
            WorkflowCategory.IdentityDocument,
            "In một mặt thẻ đúng kích thước ID-1 85.60 x 53.98 mm.",
            new LayoutSpec(
                LayoutMode.Grid,
                ItemWidthMm: 85.60,
                ItemHeightMm: 53.98,
                GapMm: 5,
                MarginMm: 8,
                AllowRotate: true,
                CutMarks: false,
                Fit: FitMode.Contain),
            SourceCopies: 1,
            new PrintSettings(
                ColorMode: ColorMode.Color,
                Quality: PrintQuality.High),
            new PolicySpec(PreviewPolicy.Required)),
        new(
            IdPhoto3x4,
            "Ảnh thẻ 3x4",
            WorkflowCategory.IdPhoto,
            "Ảnh 30 x 40 mm, crop theo Cover, mặc định 8 bản và có đường cắt.",
            new LayoutSpec(
                LayoutMode.Grid,
                ItemWidthMm: 30,
                ItemHeightMm: 40,
                GapMm: 2,
                MarginMm: 5,
                AllowRotate: false,
                CutMarks: true,
                Fit: FitMode.Cover),
            SourceCopies: 8,
            new PrintSettings(
                ColorMode: ColorMode.Color,
                Quality: PrintQuality.High),
            new PolicySpec(PreviewPolicy.Required)),
        new(
            IdPhoto4x6,
            "Ảnh thẻ 4x6",
            WorkflowCategory.IdPhoto,
            "Ảnh 40 x 60 mm, crop theo Cover, mặc định 8 bản và có đường cắt.",
            new LayoutSpec(
                LayoutMode.Grid,
                ItemWidthMm: 40,
                ItemHeightMm: 60,
                GapMm: 2,
                MarginMm: 5,
                AllowRotate: false,
                CutMarks: true,
                Fit: FitMode.Cover),
            SourceCopies: 8,
            new PrintSettings(
                ColorMode: ColorMode.Color,
                Quality: PrintQuality.High),
            new PolicySpec(PreviewPolicy.Required)),
        new(
            Label40x60,
            "Label 40x60",
            WorkflowCategory.LabelSticker,
            "Label/sticker 40 x 60 mm, mặc định 12 bản, giữ toàn bộ nội dung.",
            new LayoutSpec(
                LayoutMode.Grid,
                ItemWidthMm: 40,
                ItemHeightMm: 60,
                GapMm: 2,
                MarginMm: 5,
                AllowRotate: true,
                CutMarks: true,
                Fit: FitMode.Contain),
            SourceCopies: 12,
            new PrintSettings(
                ColorMode: ColorMode.Color,
                Quality: PrintQuality.Standard),
            new PolicySpec(PreviewPolicy.Required))
    ];

    public static IReadOnlyList<BuiltInWorkflowPreset> All => Presets;

    public static BuiltInWorkflowPreset Get(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return Presets.FirstOrDefault(x =>
                string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Unknown workflow preset: {id}");
    }
}
