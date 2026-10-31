namespace CubeRadar.Core.Capture;

/// <summary>Serial capture lifecycle, driven by the application's worker, never by a UI thread.</summary>
public sealed class CaptureActivity(Action start, Action stop) : IDisposable
{
    public bool IsPaused { get; private set; } = true;
    public void SetPaused(bool paused)
    {
        if (paused == IsPaused) return;
        if (paused) stop(); else start();
        IsPaused = paused;
    }
    public void Dispose() => SetPaused(true);
}
