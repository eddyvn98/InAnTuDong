using PrintAI.Domain;
using Xunit;

namespace PrintAI.Tests;

public sealed class ManualDuplexPlannerTests
{
    [Fact]
    public void FivePages_MapToThreePhysicalSheets()
    {
        var plan = Create(
            outputPages: 5,
            copies: 1,
            backOrder: ManualDuplexBackOrder.Forward);

        Assert.Equal(3, plan.Sheets.Count);
        Assert.Equal(new[] { 0, 2, 4 }, plan.FrontPass.Select(x => x.OutputPageIndex));
        Assert.Equal(new[] { 1, 3 }, plan.BackPass.Select(x => x.OutputPageIndex));
        Assert.Null(plan.Sheets[2].BackOutputPageIndex);
    }

    [Fact]
    public void ReverseBackOrder_ReversesPhysicalSheets()
    {
        var plan = Create(
            outputPages: 6,
            copies: 1,
            backOrder: ManualDuplexBackOrder.Reverse);

        Assert.Equal(new[] { 5, 3, 1 }, plan.BackPass.Select(x => x.OutputPageIndex));
        Assert.Equal(new[] { 2, 1, 0 }, plan.BackPass.Select(x => x.SheetIndex));
    }

    [Fact]
    public void MultipleCopies_PreserveCopyBoundariesAndPairing()
    {
        var plan = Create(
            outputPages: 4,
            copies: 2,
            backOrder: ManualDuplexBackOrder.Forward);

        Assert.Equal(4, plan.Sheets.Count);

        Assert.Collection(
            plan.Sheets,
            sheet =>
            {
                Assert.Equal(0, sheet.CopyIndex);
                Assert.Equal(0, sheet.FrontOutputPageIndex);
                Assert.Equal(1, sheet.BackOutputPageIndex);
            },
            sheet =>
            {
                Assert.Equal(0, sheet.CopyIndex);
                Assert.Equal(2, sheet.FrontOutputPageIndex);
                Assert.Equal(3, sheet.BackOutputPageIndex);
            },
            sheet =>
            {
                Assert.Equal(1, sheet.CopyIndex);
                Assert.Equal(0, sheet.FrontOutputPageIndex);
                Assert.Equal(1, sheet.BackOutputPageIndex);
            },
            sheet =>
            {
                Assert.Equal(1, sheet.CopyIndex);
                Assert.Equal(2, sheet.FrontOutputPageIndex);
                Assert.Equal(3, sheet.BackOutputPageIndex);
            });
    }

    [Fact]
    public void ShortEdge_UsesShortEdgeRotation()
    {
        var plan = ManualDuplexPlanner.Create(
            outputPageCount: 2,
            copies: 1,
            mode: DuplexMode.ShortEdge,
            backOrder: ManualDuplexBackOrder.Forward,
            longEdgeBackRotationDegrees: 0,
            shortEdgeBackRotationDegrees: 180);

        Assert.Single(plan.BackPass);
        Assert.Equal(180, plan.BackPass[0].RotationDegrees);
    }

    [Fact]
    public void OnePage_HasNoBackPass()
    {
        var plan = Create(
            outputPages: 1,
            copies: 1,
            backOrder: ManualDuplexBackOrder.Forward);

        Assert.Single(plan.Sheets);
        Assert.Single(plan.FrontPass);
        Assert.Empty(plan.BackPass);
    }

    [Fact]
    public void OffMode_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            ManualDuplexPlanner.Create(
                2,
                1,
                DuplexMode.Off,
                ManualDuplexBackOrder.Forward,
                0,
                180));
    }

    private static ManualDuplexPlan Create(
        int outputPages,
        int copies,
        ManualDuplexBackOrder backOrder) =>
        ManualDuplexPlanner.Create(
            outputPages,
            copies,
            DuplexMode.LongEdge,
            backOrder,
            longEdgeBackRotationDegrees: 0,
            shortEdgeBackRotationDegrees: 180);
}
