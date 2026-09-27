namespace PrintAI.ReleaseReadiness;

public enum ReadinessState
{
    Pass,
    Warning,
    Fail
}

public sealed record ReleaseCheck(
    string Id,
    string Name,
    ReadinessState State,
    string Detail);

public sealed record ReleaseReadinessInput(
    bool WorkDirectoryWritable,
    int PrinterCount,
    bool SelectedPrinterVerified,
    int ScannerCount,
    bool LibreOfficeAvailable,
    bool AiPlannerConfigured);

public sealed record ReleaseReadinessReport(
    IReadOnlyList<ReleaseCheck> Checks)
{
    public bool ReadyForCorePrinting =>
        Checks.All(check => check.State != ReadinessState.Fail);

    public int PassCount =>
        Checks.Count(check => check.State == ReadinessState.Pass);

    public int WarningCount =>
        Checks.Count(check => check.State == ReadinessState.Warning);

    public int FailureCount =>
        Checks.Count(check => check.State == ReadinessState.Fail);
}

public static class ReleaseReadinessEvaluator
{
    public static ReleaseReadinessReport Evaluate(
        ReleaseReadinessInput input)
    {
        var checks = new List<ReleaseCheck>
        {
            input.WorkDirectoryWritable
                ? Pass("storage", "Thư mục làm việc", "Có thể ghi file preview/conversion.")
                : Fail("storage", "Thư mục làm việc", "Không thể ghi vào thư mục làm việc của PrintAI."),

            input.PrinterCount > 0
                ? Pass("printer", "Máy in Windows", $"Tìm thấy {input.PrinterCount} máy in.")
                : Fail("printer", "Máy in Windows", "Không tìm thấy máy in Windows hợp lệ."),

            input.PrinterCount == 0
                ? Warning("profile", "Hiệu chuẩn máy in", "Chưa thể kiểm tra profile vì không có máy in.")
                : input.SelectedPrinterVerified
                    ? Pass("profile", "Hiệu chuẩn máy in", "Profile máy in đang chọn đã được xác minh vật lý.")
                    : Warning("profile", "Hiệu chuẩn máy in", "Profile máy in đang chọn chưa được xác minh vật lý."),

            input.ScannerCount > 0
                ? Pass("scanner", "Scanner WIA", $"Tìm thấy {input.ScannerCount} scanner.")
                : Warning("scanner", "Scanner WIA", "Không có scanner WIA; chức năng in vẫn hoạt động."),

            input.LibreOfficeAvailable
                ? Pass("office", "Office conversion", "LibreOffice/soffice khả dụng.")
                : Warning("office", "Office conversion", "Chưa có LibreOffice; DOC/XLS/PPT sẽ không import được."),

            input.AiPlannerConfigured
                ? Pass("ai", "AI planner", "AI planner đã được cấu hình.")
                : Warning("ai", "AI planner", "AI chưa cấu hình; manual preview/print vẫn hoạt động.")
        };

        return new(checks);
    }

    private static ReleaseCheck Pass(
        string id,
        string name,
        string detail) =>
        new(id, name, ReadinessState.Pass, detail);

    private static ReleaseCheck Warning(
        string id,
        string name,
        string detail) =>
        new(id, name, ReadinessState.Warning, detail);

    private static ReleaseCheck Fail(
        string id,
        string name,
        string detail) =>
        new(id, name, ReadinessState.Fail, detail);
}
