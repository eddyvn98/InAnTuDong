using SkiaSharp;

namespace PrintAI.Web;

public static class DemoSourceFactory
{
    public static SKBitmap Create()
    {
        var bitmap = new SKBitmap(600, 900);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(new SKColor(245, 238, 223));

        using var paint = new SKPaint { IsAntialias = true };
        paint.Color = new SKColor(40, 70, 120);
        canvas.DrawRect(0, 0, 600, 300, paint);

        paint.Color = new SKColor(226, 116, 92);
        canvas.DrawCircle(300, 450, 190, paint);

        paint.Color = new SKColor(55, 135, 105);
        canvas.DrawRect(80, 680, 440, 130, paint);

        return bitmap;
    }
}
