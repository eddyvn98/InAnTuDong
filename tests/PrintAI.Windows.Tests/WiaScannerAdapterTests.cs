using PrintAI.Scanning;
using PrintAI.Windows.Scanning;
using Xunit;

namespace PrintAI.Windows.Tests;

public sealed class WiaScannerAdapterTests
{
    [Fact]
    public async Task EnumerateAsync_IsSafeWhenRunnerHasNoScanner()
    {
        var adapter = new WiaScannerAdapter();

        var devices = await adapter.EnumerateAsync();

        Assert.NotNull(devices);
        Assert.All(devices, device =>
        {
            Assert.False(string.IsNullOrWhiteSpace(device.Id));
            Assert.False(string.IsNullOrWhiteSpace(device.Name));
            Assert.Equal("WIA", device.Adapter);
        });
    }

    [Fact]
    public async Task ScanAsync_RejectsInvalidDpiBeforeHardwareAccess()
    {
        var adapter = new WiaScannerAdapter();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            adapter.ScanAsync(
                new ScanRequest(
                    Path.GetTempPath(),
                    Dpi: 50)));
    }
}
