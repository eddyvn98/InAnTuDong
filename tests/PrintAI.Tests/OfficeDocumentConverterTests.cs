using PrintAI.DocumentConversion;
using Xunit;

namespace PrintAI.Tests;

public sealed class OfficeDocumentConverterTests
{
    [Theory]
    [InlineData("a.doc")]
    [InlineData("a.docx")]
    [InlineData("a.xls")]
    [InlineData("a.xlsx")]
    [InlineData("a.ppt")]
    [InlineData("a.pptx")]
    [InlineData("A.DOCX")]
    public void IsSupported_AcceptsExpectedOfficeExtensions(string path)
    {
        Assert.True(OfficeDocumentConverter.IsSupported(path));
    }

    [Fact]
    public void IsSupported_RejectsPdf()
    {
        Assert.False(OfficeDocumentConverter.IsSupported("a.pdf"));
    }

    [Fact]
    public void GetOutputPdfPath_UsesSourceBaseName()
    {
        var result = OfficeDocumentConverter.GetOutputPdfPath(
            Path.Combine("input", "proposal.docx"),
            Path.Combine("work", "converted"));

        Assert.Equal(
            Path.Combine("work", "converted", "proposal.pdf"),
            result);
    }

    [Fact]
    public void ConvertToPdf_RejectsUnsupportedExtensionBeforeLaunchingConverter()
    {
        var input = Path.GetTempFileName();

        try
        {
            Assert.Throws<NotSupportedException>(() =>
                OfficeDocumentConverter.ConvertToPdf(
                    input,
                    Path.GetTempPath(),
                    executablePath: "missing-soffice"));
        }
        finally
        {
            File.Delete(input);
        }
    }
}
