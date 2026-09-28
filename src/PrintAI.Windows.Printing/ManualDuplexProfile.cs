using PrintAI.Domain;

namespace PrintAI.Windows.Printing;

public sealed record ManualDuplexProfile(
    bool IsVerified,
    ManualDuplexBackOrder BackOrder,
    int LongEdgeBackRotationDegrees,
    int ShortEdgeBackRotationDegrees,
    string ReinsertInstruction)
{
    public static ManualDuplexProfile UnverifiedDefault { get; } =
        new(
            IsVerified: false,
            BackOrder: ManualDuplexBackOrder.Reverse,
            LongEdgeBackRotationDegrees: 0,
            ShortEdgeBackRotationDegrees: 180,
            ReinsertInstruction:
                "Giữ nguyên thứ tự xấp giấy. Đặt lại xấp theo hướng nạp giấy của máy in. " +
                "Cấu hình manual duplex này chưa được xác nhận bằng bản in thật; nên thử với 2 trang trước.");
}
