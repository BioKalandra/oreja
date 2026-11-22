using Oreja.Recording;

namespace Oreja.Tests;

public sealed class LibFlacLoaderTests : IDisposable
{
    private static readonly byte[] Content = [0x4D, 0x5A, 1, 2, 3, 4, 5, 6, 7, 8];

    private readonly string _root = Directory.CreateTempSubdirectory("oreja-native-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void ExtractsIntoAHashNamedFolder()
    {
        string path = LibFlacLoader.EnsureExtracted(_root, Content);

        Assert.Equal(LibFlacLoader.FileName, Path.GetFileName(path));
        Assert.Equal(_root, Path.GetDirectoryName(Path.GetDirectoryName(path)));
        Assert.Equal(Content, File.ReadAllBytes(path));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(path)!)); // keine liegengebliebene .tmp-Datei
    }

    [Fact]
    public void ExtractsAgainWhenOnlyTheEmptyFolderIsLeft()
    {
        // Genau das Bild nach der Windows-Speicheroptimierung: Ordner da, DLL weg.
        string path = LibFlacLoader.EnsureExtracted(_root, Content);
        File.Delete(path);

        Assert.Equal(path, LibFlacLoader.EnsureExtracted(_root, Content));
        Assert.Equal(Content, File.ReadAllBytes(path));
    }

    [Fact]
    public void RepairsADamagedCopy()
    {
        string path = LibFlacLoader.EnsureExtracted(_root, Content);
        File.WriteAllBytes(path, [0x4D, 0x5A]);

        LibFlacLoader.EnsureExtracted(_root, Content);

        Assert.Equal(Content, File.ReadAllBytes(path));
    }

    [Fact]
    public void RepairsACopyWithTheRightLengthButWrongBytes()
    {
        string path = LibFlacLoader.EnsureExtracted(_root, Content);
        var damaged = (byte[])Content.Clone();
        damaged[^1] ^= 0xFF;
        File.WriteAllBytes(path, damaged);

        LibFlacLoader.EnsureExtracted(_root, Content);

        Assert.Equal(Content, File.ReadAllBytes(path));
    }

    [Fact]
    public void LeavesAnIntactCopyUntouched()
    {
        string path = LibFlacLoader.EnsureExtracted(_root, Content);
        var old = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, old);

        LibFlacLoader.EnsureExtracted(_root, Content);

        Assert.Equal(old, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public void ChecksAnIntactCopyWhileItIsInUse()
    {
        string path = LibFlacLoader.EnsureExtracted(_root, Content);
        // Ein anderer Prozess hat die DLL geladen: lesen ja, ersetzen nein.
        using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        Assert.Equal(path, LibFlacLoader.EnsureExtracted(_root, Content));
    }

    [Fact]
    public void FailsLoudlyWhenADamagedCopyCannotBeReplaced()
    {
        string path = LibFlacLoader.EnsureExtracted(_root, Content);
        File.WriteAllBytes(path, [0x4D, 0x5A]);
        using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        Assert.ThrowsAny<Exception>(() => LibFlacLoader.EnsureExtracted(_root, Content));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(path)!)); // die .tmp-Datei ist wieder weg
    }

    [Fact]
    public void DifferentVersionsGetSeparateFolders()
    {
        string first = LibFlacLoader.EnsureExtracted(_root, Content);
        string second = LibFlacLoader.EnsureExtracted(_root, [.. Content, 9]);

        Assert.NotEqual(Path.GetDirectoryName(first), Path.GetDirectoryName(second));
    }

    [Fact]
    public void ExeCarriesTheShippedLibraryAsResource()
    {
        using var resource = typeof(LibFlacLoader).Assembly.GetManifestResourceStream(LibFlacLoader.ResourceName);
        Assert.NotNull(resource);
        using var buffer = new MemoryStream();
        resource.CopyTo(buffer);

        Assert.Equal(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, LibFlacLoader.FileName)), buffer.ToArray());
    }
}
