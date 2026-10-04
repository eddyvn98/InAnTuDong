using PrintAI.Rendering;

namespace PrintAI.Tests;

public sealed class PrintableTextRendererTests
{
    [Fact]
    public void RenderA4Png_CreatesPrintablePng()
    {
        var bytes = PrintableTextRenderer.RenderA4Png(
            "Thông báo\nPrintAI prompt-only source");

        Assert.True(bytes.Length > 1000);
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'N', bytes[2]);
        Assert.Equal((byte)'G', bytes[3]);
    }

    [Fact]
    public void RenderA4Png_RejectsEmptyContent()
    {
        Assert.Throws<ArgumentException>(
            () => PrintableTextRenderer.RenderA4Png("   "));
    }
}
