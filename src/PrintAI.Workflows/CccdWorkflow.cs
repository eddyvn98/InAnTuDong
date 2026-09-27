using PrintAI.Domain;

namespace PrintAI.Workflows;

public static class CccdWorkflow
{
    public const string FrontBackId = "cccd-front-back";

    public static PrintJobSpec CreateFrontBackJob(
        SourceSpec front,
        SourceSpec back,
        string jobName = "CCCD trước + sau")
    {
        ArgumentNullException.ThrowIfNull(front);
        ArgumentNullException.ThrowIfNull(back);

        return new(
            jobName,
            [
                front with { Copies = 1 },
                back with { Copies = 1 }
            ],
            new PaperSpec(),
            new LayoutSpec(
                LayoutMode.Grid,
                ItemWidthMm: 85.60,
                ItemHeightMm: 53.98,
                GapMm: 8,
                MarginMm: 8,
                AllowRotate: false,
                CutMarks: false,
                Fit: FitMode.Contain),
            new PrintSettings(
                ColorMode: ColorMode.Color,
                Quality: PrintQuality.High),
            new PolicySpec(PreviewPolicy.Required));
    }
}
