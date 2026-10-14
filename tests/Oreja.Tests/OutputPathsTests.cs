using Oreja.Infrastructure;

namespace Oreja.Tests;

public sealed class OutputPathsTests : IDisposable
{
    private static readonly DateTime Start = new(2026, 10, 14, 18, 5, 32);
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("oreja-paths-");
    private readonly OutputPaths _paths;

    public OutputPathsTests() =>
        _paths = new OutputPaths(Path.Combine(_root.FullName, "work"), () => Path.Combine(_root.FullName, "target"));

    public void Dispose() => _root.Delete(recursive: true);

    [Fact]
    public void NewRecordingPathIsNamedAfterStartTimeInWorkingFolder()
    {
        var path = _paths.NewRecordingPath(Start);

        Assert.Equal(Path.Combine(_root.FullName, "work", "2026-10-14_18-05-32.flac"), path);
        Assert.True(Directory.Exists(_paths.WorkingFolder));
    }

    [Fact]
    public void AddsSuffixWhenNameIsTakenInWorkingFolder()
    {
        File.WriteAllText(_paths.NewRecordingPath(Start), "");

        Assert.EndsWith("2026-10-14_18-05-32_2.flac", _paths.NewRecordingPath(Start));
    }

    [Fact]
    public void AddsSuffixWhenNameIsTakenInTargetFolder()
    {
        Directory.CreateDirectory(_paths.TargetFolder);
        File.WriteAllText(Path.Combine(_paths.TargetFolder, "2026-10-14_18-05-32.flac"), "");

        Assert.EndsWith("2026-10-14_18-05-32_2.flac", _paths.NewRecordingPath(Start));
    }

    [Fact]
    public void UniqueFileNameCountsUp()
    {
        var taken = new HashSet<string> { "a.flac", "a_2.flac" };

        Assert.Equal("a_3.flac", OutputPaths.UniqueFileName("a", taken.Contains));
        Assert.Equal("b.flac", OutputPaths.UniqueFileName("b", taken.Contains));
    }
}
