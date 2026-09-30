using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PrintAI.Domain;

namespace PrintAI.Planning;

public static partial class SimpleA4MonochromePlanner
{
    private static readonly Regex ClarificationExchange = new(
        @"câu hỏi làm rõ của agy:.*?trả lời của người dùng:",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex CopyCount = new(
        @"\b\d+\s*(?:ban|copies|copy)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly HashSet<string> SimpleTerms = new(StringComparer.Ordinal)
    {
        "in", "print", "a4", "trang", "den", "black", "white", "and",
        "grayscale", "monochrome", "mot", "hai", "1", "2", "mat", "simplex",
        "single", "side", "sided", "double", "duplex", "giay", "kho", "tai",
        "lieu", "pdf", "anh", "hinh", "cho", "toi", "ban", "muon", "vui",
        "long", "hay", "giup", "lam", "on", "one", "two", "sides"
    };

    private static readonly Regex Word = new(
        @"[\p{L}\p{N}]+",
        RegexOptions.Compiled);

    public static bool TryPlan(
        PlanningRequest request,
        out PlanningOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(request);
        var text = Normalize(ClarificationExchange.Replace(request.UserRequest, " "));
        var hasA4 = Regex.IsMatch(text, @"\ba4\b", RegexOptions.CultureInvariant);
        var monochrome = text.Contains("trang den", StringComparison.Ordinal) ||
                         text.Contains("den trang", StringComparison.Ordinal) ||
                         text.Contains("grayscale", StringComparison.Ordinal) ||
                         text.Contains("monochrome", StringComparison.Ordinal);
        if (!hasA4 || !monochrome || request.Sources.Count is < 1 or > 100 ||
            request.Sources.Sum(source => source.PageCount ?? 1) > 20 ||
            IsComplexOrConflicting(text) ||
            Word.Matches(text).Any(match => !SimpleTerms.Contains(match.Value)))
        {
            outcome = null!;
            return false;
        }

        var oneSided = ContainsAny(text, "mot mat", "1 mat", "simplex", "single sided");
        var twoSided = ContainsAny(text, "hai mat", "2 mat", "duplex", "double sided");
        if (oneSided && twoSided)
        {
            outcome = null!;
            return false;
        }

        var sources = request.Sources.SelectMany(source =>
            Enumerable.Range(0, source.PageCount ?? 1)
                .Select(pageIndex => new SourceSpec(
                    source.Path,
                    Copies: 1,
                    PageIndex: pageIndex))).ToArray();
        var job = new PrintJobSpec(
            "A4 đen trắng",
            sources,
            new PaperSpec(),
            new LayoutSpec(
                LayoutMode.ExactSize,
                ItemWidthMm: 200,
                ItemHeightMm: 287,
                MarginMm: 5,
                AllowRotate: false,
                Fit: FitMode.Contain),
            new PrintSettings(
                ColorMode: ColorMode.Grayscale,
                Duplex: twoSided ? DuplexMode.LongEdge : DuplexMode.Off),
            new PolicySpec(PreviewPolicy.Required));

        outcome = new PlanningOutcome(
            job,
            Confidence: 1,
            Questions: oneSided || twoSided
                ? []
                : ["Bạn muốn in 1 mặt hay 2 mặt (duplex)?"],
            Warnings: [],
            RawJson: "");
        return true;
    }

    private static bool IsComplexOrConflicting(string text) =>
        text.Contains("khong", StringComparison.Ordinal) ||
        CopyCount.IsMatch(text) ||
        ContainsAny(text,
            "trang/to", "trang to", "n-up", "booklet", "gap", "can le", "le trai", "le phai",
            "crop", "cat", "xoay", "landscape", "portrait", "kho a3", "kho a5",
            "phong to", "thu nho", "phan tram", "poster", "mau", "cm", "mm");

    private static bool ContainsAny(string text, params string[] values) =>
        values.Any(value => text.Contains(value, StringComparison.Ordinal));

    private static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                result.Append(character);
        }
        return result.ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace('đ', 'd')
            .ToLowerInvariant();
    }
}
