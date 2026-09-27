using PrintAI.Domain;

namespace PrintAI.Layout;

public readonly record struct NormalizedRect(
    double X,
    double Y,
    double Width,
    double Height);

public sealed record ContentFitGeometry(
    NormalizedRect Source,
    NormalizedRect Destination);

public static class ContentFitCalculator
{
    public static ContentFitGeometry Calculate(
        double sourceWidth,
        double sourceHeight,
        double targetWidth,
        double targetHeight,
        FitMode mode)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(sourceWidth), "Source dimensions must be positive.");

        if (targetWidth <= 0 || targetHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetWidth), "Target dimensions must be positive.");

        return mode switch
        {
            FitMode.Contain => Contain(sourceWidth, sourceHeight, targetWidth, targetHeight),
            FitMode.Cover => Cover(sourceWidth, sourceHeight, targetWidth, targetHeight),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }

    private static ContentFitGeometry Contain(
        double sourceWidth,
        double sourceHeight,
        double targetWidth,
        double targetHeight)
    {
        var scale = Math.Min(targetWidth / sourceWidth, targetHeight / sourceHeight);
        var width = sourceWidth * scale;
        var height = sourceHeight * scale;

        return new(
            Source: new NormalizedRect(0, 0, 1, 1),
            Destination: new NormalizedRect(
                (targetWidth - width) / (2 * targetWidth),
                (targetHeight - height) / (2 * targetHeight),
                width / targetWidth,
                height / targetHeight));
    }

    private static ContentFitGeometry Cover(
        double sourceWidth,
        double sourceHeight,
        double targetWidth,
        double targetHeight)
    {
        var scale = Math.Max(targetWidth / sourceWidth, targetHeight / sourceHeight);
        var visibleSourceWidth = targetWidth / scale;
        var visibleSourceHeight = targetHeight / scale;

        return new(
            Source: new NormalizedRect(
                (sourceWidth - visibleSourceWidth) / (2 * sourceWidth),
                (sourceHeight - visibleSourceHeight) / (2 * sourceHeight),
                visibleSourceWidth / sourceWidth,
                visibleSourceHeight / sourceHeight),
            Destination: new NormalizedRect(0, 0, 1, 1));
    }
}
