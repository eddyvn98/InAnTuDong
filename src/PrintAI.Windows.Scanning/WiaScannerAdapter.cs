using PrintAI.Scanning;

namespace PrintAI.Windows.Scanning;

public sealed class WiaScannerAdapter : IScannerAdapter
{
    private const int ScannerDeviceType = 1;
    private const int WiaPropertyDeviceId = 2;
    private const int WiaPropertyDeviceName = 7;
    private const int WiaHorizontalResolution = 6147;
    private const int WiaVerticalResolution = 6148;
    private const int WiaCurrentIntent = 6146;

    private const int IntentColor = 1;
    private const int IntentGrayscale = 2;
    private const int IntentText = 4;

    private const string WiaPngFormat =
        "{B96B3CAF-0728-11D3-9D7B-0000F81EF32E}";

    public IReadOnlyList<ScannerDevice> Enumerate()
    {
        EnsureWindows();

        dynamic manager = CreateCom("WIA.DeviceManager");
        var result = new List<ScannerDevice>();

        foreach (dynamic info in manager.DeviceInfos)
        {
            if ((int)info.Type != ScannerDeviceType)
                continue;

            var id = ReadProperty(info.Properties, WiaPropertyDeviceId)
                ?? throw new InvalidOperationException("WIA scanner has no device id.");

            var name = ReadProperty(info.Properties, WiaPropertyDeviceName)
                ?? id;

            result.Add(new(id, name));
        }

        return result;
    }

    public Task<string> ScanPageAsync(
        string scannerId,
        ScanCaptureSettings settings,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scannerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        EnsureWindows();
        cancellationToken.ThrowIfCancellationRequested();

        return RunStaAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            dynamic manager = CreateCom("WIA.DeviceManager");
            dynamic? selected = null;

            foreach (dynamic info in manager.DeviceInfos)
            {
                if ((int)info.Type != ScannerDeviceType)
                    continue;

                var id = ReadProperty(info.Properties, WiaPropertyDeviceId);
                if (string.Equals(id, scannerId, StringComparison.OrdinalIgnoreCase))
                {
                    selected = info;
                    break;
                }
            }

            if (selected is null)
                throw new InvalidOperationException("Selected WIA scanner is no longer available.");

            dynamic device = selected.Connect();
            dynamic item = device.Items[1];

            TrySetProperty(item.Properties, WiaHorizontalResolution, settings.Dpi);
            TrySetProperty(item.Properties, WiaVerticalResolution, settings.Dpi);
            TrySetProperty(
                item.Properties,
                WiaCurrentIntent,
                settings.ColorMode switch
                {
                    ScanColorMode.Grayscale => IntentGrayscale,
                    ScanColorMode.BlackAndWhite => IntentText,
                    _ => IntentColor
                });

            dynamic image = item.Transfer(WiaPngFormat);

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            if (File.Exists(outputPath))
                File.Delete(outputPath);

            image.SaveFile(outputPath);
            return outputPath;
        }, cancellationToken);
    }

    private static Task<T> RunStaAsync<T>(
        Func<T> action,
        CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<T>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var thread = new Thread(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                completion.TrySetResult(action());
            }
            catch (OperationCanceledException ex)
            {
                completion.TrySetCanceled(ex.CancellationToken);
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = "PrintAI-WIA"
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return completion.Task;
    }

    private static dynamic CreateCom(string progId)
    {
        var type = Type.GetTypeFromProgID(progId, throwOnError: false)
            ?? throw new PlatformNotSupportedException(
                "Windows Image Acquisition (WIA) is not available on this Windows installation.");

        return Activator.CreateInstance(type)
            ?? throw new InvalidOperationException($"Could not create COM object {progId}.");
    }

    private static string? ReadProperty(dynamic properties, int propertyId)
    {
        foreach (dynamic property in properties)
        {
            if ((int)property.PropertyID == propertyId)
                return Convert.ToString(property.Value);
        }

        return null;
    }

    private static void TrySetProperty(
        dynamic properties,
        int propertyId,
        object value)
    {
        foreach (dynamic property in properties)
        {
            if ((int)property.PropertyID != propertyId)
                continue;

            try
            {
                property.Value = value;
            }
            catch
            {
                // Some drivers expose a property but reject unsupported values.
                // The driver default remains in effect and scan can still proceed.
            }

            return;
        }
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "WIA scanning is available only on Windows.");
        }
    }
}
