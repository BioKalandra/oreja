using Microsoft.Win32;

namespace Oreja.Infrastructure;

/// <summary>Autostart-Eintrag unter HKCU\...\Run, zeigt immer auf die laufende Exe.</summary>
public sealed class Autostart
{
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly RegistryKey _runKey;
    private readonly string _valueName;
    private readonly string _exePath;

    public Autostart(RegistryKey runKey, string valueName, string exePath)
    {
        _runKey = runKey;
        _valueName = valueName;
        _exePath = exePath;
    }

    private string Command => $"\"{_exePath}\"";

    public bool IsEnabled => _runKey.GetValue(_valueName) is string;

    public void Enable() => _runKey.SetValue(_valueName, Command, RegistryValueKind.String);

    public void Disable() => _runKey.DeleteValue(_valueName, throwOnMissingValue: false);

    /// <summary>Richtet einen vorhandenen Eintrag auf die aktuelle Exe aus (z. B. nach Verschieben). Schaltet nichts ein.</summary>
    public void RepairPath()
    {
        if (_runKey.GetValue(_valueName) is string current && current != Command) Enable();
    }
}
