using System.Runtime.InteropServices;
using PrintAI.Scanning;

namespace PrintAI.Windows.Scanning;

public sealed class WiaScannerAdapter : IScannerAdapter
{
    public Task<IReadOnlyList<ScannerDevice>> EnumerateAsync(
        CancellationToken cancellationToken = default) =>
        StaThreadRunner.RunAsync(
            EnumerateCore,
            cancellationToken);

    public Task<ScanResult> ScanAsync(
        ScanRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Validate(request);

        return StaThreadRunner.RunAsync(
            () => ScanCore(request),
            cancellationToken);
    }

    private static IReadOnlyList<ScannerDevice> EnumerateCore()
    {
        object? manager = null;

        try
        {
            manager = WiaCom.Create("WIA.DeviceManager");
            dynamic dynamicManager = manager;

            var result = new List<ScannerDevice>();

            foreach (dynamic info in dynamicManager.DeviceInfos)
            {
                try
                {
                    if ((int)info.Type != WiaConstants.ScannerDeviceType)
                        continue;

                    var id = (string)info.DeviceID;
                    var name = ReadName(info) ?? id;

                    result.Add(new(
                        id,
                        name,
                        Adapter: "WIA"));
                }
                finally
                {
                    WiaCom.Release(info);
                }
            }

            return result;
        }
        catch (InvalidOperationException)
        {
            return [];
        }
        catch (COMException ex)
        {
            throw new ScannerUnavailableException(
                "Windows WIA could not enumerate scanners.",
                ex);
        }
        finally
        {
            WiaCom.Release(manager);
        }
    }

    private static ScanResult ScanCore(ScanRequest request)
    {
        Directory.CreateDirectory(request.OutputDirectory);

        object? manager = null;
        object? deviceInfo = null;
        object? device = null;
        object? item = null;
        object? image = null;

        try
        {
            try
            {
                manager = WiaCom.Create("WIA.DeviceManager");
            }
            catch (InvalidOperationException ex)
            {
                throw new ScannerUnavailableException(
                    "Windows WIA is unavailable.",
                    ex);
            }

            dynamic dynamicManager = manager;

            deviceInfo = FindDeviceInfo(
                dynamicManager,
                request.DeviceId);

            if (deviceInfo is null)
            {
                throw new ScannerUnavailableException(
                    request.DeviceId is null
                        ? "No WIA scanner is installed."
                        : $"WIA scanner '{request.DeviceId}' was not found.");
            }

            dynamic dynamicInfo = deviceInfo;
            var scanner = new ScannerDevice(
                (string)dynamicInfo.DeviceID,
                ReadName(dynamicInfo) ??
                (string)dynamicInfo.DeviceID,
                "WIA");

            device = dynamicInfo.Connect();
            dynamic dynamicDevice = device;

            if ((int)dynamicDevice.Items.Count < 1)
            {
                throw new ScannerUnavailableException(
                    "The selected WIA scanner exposes no scan item.");
            }

            item = dynamicDevice.Items[1];
            ConfigureItem(item, request);

            dynamic dynamicItem = item;
            image = dynamicItem.Transfer(
                WiaConstants.PngFormatId);

            var outputPath = CreateOutputPath(
                request.OutputDirectory);

            if (File.Exists(outputPath))
                File.Delete(outputPath);

            dynamic dynamicImage = image;
            dynamicImage.SaveFile(outputPath);

            return new(
                outputPath,
                scanner,
                request.Dpi,
                request.ColorMode);
        }
        catch (COMException ex)
        {
            throw new ScanAcquisitionException(
                "Windows WIA scan failed.",
                ex);
        }
        finally
        {
            WiaCom.Release(image);
            WiaCom.Release(item);
            WiaCom.Release(device);
            WiaCom.Release(deviceInfo);
            WiaCom.Release(manager);
        }
    }

    private static object? FindDeviceInfo(
        dynamic manager,
        string? deviceId)
    {
        foreach (dynamic info in manager.DeviceInfos)
        {
            if ((int)info.Type != WiaConstants.ScannerDeviceType)
            {
                WiaCom.Release(info);
                continue;
            }

            var id = (string)info.DeviceID;
            if (deviceId is null ||
                id.Equals(
                    deviceId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return info;
            }

            WiaCom.Release(info);
        }

        return null;
    }

    private static string? ReadName(dynamic info)
    {
        try
        {
            foreach (dynamic property in info.Properties)
            {
                try
                {
                    if (string.Equals(
                        (string)property.Name,
                        "Name",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return property.Value?.ToString();
                    }
                }
                finally
                {
                    WiaCom.Release(property);
                }
            }
        }
        catch (COMException)
        {
        }

        return null;
    }

    private static void ConfigureItem(
        object item,
        ScanRequest request)
    {
        dynamic dynamicItem = item;

        TrySetProperty(
            dynamicItem.Properties,
            WiaConstants.HorizontalResolutionPropertyId,
            request.Dpi);

        TrySetProperty(
            dynamicItem.Properties,
            WiaConstants.VerticalResolutionPropertyId,
            request.Dpi);

        TrySetProperty(
            dynamicItem.Properties,
            WiaConstants.CurrentIntentPropertyId,
            ToIntent(request.ColorMode));
    }

    private static void TrySetProperty(
        dynamic properties,
        int propertyId,
        object value)
    {
        foreach (dynamic property in properties)
        {
            try
            {
                if ((int)property.PropertyID == propertyId)
                {
                    property.Value = value;
                    return;
                }
            }
            catch (COMException)
            {
                return;
            }
            finally
            {
                WiaCom.Release(property);
            }
        }
    }

    private static int ToIntent(ScanColorMode colorMode) =>
        colorMode switch
        {
            ScanColorMode.Color => WiaConstants.IntentColor,
            ScanColorMode.Grayscale => WiaConstants.IntentGrayscale,
            ScanColorMode.BlackAndWhite => WiaConstants.IntentText,
            _ => throw new ArgumentOutOfRangeException(
                nameof(colorMode))
        };

    private static string CreateOutputPath(
        string directory) =>
        Path.Combine(
            directory,
            $"scan-{DateTime.Now:yyyyMMdd-HHmmss-fff}.png");

    private static void Validate(ScanRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            request.OutputDirectory);

        if (request.Dpi is < 75 or > 1200)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.Dpi),
                "Scan DPI must be between 75 and 1200.");
        }
    }
}

public sealed class ScannerUnavailableException : Exception
{
    public ScannerUnavailableException(string message)
        : base(message)
    {
    }

    public ScannerUnavailableException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class ScanAcquisitionException : Exception
{
    public ScanAcquisitionException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }
}
