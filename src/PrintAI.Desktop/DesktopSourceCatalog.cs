using System.IO;
using PrintAI.SourceInspection;

namespace PrintAI.Desktop;

public static class DesktopSourceCatalog
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".heic",
            ".heif",
            ".pdf"
        };

    public static IReadOnlyList<DesktopPage> BuildPages(
        IEnumerable<string> paths)
    {
        var pages = new List<DesktopPage>();

        foreach (var path in paths)
        {
            try
            {
                var metadata = SourceInspector.Inspect(path);
                var count = metadata.Kind == SourceKind.Pdf
                    ? metadata.PageCount ?? 0
                    : 1;

                for (var sourcePage = 0;
                     sourcePage < count;
                     sourcePage++)
                {
                    pages.Add(new(
                        pages.Count,
                        path,
                        Path.GetFileName(path),
                        sourcePage,
                        metadata.Kind == SourceKind.Pdf
                            ? $"Trang {sourcePage + 1}/{count}"
                            : "Ảnh"));
                }
            }
            catch
            {
            }
        }

        return pages;
    }

    public static DesktopFile InspectSafe(string path)
    {
        try
        {
            var metadata = SourceInspector.Inspect(path);

            return new(
                Path.GetFileName(path),
                path,
                metadata.Kind.ToString(),
                metadata.PixelWidth,
                metadata.PixelHeight,
                metadata.PageCount,
                null);
        }
        catch (Exception ex)
        {
            return new(
                Path.GetFileName(path),
                path,
                "Error",
                null,
                null,
                null,
                ex.Message);
        }
    }

    public static IEnumerable<string> Expand(
        IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (File.Exists(path) && Supported(path))
            {
                yield return Path.GetFullPath(path);
                continue;
            }

            if (!Directory.Exists(path))
                continue;

            foreach (var file in Directory.EnumerateFiles(
                path,
                "*.*",
                SearchOption.TopDirectoryOnly))
            {
                if (Supported(file))
                    yield return Path.GetFullPath(file);
            }
        }
    }

    private static bool Supported(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path));
}
