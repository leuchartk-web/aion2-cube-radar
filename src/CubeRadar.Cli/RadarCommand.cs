using CubeRadar.Core.Capture;
using CubeRadar.Core.Protocol;
using CubeRadar.Core.Radar;
using static CubeRadar.Core.Localization.Text;

namespace CubeRadar.Cli;

internal static class RadarCommand
{
    public const string QuitSignalName = @"Local\CubeRadarFR.Quit";
    public const string InstanceLockName = @"Local\CubeRadarFR.LiveRadar";
    public static int Run(string[] args)
    {
        string? device = null;
        var soundEnabled = true;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--device" && i + 1 < args.Length) device = args[++i];
            else if (args[i] == "--no-beep") soundEnabled = false;
            else { NativeConsole.Notify(T("usage", Core.Dump.DumpSession.Version), false); return 2; }
        }
        using var instance = Elevation.SharedMutex(true, InstanceLockName, out var first);
        if (!first) { NativeConsole.Notify(T("alreadyRunning"), false); return 3; }
        if (!LiveSniffer.IsNpcapInstalled()) { NativeConsole.Notify(T("npcapMissing")); return 2; }
        var settings = RadarSettings.Load(Path.Combine(AppPaths.DataDir, "settings.json"));
        var pauseRequested = settings.CapturePaused;
        var searchRequested = 0;
        long lastSearchAt = -10000;
        long packetCount = 0;
        var cfg = RadarConfig.Load(AppPaths.Config("radar.json"), AppPaths.Config("cubes.json"), AppPaths.DataRoot);
        var spots = SpotIndex.TryLoad(cfg.SpotsCsv);
        using var store = new SqliteRadarStore(Path.Combine(AppPaths.DataDir, "radar.db"), "live_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));
        using var stop = new ManualResetEventSlim();
        using var captureChanged = new AutoResetEvent(false);
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Set(); };
        using var quitSignal = Elevation.SharedEvent(QuitSignalName);
        var registeredWait = ThreadPool.RegisterWaitForSingleObject(quitSignal, (_, _) => stop.Set(), null, Timeout.Infinite, true);
        OverlayWindow? overlay = null;
        void Play()
        {
            if (Volatile.Read(ref pauseRequested)) return;
            if (CubeSound.Play(settings.SoundFile, settings.SoundVolumePct) is { } note) overlay?.Notify(T("soundError", note));
        }
        RadarEngine NewEngine()
        {
            var fresh = new RadarEngine(cfg, store, spots, trackPlayers: false);
            if (settings.North is { } north)
            {
                try { fresh.SetNorth(north); } catch (FormatException) { settings.North = null; }
            }
            fresh.CubeAppeared += _ => { if (soundEnabled && settings.SoundEnabled) Play(); };
            return fresh;
        }
        var engine = NewEngine();
        StreamTracker NewTracker()
        {
            var fresh = new StreamTracker();
            fresh.OnFrame = (GameFlow flow, in FrameMeta meta, ReadOnlySpan<byte> body) => engine.OnFrame(flow.TimeMs, meta.Opcode, body);
            return fresh;
        }
        var tracker = NewTracker();
        var gate = new object();
        using var sniffer = new LiveSniffer(new LiveSnifferOptions { DeviceName = device });
        sniffer.PacketArrived += (raw, _) =>
        {
            Interlocked.Increment(ref packetCount);
            if (Volatile.Read(ref pauseRequested) || !SegmentReader.TryRead(raw, out var seg)) return;
            lock (gate) tracker.OnSegment(seg.TimeMs, seg.Key, seg.Seq, seg.Syn, seg.FinOrRst, seg.Payload);
        };
        void ResetTracking()
        {
            // A resumed stream may have a gap: discard old reassembly buffers and live contacts/cubes.
            // Learned cube templates and calibration remain in the persistent store.
            lock (gate)
            {
                Volatile.Write(ref engine, NewEngine());
                tracker = NewTracker();
            }
        }
        using var activity = new CaptureActivity(sniffer.Start, () => { sniffer.Stop(); CubeSound.Close(); });
        void RequestPause(bool paused)
        {
            Volatile.Write(ref pauseRequested, paused);
            settings.CapturePaused = paused;
            settings.Save();
            captureChanged.Set(); // the main worker stops/starts capture; the UI never waits for Npcap
        }
        void FindPosition()
        {
            if (Volatile.Read(ref pauseRequested)) { overlay?.Notify(T("searchPaused")); return; }
            var now = Environment.TickCount64;
            var previous = Interlocked.Read(ref lastSearchAt);
            if (now - previous < 5000 || Interlocked.CompareExchange(ref lastSearchAt, now, previous) != previous)
            { overlay?.Notify(T("searchBusy")); return; }
            Interlocked.Exchange(ref searchRequested, 1);
            captureChanged.Set();
            overlay?.Notify(T("searchHint"));
        }
        string Status()
        {
            if (Volatile.Read(ref pauseRequested)) return T("pausedStatus");
            lock (gate)
            {
                var s = engine.Snapshot();
                var detail = s.Self is { } fix ? T("positionReceived", Math.Max(0, (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - fix.TimeMs) / 1000))
                    : tracker.GameFlows.Count == 0 ? T("waitingGameStream")
                    : s.SelfId is null ? T("waitingIdentity") : T("waitingOwnPosition");
                return sniffer.Status + "\n" + T("captureDiagnostics", Interlocked.Read(ref packetCount), tracker.GameFlows.Count, s.Frames) + "\n" + detail;
            }
        }
        RadarSnapshot DisplaySnapshot()
        {
            var s = Volatile.Read(ref engine).Snapshot();
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return s with
            {
                Self = s.Self is { } fix ? fix with { AgeMs = Math.Max(fix.AgeMs, now - fix.TimeMs) } : null,
                Ring = s.Ring is { } ring ? ring with { AgeMs = Math.Max(ring.AgeMs, now - ring.TimeMs) } : null,
            };
        }
        var actions = new OverlayActions(stop.Set, Play, () => engine.North, north =>
        {
            lock (gate) { engine.SetNorth(north); settings.North = engine.North; }
            settings.Save();
        }, () => Elevation.Open(AppPaths.DataRoot),
            Status, () => Volatile.Read(ref pauseRequested), RequestPause, FindPosition);
        using var keys = new GlobalHotkeys(GlobalHotkeys.Parse("ctrl+shift+F8=pause,ctrl+shift+F9=move,ctrl+shift+F10=visible,ctrl+shift+F11=settings,ctrl+shift+F12=range"), b =>
        {
            if (b.Label == "pause") RequestPause(!Volatile.Read(ref pauseRequested));
            else if (b.Label == "move") overlay?.TogglePlacement();
            else if (b.Label == "visible") overlay?.ToggleVisible();
            else if (b.Label == "settings") overlay?.ToggleSettings();
            else overlay?.CycleRange();
        });
        try
        {
            overlay = OverlayWindow.Start(DisplaySnapshot, settings, actions);
            overlay.ShowTray();
            foreach (var (binding, ok, _) in keys.Results) if (!ok) overlay.Notify(T("hotkeyConflict", binding.Display));
            do
            {
                var paused = Volatile.Read(ref pauseRequested);
                if (paused != activity.IsPaused)
                {
                    if (!paused) ResetTracking();
                    activity.SetPaused(paused);
                    if (paused) ResetTracking();
                }
                if (Interlocked.Exchange(ref searchRequested, 0) != 0 && !paused)
                    activity.Refresh(() => { lock (gate) tracker = NewTracker(); });
            }
            while (WaitHandle.WaitAny([stop.WaitHandle, captureChanged]) != 0);
        }
        finally
        {
            activity.Dispose(); registeredWait.Unregister(null); overlay?.Shutdown(); CubeSound.Close();
        }
        return 0;
    }
}
