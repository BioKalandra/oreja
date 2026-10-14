using Microsoft.Win32;

namespace Oreja.Infrastructure;

/// <summary>Dauerhafte Einstellungen unter HKCU\Software\Oreja.</summary>
public sealed class Settings
{
    private const string OutputFolderValue = "OutputFolder";
    private const string InitializedValue = "Initialized";

    private readonly RegistryKey _key;

    public Settings(RegistryKey key) => _key = key;

    public static string DefaultOutputFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Oreja");

    public string OutputFolder
    {
        get => _key.GetValue(OutputFolderValue) is string folder && folder.Length > 0 ? folder : DefaultOutputFolder;
        set => _key.SetValue(OutputFolderValue, value, RegistryValueKind.String);
    }

    public bool IsInitialized => _key.GetValue(InitializedValue) is int value && value == 1;

    public void MarkInitialized() => _key.SetValue(InitializedValue, 1, RegistryValueKind.DWord);
}
