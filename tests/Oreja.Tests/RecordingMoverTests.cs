using Oreja.Infrastructure;

namespace Oreja.Tests;

public sealed class RecordingMoverTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("oreja-move-");
    private string _target;
    private readonly OutputPaths _paths;
    private readonly RecordingMover _mover;

    public RecordingMoverTests()
    {
        _target = Path.Combine(_root.FullName, "target");
        _paths = new OutputPaths(Path.Combine(_root.FullName, "work"), () => _target);
        _mover = new RecordingMover(_paths, new Log(Path.Combine(_root.FullName, "oreja.log")));
        Directory.CreateDirectory(_paths.WorkingFolder);
    }

    public void Dispose() => _root.Delete(recursive: true);

    private string Recording(string name, string content)
    {
        var path = Path.Combine(_paths.WorkingFolder, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void MovesFinishedRecordingsAndDeletesLocalCopy()
    {
        var local = Recording("a.flac", "inhalt-a");

        Assert.Equal(1, _mover.MovePending());

        Assert.False(File.Exists(local));
        Assert.Equal("inhalt-a", File.ReadAllText(Path.Combine(_target, "a.flac")));
        Assert.Empty(Directory.GetFiles(_target, "*.tmp"));
    }

    [Fact]
    public void SkipsFileThatIsStillBeingRecorded()
    {
        var local = Recording("a.flac", "läuft noch");
        using var recording = new FileStream(local, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);

        Assert.Equal(0, _mover.MovePending());

        Assert.True(File.Exists(local));
        Assert.False(Directory.Exists(_target) && Directory.EnumerateFileSystemEntries(_target).Any());
    }

    [Fact]
    public void AddsSuffixOnNameCollisionInTarget()
    {
        Directory.CreateDirectory(_target);
        File.WriteAllText(Path.Combine(_target, "a.flac"), "alt");
        Recording("a.flac", "neu");

        _mover.MovePending();

        Assert.Equal("alt", File.ReadAllText(Path.Combine(_target, "a.flac")));
        Assert.Equal("neu", File.ReadAllText(Path.Combine(_target, "a_2.flac")));
    }

    [Fact]
    public void KeepsFileWhenTargetIsUnreachable()
    {
        var blocker = Path.Combine(_root.FullName, "blocker");
        File.WriteAllText(blocker, "ich bin eine Datei, kein Ordner");
        _target = Path.Combine(blocker, "ziel");
        var local = Recording("a.flac", "inhalt");

        Assert.Equal(0, _mover.MovePending());

        Assert.True(File.Exists(local));
        Assert.Contains("ERROR", File.ReadAllText(Path.Combine(_root.FullName, "oreja.log")));
    }

    [Fact]
    public void OverwritesLeftoverTemporaryFile()
    {
        Directory.CreateDirectory(_target);
        File.WriteAllText(Path.Combine(_target, "a.flac.tmp"), "abgebrochener Rest");
        Recording("a.flac", "vollständig");

        _mover.MovePending();

        Assert.Equal("vollständig", File.ReadAllText(Path.Combine(_target, "a.flac")));
        Assert.False(File.Exists(Path.Combine(_target, "a.flac.tmp")));
    }

    [Fact]
    public void IgnoresNonFlacFiles()
    {
        var other = Recording("notiz.txt", "x");

        Assert.Equal(0, _mover.MovePending());
        Assert.True(File.Exists(other));
    }

    [Fact]
    public void DoesNothingWhenTargetIsTheWorkingFolder()
    {
        _target = _paths.WorkingFolder;
        var local = Recording("a.flac", "x");

        Assert.Equal(0, _mover.MovePending());
        Assert.True(File.Exists(local));
    }

    [Fact]
    public void MovesIntoFolderWithNonAsciiCharacters()
    {
        _target = Path.Combine(_root.FullName, "Jürgen", "Meine Ablage", "Español Übungen");
        Recording("a.flac", "olé");

        Assert.Equal(1, _mover.MovePending());
        Assert.Equal("olé", File.ReadAllText(Path.Combine(_target, "a.flac")));
    }
}
