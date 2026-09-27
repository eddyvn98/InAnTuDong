namespace PrintAI.Layout;

public readonly record struct CutMark(
    double X1Mm,
    double Y1Mm,
    double X2Mm,
    double Y2Mm);

public static class CutMarkGenerator
{
    public static IReadOnlyList<CutMark> Create(
        Placement placement,
        double lengthMm = 2,
        double offsetMm = 0.5)
    {
        if (lengthMm <= 0)
            throw new ArgumentOutOfRangeException(nameof(lengthMm));

        if (offsetMm < 0)
            throw new ArgumentOutOfRangeException(nameof(offsetMm));

        var left = placement.XMm;
        var top = placement.YMm;
        var right = left + placement.WidthMm;
        var bottom = top + placement.HeightMm;

        return
        [
            new(left - offsetMm - lengthMm, top, left - offsetMm, top),
            new(left, top - offsetMm - lengthMm, left, top - offsetMm),

            new(right + offsetMm, top, right + offsetMm + lengthMm, top),
            new(right, top - offsetMm - lengthMm, right, top - offsetMm),

            new(left - offsetMm - lengthMm, bottom, left - offsetMm, bottom),
            new(left, bottom + offsetMm, left, bottom + offsetMm + lengthMm),

            new(right + offsetMm, bottom, right + offsetMm + lengthMm, bottom),
            new(right, bottom + offsetMm, right, bottom + offsetMm + lengthMm)
        ];
    }
}
