using PrintAI.Layout;
using Xunit;

namespace PrintAI.Tests;

public sealed class CutMarkGeneratorTests
{
    [Fact]
    public void CreatesEightMarksAroundPlacement()
    {
        var placement = new Placement(
            Index: 0,
            Page: 0,
            XMm: 10,
            YMm: 20,
            WidthMm: 40,
            HeightMm: 60,
            Rotated: false);

        var marks = CutMarkGenerator.Create(placement);

        Assert.Equal(8, marks.Count);
        Assert.Contains(marks, m => m.Y1Mm == 20 && m.Y2Mm == 20 && m.X2Mm < 10);
        Assert.Contains(marks, m => m.X1Mm == 50 && m.X2Mm == 50 && m.Y1Mm > 80);
    }
}
