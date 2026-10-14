namespace Oreja.Infrastructure;

/// <summary>
/// Verschiebt fertige Aufnahmen vom Arbeitsordner in den Zielordner: Kopie als „name.flac.tmp“
/// ins Ziel, dort umbenennen, erst dann lokal löschen. Sync-Clients (Google Drive) sehen so
/// nur vollständige Dateien. Dateien, die noch geschrieben werden, bleiben liegen.
/// </summary>
public sealed class RecordingMover
{
    private readonly OutputPaths _paths;
    private readonly Log _log;
    private readonly object _gate = new();

    public RecordingMover(OutputPaths paths, Log log)
    {
        _paths = paths;
        _log = log;
    }

    /// <summary>Liefert die Anzahl verschobener Dateien. I/O-Probleme werden protokolliert und beim nächsten Mal erneut versucht.</summary>
    public int MovePending()
    {
        lock (_gate)
        {
            string working = _paths.WorkingFolder;
            string target = _paths.TargetFolder;
            if (!Directory.Exists(working) || SameFolder(working, target)) return 0;

            int moved = 0;
            foreach (var source in Directory.EnumerateFiles(working, "*" + OutputPaths.Extension).Order().ToList())
            {
                try
                {
                    if (MoveOne(source, target)) moved++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _log.Error($"Verschieben von {source} nach {target} fehlgeschlagen, neuer Versuch später", ex);
                }
            }
            return moved;
        }
    }

    private bool MoveOne(string source, string targetFolder)
    {
        string fileName = Path.GetFileName(source);
        string temporary = Path.Combine(targetFolder, fileName + ".tmp");

        using (var input = TryOpenExclusive(source))
        {
            if (input is null) return false; // wird noch aufgenommen
            Directory.CreateDirectory(targetFolder);
            using var output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None);
            input.CopyTo(output);
        }

        string finalName = OutputPaths.UniqueFileName(Path.GetFileNameWithoutExtension(fileName),
            n => File.Exists(Path.Combine(targetFolder, n)));
        string destination = Path.Combine(targetFolder, finalName);
        File.Move(temporary, destination);
        File.Delete(source);
        _log.Info($"Aufnahme verschoben: {source} -> {destination}");
        return true;
    }

    private static FileStream? TryOpenExclusive(string path)
    {
        try
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
        }
        catch (IOException)
        {
            return null; // noch geöffnet (Freigabeverletzung) oder inzwischen verschwunden
        }
    }

    private static bool SameFolder(string a, string b) =>
        string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)),
                      Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
                      StringComparison.OrdinalIgnoreCase);
}
