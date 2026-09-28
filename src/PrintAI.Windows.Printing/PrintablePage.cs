namespace PrintAI.Windows.Printing;

public sealed record PrintablePage(
    string PngPath,
    int RotationDegrees = 0);
