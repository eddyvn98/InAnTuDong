using PrintAI.Domain;
using PrintAI.Layout;
using PrintAI.Workflows;
using Xunit;

namespace PrintAI.Tests;

public sealed class BuiltInWorkflowCatalogTests
{
    [Theory]
    [InlineData(BuiltInWorkflowCatalog.CccdOneToOne)]
    [InlineData(BuiltInWorkflowCatalog.IdPhoto3x4)]
    [InlineData(BuiltInWorkflowCatalog.IdPhoto4x6)]
    [InlineData(BuiltInWorkflowCatalog.Label40x60)]
    public void EveryPreset_CreatesValidA4Job(string presetId)
    {
        var preset = BuiltInWorkflowCatalog.Get(presetId);
        var job = preset.CreateJob("C:/source.png");

        var validation = PrintJobValidator.Validate(job);
        var layout = LayoutEngine.Layout(job);

        Assert.True(validation.IsValid);
        Assert.Equal(preset.SourceCopies, layout.Placements.Count);
        Assert.All(
            layout.Placements,
            placement => Assert.True(placement.Page >= 0));
    }

    [Fact]
    public void CccdPreset_UsesId1PhysicalSize()
    {
        var job = BuiltInWorkflowCatalog
            .Get(BuiltInWorkflowCatalog.CccdOneToOne)
            .CreateJob("C:/cccd.png");

        Assert.Equal(85.60, job.Layout.ItemWidthMm, 2);
        Assert.Equal(53.98, job.Layout.ItemHeightMm, 2);
        Assert.Equal(1, job.Sources[0].Copies);
        Assert.Equal(FitMode.Contain, job.Layout.Fit);
        Assert.Equal(PreviewPolicy.Required, job.Policy.Preview);
    }

    [Fact]
    public void IdPhotoPresets_UseCoverAndCutMarks()
    {
        var threeByFour = BuiltInWorkflowCatalog
            .Get(BuiltInWorkflowCatalog.IdPhoto3x4)
            .CreateJob("C:/portrait.png");

        var fourBySix = BuiltInWorkflowCatalog
            .Get(BuiltInWorkflowCatalog.IdPhoto4x6)
            .CreateJob("C:/portrait.png");

        Assert.Equal((30d, 40d), (
            threeByFour.Layout.ItemWidthMm,
            threeByFour.Layout.ItemHeightMm));
        Assert.Equal((40d, 60d), (
            fourBySix.Layout.ItemWidthMm,
            fourBySix.Layout.ItemHeightMm));

        Assert.Equal(FitMode.Cover, threeByFour.Layout.Fit);
        Assert.Equal(FitMode.Cover, fourBySix.Layout.Fit);
        Assert.True(threeByFour.Layout.CutMarks);
        Assert.True(fourBySix.Layout.CutMarks);
        Assert.Equal(8, threeByFour.Sources[0].Copies);
        Assert.Equal(8, fourBySix.Sources[0].Copies);
    }

    [Fact]
    public void LabelPreset_PreservesWholeContent()
    {
        var job = BuiltInWorkflowCatalog
            .Get(BuiltInWorkflowCatalog.Label40x60)
            .CreateJob("C:/label.png");

        Assert.Equal(40, job.Layout.ItemWidthMm);
        Assert.Equal(60, job.Layout.ItemHeightMm);
        Assert.Equal(12, job.Sources[0].Copies);
        Assert.Equal(FitMode.Contain, job.Layout.Fit);
        Assert.True(job.Layout.CutMarks);
    }
}
