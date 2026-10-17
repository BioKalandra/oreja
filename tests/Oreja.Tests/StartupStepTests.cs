using System.Text.RegularExpressions;
using Oreja.Infrastructure;

namespace Oreja.Tests;

public sealed class StartupStepTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("oreja-step-");

    public void Dispose()
    {
        if (_dir.Exists) _dir.Delete(recursive: true);
    }

    private string LogPath => Path.Combine(_dir.FullName, "oreja.log");

    private string[] Lines() => File.Exists(LogPath) ? File.ReadAllLines(LogPath) : [];

    [Fact]
    public void SuccessLogsStartAndDoneAndReturnsTheValue()
    {
        var log = new Log(LogPath);

        int result = StartupStep.Run(log, "Testschritt", () => 42, TimeSpan.FromSeconds(5));

        Assert.Equal(42, result);
        var lines = Lines();
        Assert.Equal(2, lines.Length);
        Assert.EndsWith(" INFO Testschritt …", lines[0]);
        Assert.Matches(@" INFO Testschritt fertig \(\d+ ms\)$", lines[1]);
    }

    [Fact]
    public void FastStepLogsNoSlowWarning()
    {
        var log = new Log(LogPath);

        StartupStep.Run(log, "Testschritt", () => 1, TimeSpan.FromSeconds(5));

        Assert.DoesNotContain(Lines(), l => l.Contains("dauert ungewöhnlich lange"));
    }

    [Fact]
    public void SlowStepWarnsExactlyOnceAndStillReturnsTheResult()
    {
        var log = new Log(LogPath);

        string result = StartupStep.Run(log, "Testschritt", () =>
        {
            Thread.Sleep(600);
            return "ok";
        }, TimeSpan.FromMilliseconds(100));

        Assert.Equal("ok", result);
        var lines = Lines();
        var warnings = lines.Where(l => l.Contains("dauert ungewöhnlich lange")).ToArray();
        var warning = Assert.Single(warnings);
        Assert.Matches(@" INFO Testschritt dauert ungewöhnlich lange \(über .+ s\)$", warning);
        Assert.Matches(@" INFO Testschritt fertig \(\d+ ms\)$", lines[^1]);
        Assert.True(Array.IndexOf(lines, warning) < lines.Length - 1, "Warnung muss vor 'fertig' stehen");
    }

    [Fact]
    public void ExceptionIsRethrownUnchangedWithoutDoneLine()
    {
        var log = new Log(LogPath);
        var original = new InvalidOperationException("kaputt");

        var thrown = Assert.Throws<InvalidOperationException>(() =>
            StartupStep.Run<int>(log, "Testschritt", () => throw original, TimeSpan.FromSeconds(5)));

        Assert.Same(original, thrown);
        Assert.Equal("kaputt", thrown.Message);
        Assert.DoesNotContain(Lines(), l => l.Contains("Testschritt fertig"));
        Assert.Contains(Lines(), l => l.EndsWith(" INFO Testschritt …"));
    }

    [Fact]
    public void StepRunsOnAThreadPoolThreadInTheMtaEvenWhenCallerIsSta()
    {
        var log = new Log(LogPath);
        (int Caller, int Step, bool Pool, ApartmentState Apartment) seen = default;
        Exception? error = null;

        var caller = new Thread(() =>
        {
            try
            {
                int callerId = Environment.CurrentManagedThreadId;
                seen = StartupStep.Run(log, "Testschritt", () =>
                    (callerId, Environment.CurrentManagedThreadId, Thread.CurrentThread.IsThreadPoolThread,
                        Thread.CurrentThread.GetApartmentState()), TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        caller.SetApartmentState(ApartmentState.STA);
        caller.Start();
        Assert.True(caller.Join(TimeSpan.FromSeconds(10)));

        Assert.Null(error);
        Assert.NotEqual(seen.Caller, seen.Step);
        Assert.True(seen.Pool);
        Assert.Equal(ApartmentState.MTA, seen.Apartment);
    }
}
