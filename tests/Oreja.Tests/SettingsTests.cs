using Oreja.Infrastructure;
using Oreja.Tests.Fakes;

namespace Oreja.Tests;

public sealed class SettingsTests : IDisposable
{
    private readonly TestRegistryKey _registry = new();

    public void Dispose() => _registry.Dispose();

    [Fact]
    public void OutputFolderDefaultsToDocumentsOreja()
    {
        var settings = new Settings(_registry.Key);

        var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Oreja");
        Assert.Equal(expected, settings.OutputFolder);
        Assert.Equal(expected, Settings.DefaultOutputFolder);
    }

    [Fact]
    public void OutputFolderIsPersisted()
    {
        new Settings(_registry.Key).OutputFolder = @"G:\Meine Ablage\Spanisch";

        Assert.Equal(@"G:\Meine Ablage\Spanisch", new Settings(_registry.Key).OutputFolder);
    }

    [Fact]
    public void InitializedMarkerIsPersisted()
    {
        var settings = new Settings(_registry.Key);
        Assert.False(settings.IsInitialized);

        settings.MarkInitialized();

        Assert.True(new Settings(_registry.Key).IsInitialized);
    }
}
