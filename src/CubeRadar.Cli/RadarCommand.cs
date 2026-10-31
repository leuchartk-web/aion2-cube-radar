using CubeRadar.Core.Capture;
using CubeRadar.Core.Protocol;
using CubeRadar.Core.Radar;
using static CubeRadar.Core.Localization.Text;

namespace CubeRadar.Cli;

/// <summary>Native cubes-only application. No web listener, updater, packet recording or enemy tracking.</summary>
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
        var cfg = RadarConfig.Load(AppPaths.Config("radar.json"), AppPaths.Config("cubes.json"), AppPaths.DataRoot);
        using var store = new SqliteRadarStore(Path.Combine(AppPaths.DataDir, "radar.db"), "live_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));
        var engine = new RadarEngine(cfg, store, SpotIndex.TryLoad(cfg.SpotsCsv), trackPlayers: false);
        if (settings.North is { } saved)
        {
            try { engine.SetNorth(saved); }
            catch (FormatException) { settings.North = null; }
        }
        using var stop = new ManualResetEventSlim();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Set(); };
        using var quitSignal = Elevation.SharedEvent(QuitSignalName);
        var registeredWait = ThreadPool.RegisterWaitForSingleObject(quitSignal, (_, _) => stop.Set(), null, Timeout.Infinite, true);
        OverlayWindow? overlay = null;
        void Play()
        {
            if (CubeSound.Play(settings.SoundFile, settings.SoundVolumePct) is { } note) overlay?.Notify(T("soundError", note));
        }
        engine.CubeAppeared += _ => { if (soundEnabled && settings.SoundEnabled) Play(); };
        // Preserve upstream TCP reassembly and embedded LZ4 decoding, without census or recording work.
        var tracker = new StreamTracker();
        tracker.OnFrame = (GameFlow flow, in FrameMeta meta, ReadOnlySpan<byte> body) => engine.OnFrame(flow.TimeMs, meta.Opcode, body);
        var gate = new object();
        using var sniffer = new LiveSniffer(new LiveSnifferOptions { DeviceName = device });
        sniffer.PacketArrived += (raw, _) =>
        {
            if (!SegmentReader.TryRead(raw, out var seg)) return;
            lock (gate) tracker.OnSegment(seg.TimeMs, seg.Key, seg.Seq, seg.Syn, seg.FinOrRst, seg.Payload);
        };
        var actions = new OverlayActions(stop.Set, Play, () => engine.North, north =>
        {
            engine.SetNorth(north); settings.North = engine.North; settings.Save();
        }, () => Elevation.Open(AppPaths.DataRoot));
        using var keys = new GlobalHotkeys(GlobalHotkeys.Parse("ctrl+shift+F10=visible,ctrl+shift+F11=settings,ctrl+shift+F12=range"), b =>
        {
            if (b.Label == "visible") overlay?.ToggleVisible();
            else if (b.Label == "settings") overlay?.ToggleSettings();
            else overlay?.CycleRange();
        });
        try
        {
            overlay = OverlayWindow.Start(engine.Snapshot, settings, actions);
            overlay.ShowTray();
            foreach (var (binding, ok, _) in keys.Results) if (!ok) overlay.Notify(T("hotkeyConflict", binding.Display));
            sniffer.Start(); stop.Wait();
        }
        finally
        {
            sniffer.Stop(); registeredWait.Unregister(null); overlay?.Shutdown(); CubeSound.Close();
        }
        return 0;
    }
}
