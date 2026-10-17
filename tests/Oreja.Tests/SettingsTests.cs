using Microsoft.Win32;
using Oreja.Infrastructure;
using Oreja.Recording;
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

    [Fact]
    public void SampleRateDefaultsTo16000()
    {
        Assert.Equal(16000, new Settings(_registry.Key).SampleRate);
        Assert.Equal(16000, AudioFormat.DefaultSampleRate);
    }

    [Theory]
    [InlineData(16000)]
    [InlineData(44100)]
    [InlineData(48000)]
    public void SampleRateIsPersisted(int rate)
    {
        new Settings(_registry.Key).SampleRate = rate;

        Assert.Equal(rate, new Settings(_registry.Key).SampleRate);
    }

    [Fact]
    public void SampleRateIsStoredAsDWord()
    {
        new Settings(_registry.Key).SampleRate = 48000;

        Assert.Equal(48000, _registry.Key.GetValue("SampleRate"));
        Assert.Equal(RegistryValueKind.DWord, _registry.Key.GetValueKind("SampleRate"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-48000)]
    [InlineData(8000)]
    [InlineData(22050)]
    [InlineData(96000)]
    public void UnsupportedSampleRateIsRejectedAndNotStored(int rate)
    {
        var settings = new Settings(_registry.Key);
        settings.SampleRate = 44100;

        Assert.Throws<ArgumentOutOfRangeException>(() => settings.SampleRate = rate);

        Assert.Equal(44100, new Settings(_registry.Key).SampleRate);
    }

    [Fact]
    public void InvalidStoredSampleRateFallsBackTo16000()
    {
        _registry.Key.SetValue("SampleRate", 12345, RegistryValueKind.DWord);
        Assert.Equal(16000, new Settings(_registry.Key).SampleRate);

        _registry.Key.SetValue("SampleRate", "48000", RegistryValueKind.String);
        Assert.Equal(16000, new Settings(_registry.Key).SampleRate);

        _registry.Key.SetValue("SampleRate", -1, RegistryValueKind.DWord);
        Assert.Equal(16000, new Settings(_registry.Key).SampleRate);
    }

    [Fact]
    public void SupportedSampleRatesAreExactly16k44k48k()
    {
        Assert.Equal(new[] { 16000, 44100, 48000 }, AudioFormat.SupportedSampleRates.OrderBy(r => r));
        Assert.Contains(AudioFormat.DefaultSampleRate, AudioFormat.SupportedSampleRates);
    }
}
