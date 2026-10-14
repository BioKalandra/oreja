namespace Oreja.Tests;

public sealed class TrayIconsTests
{
    [Theory]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(32)]
    public void DrawsAllIconsInRequestedSize(int size)
    {
        using var icons = new TrayIcons(size);

        foreach (var kind in Enum.GetValues<TrayIconKind>())
        {
            Assert.Equal(size, icons[kind].Width);
            Assert.Equal(size, icons[kind].Height);
        }
    }
}
