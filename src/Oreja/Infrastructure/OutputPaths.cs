using System.Globalization;

namespace Oreja.Infrastructure;

/// <summary>Wo Aufnahmen entstehen (Arbeitsordner) und wo sie landen (Zielordner).</summary>
public sealed class OutputPaths
{
    public const string Extension = ".flac";

    private readonly Func<string> _targetFolder;

    public OutputPaths(string workingFolder, Func<string> targetFolder)
    {
        WorkingFolder = workingFolder;
        _targetFolder = targetFolder;
    }

    public string WorkingFolder { get; }

    public string TargetFolder => _targetFolder();

    /// <summary>Neue Datei im Arbeitsordner, benannt nach der Startzeit, eindeutig in Arbeits- und Zielordner.</summary>
    public string NewRecordingPath(DateTime start)
    {
        Directory.CreateDirectory(WorkingFolder);
        string target = TargetFolder;
        string baseName = start.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
        string name = UniqueFileName(baseName,
            n => File.Exists(Path.Combine(WorkingFolder, n)) || File.Exists(Path.Combine(target, n)));
        return Path.Combine(WorkingFolder, name);
    }

    /// <summary>„name.flac“, bei Kollision „name_2.flac“, „name_3.flac“, …</summary>
    public static string UniqueFileName(string baseName, Func<string, bool> isTaken)
    {
        string name = baseName + Extension;
        for (int i = 2; isTaken(name); i++) name = $"{baseName}_{i}{Extension}";
        return name;
    }
}
