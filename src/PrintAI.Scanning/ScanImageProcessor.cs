using SkiaSharp;

namespace PrintAI.Scanning;

public static class ScanImageProcessor
{
    public static ScanProcessResult Process(
        string inputPath,
        string outputPath,
        ScanProcessingSettings settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();

        using var source = SKBitmap.Decode(inputPath)
            ?? throw new InvalidDataException("Scanned image could not be decoded.");

        var deskew = settings.AutoDeskew
            ? EstimateDeskewDegrees(source, settings.MaxDeskewDegrees, settings.WhiteThreshold)
            : 0;

        using var rotated = Math.Abs(deskew) >= 0.15
            ? Rotate(source, deskew)
            : Clone(source);

        var bounds = settings.AutoCrop
            ? FindContentBounds(rotated, settings.WhiteThreshold)
            : null;

        using var final = bounds is SKRectI crop
            ? Crop(rotated, crop)
            : Clone(rotated);

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        using var image = SKImage.FromBitmap(final);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("Failed to encode processed scan.");

        File.WriteAllBytes(outputPath, data.ToArray());

        return new(
            outputPath,
            final.Width,
            final.Height,
            bounds is not null,
            deskew);
    }

    public static double EstimateDeskewDegrees(
        SKBitmap bitmap,
        double maxDegrees = 5,
        byte whiteThreshold = 238)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        if (maxDegrees <= 0 || bitmap.Width < 32 || bitmap.Height < 32)
            return 0;

        var step = Math.Max(2, Math.Max(bitmap.Width, bitmap.Height) / 500);
        var points = new List<(double X, double Y)>();

        for (var y = 0; y < bitmap.Height; y += step)
        {
            for (var x = 0; x < bitmap.Width; x += step)
            {
                if (IsContent(bitmap.GetPixel(x, y), whiteThreshold))
                    points.Add((x, y));
            }
        }

        if (points.Count < 100)
            return 0;

        var zeroScore = ProjectionScore(points, 0);
        var bestScore = zeroScore;
        var bestAngle = 0d;

        for (var angle = -maxDegrees; angle <= maxDegrees + 0.001; angle += 0.5)
        {
            if (Math.Abs(angle) < 0.001)
                continue;

            var score = ProjectionScore(points, angle);
            if (score > bestScore)
            {
                bestScore = score;
                bestAngle = angle;
            }
        }

        if (bestScore < zeroScore * 1.02)
            return 0;

        return Math.Round(bestAngle, 2);
    }

    private static double ProjectionScore(
        IReadOnlyList<(double X, double Y)> points,
        double correctionDegrees)
    {
        var radians = correctionDegrees * Math.PI / 180d;
        var sin = Math.Sin(radians);
        var cos = Math.Cos(radians);
        var min = double.MaxValue;
        var projected = new double[points.Count];

        for (var i = 0; i < points.Count; i++)
        {
            var value = points[i].X * sin + points[i].Y * cos;
            projected[i] = value;
            if (value < min)
                min = value;
        }

        const double bucketSize = 3;
        var buckets = new Dictionary<int, int>();

        foreach (var value in projected)
        {
            var bucket = (int)Math.Round((value - min) / bucketSize);
            buckets.TryGetValue(bucket, out var count);
            buckets[bucket] = count + 1;
        }

        double score = 0;
        foreach (var count in buckets.Values)
            score += (double)count * count;

        return score;
    }

    private static SKRectI? FindContentBounds(
        SKBitmap bitmap,
        byte whiteThreshold)
    {
        var sampleStep = Math.Max(1, Math.Max(bitmap.Width, bitmap.Height) / 1400);
        var minX = bitmap.Width;
        var minY = bitmap.Height;
        var maxX = -1;
        var maxY = -1;

        for (var y = 0; y < bitmap.Height; y += sampleStep)
        {
            for (var x = 0; x < bitmap.Width; x += sampleStep)
            {
                if (!IsContent(bitmap.GetPixel(x, y), whiteThreshold))
                    continue;

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY)
            return null;

        var width = maxX - minX + 1;
        var height = maxY - minY + 1;
        var areaRatio = (double)width * height / (bitmap.Width * bitmap.Height);

        if (areaRatio < 0.05)
            return null;

        var padding = Math.Max(8, Math.Min(bitmap.Width, bitmap.Height) / 100);
        minX = Math.Max(0, minX - padding);
        minY = Math.Max(0, minY - padding);
        maxX = Math.Min(bitmap.Width - 1, maxX + padding);
        maxY = Math.Min(bitmap.Height - 1, maxY + padding);

        if (minX == 0 && minY == 0 &&
            maxX == bitmap.Width - 1 && maxY == bitmap.Height - 1)
        {
            return null;
        }

        return new SKRectI(minX, minY, maxX + 1, maxY + 1);
    }

    private static bool IsContent(SKColor color, byte whiteThreshold)
    {
        if (color.Alpha < 32)
            return false;

        return color.Red < whiteThreshold ||
               color.Green < whiteThreshold ||
               color.Blue < whiteThreshold;
    }

    private static SKBitmap Rotate(SKBitmap source, double degrees)
    {
        var radians = Math.Abs(degrees) * Math.PI / 180d;
        var width = (int)Math.Ceiling(
            source.Width * Math.Cos(radians) +
            source.Height * Math.Sin(radians));
        var height = (int)Math.Ceiling(
            source.Width * Math.Sin(radians) +
            source.Height * Math.Cos(radians));

        var result = new SKBitmap(
            Math.Max(1, width),
            Math.Max(1, height),
            source.ColorType,
            source.AlphaType);

        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.White);
        canvas.Translate(result.Width / 2f, result.Height / 2f);
        canvas.RotateDegrees((float)degrees);
        canvas.Translate(-source.Width / 2f, -source.Height / 2f);
        var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);
        canvas.DrawBitmap(
            source,
            new SKRect(0, 0, source.Width, source.Height),
            new SKRect(0, 0, source.Width, source.Height),
            sampling);
        canvas.Flush();

        return result;
    }

    private static SKBitmap Crop(SKBitmap source, SKRectI bounds)
    {
        var result = new SKBitmap(
            bounds.Width,
            bounds.Height,
            source.ColorType,
            source.AlphaType);

        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.White);
        var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);
        canvas.DrawBitmap(
            source,
            bounds,
            new SKRect(0, 0, bounds.Width, bounds.Height),
            sampling);
        canvas.Flush();

        return result;
    }

    private static SKBitmap Clone(SKBitmap source)
    {
        var result = new SKBitmap(
            source.Width,
            source.Height,
            source.ColorType,
            source.AlphaType);

        using var canvas = new SKCanvas(result);
        var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);
        canvas.DrawBitmap(
            source,
            new SKRect(0, 0, source.Width, source.Height),
            new SKRect(0, 0, source.Width, source.Height),
            sampling);
        canvas.Flush();

        return result;
    }
}
