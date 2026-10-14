namespace Oreja.Tests;

public sealed class ClickGuardTests
{
    [Fact]
    public void AcceptsFirstClick() => Assert.True(new ClickGuard(TimeSpan.FromSeconds(1)).TryAccept(5000));

    [Fact]
    public void IgnoresClicksWithinOneSecond()
    {
        var guard = new ClickGuard(TimeSpan.FromSeconds(1));
        guard.TryAccept(5000);

        Assert.False(guard.TryAccept(5300));
        Assert.False(guard.TryAccept(5999));
    }

    [Fact]
    public void AcceptsAgainAfterOneSecondEvenIfClickedInBetween()
    {
        var guard = new ClickGuard(TimeSpan.FromSeconds(1));
        guard.TryAccept(5000);
        guard.TryAccept(5900);

        Assert.True(guard.TryAccept(6000));
    }
}
