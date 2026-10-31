using System.Windows.Forms;
using CubeRadar.Core.Radar;

namespace CubeRadar.Cli;

/// <summary>Deterministic visual QA on the Windows build runner; no Npcap, game, hotkeys or sound.</summary>
internal static class Preview
{
    public static void Save(string output)
    {
        Directory.CreateDirectory(output);
        var engine = new RadarEngine(new RadarConfig(), trackPlayers: false);
        var empty = engine.Snapshot();
        CubeView Cube(uint id, float x, float y, float z, string status) => new(id, 1200039, "Verteron", null,
            x, y, z, 0, 0, 0, status, 0, null, false, 1010, Math.Sqrt(x*x+y*y)/100, null, null, null, z, null, null);
        var snapshot = empty with
        {
            Frames = 10, SelfId = 1, Zone = "Verteron", Self = new FixView(0, 0, 0, 0, 1000, "BuffA"),
            Cubes = [Cube(1, 2400, -3600, 800, "Visible"), Cube(2, -4300, 2600, -500, "Visible"),
                Cube(3, 6400, 5100, 0, "OutOfView"), Cube(4, -7000, -4200, 0, "Opening")]
        };
        foreach (var scale in new[] { 1f, 1.25f, 1.5f })
            OverlayRenderer.SavePng(Path.Combine(output, $"radar-{scale:0.00}.png"), snapshot, scale: scale);
        OverlayRenderer.SavePng(Path.Combine(output, "attente.png"), empty);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        SettingsForm.SavePng(output, new RadarSettings(), new OverlayActions(() => { }, () => { }, () => "-y", _ => { }, () => { }));
    }
}
