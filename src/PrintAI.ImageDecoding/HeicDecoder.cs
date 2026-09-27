using PhotoSauce.MagicScaler;
using PhotoSauce.NativeCodecs.Libheif;
using PhotoSauce.NativeCodecs.Libpng;

namespace PrintAI.ImageDecoding;

public static class HeicDecoder
{
    private static readonly object Sync = new();
    private static bool _configured;

    public static byte[] DecodeToPng(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
            throw new FileNotFoundException("HEIC source file does not exist.", path);

        EnsureConfigured();

        using var output = new MemoryStream();
        var settings = new ProcessImageSettings();
        if (!settings.TrySetEncoderFormat(ImageMimeTypes.Png))
            throw new InvalidOperationException("PNG encoder is not available.");

        MagicImageProcessor.ProcessImage(path, output, settings);
        return output.ToArray();
    }

    private static void EnsureConfigured()
    {
        if (_configured)
            return;

        lock (Sync)
        {
            if (_configured)
                return;

            CodecManager.Configure(codecs =>
            {
                codecs.UseLibheif();
                codecs.UseLibpng();
            });

            _configured = true;
        }
    }
}
