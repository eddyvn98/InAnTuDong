using System.Runtime.InteropServices;

namespace PrintAI.Windows.Scanning;

internal static class WiaCom
{
    public static object Create(string progId)
    {
        var type = Type.GetTypeFromProgID(
            progId,
            throwOnError: false);

        if (type is null)
        {
            throw new InvalidOperationException(
                $"Windows WIA component '{progId}' is unavailable.");
        }

        return Activator.CreateInstance(type)
            ?? throw new InvalidOperationException(
                $"Windows WIA component '{progId}' could not be created.");
    }

    public static void Release(object? value)
    {
        if (value is null || !Marshal.IsComObject(value))
            return;

        try
        {
            Marshal.FinalReleaseComObject(value);
        }
        catch (InvalidComObjectException)
        {
        }
    }
}
