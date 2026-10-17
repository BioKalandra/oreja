using Microsoft.Win32;
using Oreja.Recording;

namespace Oreja.Infrastructure;

/// <summary>Dauerhafte Einstellungen unter HKCU\Software\Oreja.</summary>
public sealed class Settings
{
    private const string OutputFolderValue = "OutputFolder";
    private const string InitializedValue = "Initialized";
    private const string SampleRateValue = "SampleRate";

    private readonly RegistryKey _key;

    public Settings(RegistryKey key) => _key = key;

    public static string DefaultOutputFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Oreja");

    public string OutputFolder
    {
        get => _key.GetValue(OutputFolderValue) is string folder && folder.Length > 0 ? folder : DefaultOutputFolder;
        set => _key.SetValue(OutputFolderValue, value, RegistryValueKind.String);
    }

    /// <summary>Abtastrate neuer Aufnahmen; ein fehlender oder ungültiger Wert ergibt den Standard.</summary>
    public int SampleRate
    {
        get => _key.GetValue(SampleRateValue) is int rate && AudioFormat.IsSupported(rate) ? rate : AudioFormat.DefaultSampleRate;
        set
        {
            if (!AudioFormat.IsSupported(value))
                throw new ArgumentOutOfRangeException(nameof(value), value, "Nicht unterstützte Abtastrate");
            _key.SetValue(SampleRateValue, value, RegistryValueKind.DWord);
        }
    }

    public bool IsInitialized => _key.GetValue(InitializedValue) is int value && value == 1;

    public void MarkInitialized() => _key.SetValue(InitializedValue, 1, RegistryValueKind.DWord);
}
