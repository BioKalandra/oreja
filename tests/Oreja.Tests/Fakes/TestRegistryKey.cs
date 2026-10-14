using Microsoft.Win32;

namespace Oreja.Tests.Fakes;

/// <summary>Wegwerf-Schlüssel unter HKCU\Software\Oreja.Tests, damit Tests nie echte Einstellungen anfassen.</summary>
public sealed class TestRegistryKey : IDisposable
{
    private readonly string _path = $@"Software\Oreja.Tests\{Guid.NewGuid():N}";

    public TestRegistryKey() => Key = Registry.CurrentUser.CreateSubKey(_path);

    public RegistryKey Key { get; }

    public void Dispose()
    {
        Key.Dispose();
        Registry.CurrentUser.DeleteSubKeyTree(_path, throwOnMissingSubKey: false);
    }
}
