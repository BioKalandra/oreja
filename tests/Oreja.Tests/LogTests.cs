using Oreja.Infrastructure;

namespace Oreja.Tests;

public sealed class LogTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("oreja-log-");

    public void Dispose()
    {
        if (_dir.Exists) _dir.Delete(recursive: true);
    }

    private string LogPath => Path.Combine(_dir.FullName, "oreja.log");

    [Fact]
    public void WritesTimestampedLinesWithLevel()
    {
        var log = new Log(LogPath);

        log.Info("Aufnahme gestartet");

        var line = Assert.Single(File.ReadAllLines(LogPath));
        Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} INFO Aufnahme gestartet$", line);
    }

    [Fact]
    public void ErrorIncludesExceptionDetails()
    {
        var log = new Log(LogPath);

        log.Error("Fehler beim Schreiben", new InvalidOperationException("kaputt"));

        Assert.Contains("ERROR Fehler beim Schreiben: System.InvalidOperationException: kaputt", File.ReadAllText(LogPath));
    }

    [Fact]
    public void RotatesWhenFileExceedsLimit()
    {
        var log = new Log(LogPath, maxBytes: 200);

        for (int i = 0; i < 20; i++) log.Info($"Zeile {i}");

        Assert.True(File.Exists(LogPath + ".1"));
        Assert.True(new FileInfo(LogPath).Length < 300);
        Assert.Contains("Zeile 19", File.ReadAllText(LogPath));
    }

    [Fact]
    public void NeverThrowsOnIoErrors()
    {
        var log = new Log(LogPath);
        _dir.Delete(recursive: true);

        log.Info("geht ins Leere");
    }
}
