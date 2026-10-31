using CubeRadar.Core.Capture;

namespace CubeRadar.Tests;

public class CaptureActivityTests
{
    [Fact]
    public void Pause_closes_capture_once_and_resume_starts_a_fresh_session()
    {
        var calls = new List<string>();
        var generation = 0;
        using var activity = new CaptureActivity(() => calls.Add($"start-{++generation}"), () => calls.Add("stop"));
        activity.SetPaused(true);
        Assert.Empty(calls); // launching with a saved pause must never open Npcap
        activity.SetPaused(false);
        activity.SetPaused(false);
        activity.SetPaused(true);
        activity.SetPaused(true);
        Assert.True(activity.IsPaused);
        Assert.Equal(["start-1", "stop"], calls);
        activity.SetPaused(false);
        activity.Dispose();
        activity.Dispose();
        Assert.Equal(["start-1", "stop", "start-2", "stop"], calls);
    }

    [Fact]
    public void Refresh_reopens_active_transport_but_does_not_resume_a_paused_capture()
    {
        var calls = new List<string>();
        using var activity = new CaptureActivity(() => calls.Add("start"), () => calls.Add("stop"));
        Assert.False(activity.Refresh(() => calls.Add("transport")));
        Assert.Empty(calls);
        activity.SetPaused(false);
        Assert.True(activity.Refresh(() => calls.Add("transport")));
        Assert.Equal(["start", "stop", "transport", "start"], calls);
        Assert.False(activity.IsPaused);
    }

    [Fact]
    public void A_failed_start_does_not_report_an_active_capture()
    {
        using var activity = new CaptureActivity(() => throw new IOException("test"), () => Assert.Fail("nothing was started"));
        Assert.Throws<IOException>(() => activity.SetPaused(false));
        Assert.True(activity.IsPaused);
    }
}
