using PrintAI.Domain;
using PrintAI.Layout;
using PrintAI.Rendering;
using Xunit;

namespace PrintAI.Tests;

public sealed class BookletImpositionResolverTests
{
    [Fact]
    public void EightPages_UsesStandardBookletOrder()
    {
        var group = Group();
        var logical = Enumerable.Range(0, 8)
            .Select(page => new SourceSpec(
                "C:/print/doc.pdf",
                PageIndex: page))
            .ToArray();

        var result = BookletImpositionResolver.Resolve(
            group,
            logical);

        Assert.Equal(
            [7, 0, 1, 6, 5, 2, 3, 4],
            result.Sources.Select(source => source.PageIndex).ToArray());
        Assert.DoesNotContain(
            result.Sources,
            source => source.IsBlank);
        Assert.Equal(8, result.PaddedPageCount);
        Assert.Equal(2, result.SheetCount);
        Assert.Equal(DuplexMode.ShortEdge, result.Duplex);
        Assert.Equal(PageOrientation.Landscape, result.Paper.Orientation);
    }

    [Fact]
    public void TenPages_PadsToTwelveWithVirtualBlankPages()
    {
        var logical = Enumerable.Range(0, 10)
            .Select(page => new SourceSpec(
                "C:/print/doc.pdf",
                PageIndex: page))
            .ToArray();

        var result = BookletImpositionResolver.Resolve(
            Group(),
            logical);

        Assert.Equal(12, result.PaddedPageCount);
        Assert.Equal(3, result.SheetCount);
        Assert.Equal(2, result.Sources.Count(source => source.IsBlank));

        Assert.True(result.Sources[0].IsBlank);
        Assert.Equal(0, result.Sources[1].PageIndex);
        Assert.Equal(1, result.Sources[2].PageIndex);
        Assert.True(result.Sources[3].IsBlank);
    }

    [Fact]
    public void SixteenPages_ProduceFourPhysicalSheets()
    {
        var logical = Enumerable.Range(0, 16)
            .Select(page => new SourceSpec(
                "C:/print/doc.pdf",
                PageIndex: page))
            .ToArray();

        var result = BookletImpositionResolver.Resolve(
            Group(),
            logical);

        var job = new PrintJobSpec(
            "booklet",
            result.Sources,
            result.Paper,
            result.Layout,
            new PrintSettings(
                Duplex: result.Duplex),
            new PolicySpec());

        Assert.Equal(4, result.SheetCount);
        Assert.Equal(8, SourceJobRenderer.GetOutputPageCount(job));

        var duplex = ManualDuplexPlanner.Create(
            outputPageCount: 8,
            copies: 1,
            mode: result.Duplex,
            backOrder: ManualDuplexBackOrder.Forward,
            longEdgeBackRotationDegrees: 0,
            shortEdgeBackRotationDegrees: 0);

        Assert.Equal(4, duplex.Sheets.Count);
    }

    [Fact]
    public void WiderGutter_ChangesTwoUpGeometry()
    {
        var result = BookletImpositionResolver.Resolve(
            Group(new BookletSpec(
                GutterMm: 10,
                MarginMm: 5)),
            Enumerable.Range(0, 4)
                .Select(page => new SourceSpec(
                    "C:/print/doc.pdf",
                    PageIndex: page))
                .ToArray());

        Assert.Equal(10, result.Layout.GapMm);
        Assert.Equal(138.5, result.Layout.ItemWidthMm, 6);

        var layout = GridLayoutEngine.Layout(
            new PrintJobSpec(
                "booklet",
                result.Sources,
                result.Paper,
                result.Layout,
                new PrintSettings(
                    Duplex: result.Duplex),
                new PolicySpec()));

        Assert.Equal(2, layout.Columns);
        Assert.Equal(1, layout.Rows);
    }

    private static PrintOutputGroupSpec Group(
        BookletSpec? booklet = null) =>
        new(
            Name: "booklet",
            Selections:
            [
                new PageSelectionSpec(
                    0,
                    [new PageRangeSpec(1, 1)])
            ],
            Paper: new PaperSpec(),
            Layout: new LayoutSpec(
                LayoutMode.ExactSize,
                200,
                287),
            Print: new OutputPrintSettings(),
            Booklet: booklet ?? new BookletSpec());
}
