using Oreja.Infrastructure;
using Oreja.Tests.Fakes;

namespace Oreja.Tests;

public sealed class AutostartTests : IDisposable
{
    private readonly TestRegistryKey _registry = new();

    public void Dispose() => _registry.Dispose();

    [Fact]
    public void EnableWritesQuotedExePath()
    {
        var autostart = new Autostart(_registry.Key, "Oreja", @"C:\Tools\Oreja\Oreja.exe");

        autostart.Enable();

        Assert.True(autostart.IsEnabled);
        Assert.Equal("\"C:\\Tools\\Oreja\\Oreja.exe\"", _registry.Key.GetValue("Oreja"));
    }

    [Fact]
    public void DisableRemovesEntry()
    {
        var autostart = new Autostart(_registry.Key, "Oreja", @"C:\Tools\Oreja\Oreja.exe");
        autostart.Enable();

        autostart.Disable();
        autostart.Disable(); // zweimal ist harmlos

        Assert.False(autostart.IsEnabled);
        Assert.Null(_registry.Key.GetValue("Oreja"));
    }

    [Fact]
    public void RepairPathUpdatesMovedExe()
    {
        new Autostart(_registry.Key, "Oreja", @"C:\Alt\Oreja.exe").Enable();

        new Autostart(_registry.Key, "Oreja", @"C:\Neu\Oreja.exe").RepairPath();

        Assert.Equal("\"C:\\Neu\\Oreja.exe\"", _registry.Key.GetValue("Oreja"));
    }

    [Fact]
    public void RepairPathDoesNotEnableDisabledAutostart()
    {
        var autostart = new Autostart(_registry.Key, "Oreja", @"C:\Neu\Oreja.exe");

        autostart.RepairPath();

        Assert.False(autostart.IsEnabled);
    }
}
