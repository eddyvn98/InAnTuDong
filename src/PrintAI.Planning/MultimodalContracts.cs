namespace PrintAI.Planning;

public sealed record MultimodalImage(
    int SourceIndex,
    string DataUrl,
    int? PixelWidth = null,
    int? PixelHeight = null);

public sealed record MultimodalModelRequest(
    string SystemInstruction,
    string UserText,
    IReadOnlyList<MultimodalImage> Images);

public interface IMultimodalModelClient
{
    Task<string> CompleteMultimodalAsync(
        MultimodalModelRequest request,
        CancellationToken cancellationToken = default);
}
