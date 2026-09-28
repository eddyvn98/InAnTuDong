using PrintAI.Domain;

namespace PrintAI.Layout;

public static class PhysicalScaleCalculator
{
    public static ContentFitGeometry Calculate(
        double sourcePixelWidth,
        double sourcePixelHeight,
        double targetWidthPx,
        double targetHeightPx,
        double targetWidthMm,
        double targetHeightMm,
        double? sourceWidthMm,
        double? sourceHeightMm,
        PhysicalScaleSpec scaling)
    {
        ArgumentNullException.ThrowIfNull(scaling);

        if (targetWidthMm <= 0 || targetHeightMm <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetWidthMm));

        return scaling.Mode switch
        {
            PhysicalScaleMode.MaxFit =>
                ContentFitCalculator.Calculate(
                    sourcePixelWidth,
                    sourcePixelHeight,
                    targetWidthPx,
                    targetHeightPx,
                    FitMode.Contain),

            PhysicalScaleMode.ShrinkOnly =>
                FromPhysicalSize(
                    targetWidthMm,
                    targetHeightMm,
                    RequirePhysical(sourceWidthMm, nameof(sourceWidthMm)),
                    RequirePhysical(sourceHeightMm, nameof(sourceHeightMm)),
                    factor: Math.Min(
                        1d,
                        Math.Min(
                            targetWidthMm / sourceWidthMm!.Value,
                            targetHeightMm / sourceHeightMm!.Value))),

            PhysicalScaleMode.Percent =>
                FromPhysicalSize(
                    targetWidthMm,
                    targetHeightMm,
                    RequirePhysical(sourceWidthMm, nameof(sourceWidthMm)),
                    RequirePhysical(sourceHeightMm, nameof(sourceHeightMm)),
                    factor: scaling.Percent / 100d),

            _ => throw new ArgumentOutOfRangeException(nameof(scaling.Mode))
        };
    }

    private static ContentFitGeometry FromPhysicalSize(
        double targetWidthMm,
        double targetHeightMm,
        double sourceWidthMm,
        double sourceHeightMm,
        double factor)
    {
        if (!double.IsFinite(factor) || factor <= 0)
            throw new ArgumentOutOfRangeException(nameof(factor));

        var width = sourceWidthMm * factor / targetWidthMm;
        var height = sourceHeightMm * factor / targetHeightMm;

        return new(
            Source: new NormalizedRect(0, 0, 1, 1),
            Destination: new NormalizedRect(
                X: (1 - width) / 2,
                Y: (1 - height) / 2,
                Width: width,
                Height: height));
    }

    private static double RequirePhysical(
        double? value,
        string parameterName)
    {
        if (value is null ||
            !double.IsFinite(value.Value) ||
            value.Value <= 0)
        {
            throw new ArgumentException(
                "Physical scaling requires trusted positive source dimensions in millimetres.",
                parameterName);
        }

        return value.Value;
    }
}
