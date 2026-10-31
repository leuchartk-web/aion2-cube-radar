using System.Text.Json;
using CubeRadar.Core.Localization;
using CubeRadar.Core.Radar;

namespace CubeRadar.Tests;

public class CubesOnlyTests
{
    [Theory]
    [InlineData("cubes/2026-10-13_verteron_two_cubes.json")]
    [InlineData("cubes/2026-10-13_verteron_1200041_teleport.json")]
    [InlineData("selfpos/2026-10-13_position_sources.json")]
    public void Disabling_contacts_preserves_cube_lifecycle_and_own_position(string fixture)
    {
        var full = new RadarEngine(new RadarConfig());
        var cubes = new RadarEngine(new RadarConfig(), trackPlayers: false);
        var appeared = new List<uint>();
        cubes.CubeAppeared += c => appeared.Add(c.ObjectId);
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", fixture)));
        var records = doc.RootElement.TryGetProperty("frames", out var frames) ? frames : doc.RootElement.GetProperty("records");
        foreach (var f in records.EnumerateArray())
        {
            var t = f.GetProperty("t").GetInt64();
            var op = Convert.ToUInt16(f.GetProperty("op").GetString(), 16);
            var body = Convert.FromHexString(f.GetProperty("hex").GetString()!);
            full.OnFrame(t, op, body); cubes.OnFrame(t, op, body);
            var a = full.Snapshot(); var b = cubes.Snapshot();
            Assert.Equal(a.Cubes, b.Cubes);
            Assert.Equal(a.Self, b.Self);
            Assert.Equal(a.SelfId, b.SelfId);
            Assert.Equal(a.Ring, b.Ring);
            Assert.Empty(b.Players);
        }
        if (fixture.StartsWith("cubes/")) Assert.NotEmpty(appeared);
    }

    [Fact]
    public void Enemy_fixture_produces_no_contacts_in_cubes_mode()
    {
        var full = new RadarEngine(new RadarConfig());
        var cubes = new RadarEngine(new RadarConfig(), trackPlayers: false);
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "players", "2026-10-18_abyss.json")));
        var sawContact = false;
        foreach (var f in doc.RootElement.GetProperty("frames").EnumerateArray())
        {
            var t = f.GetProperty("t").GetInt64();
            var op = Convert.ToUInt16(f.GetProperty("op").GetString(), 16);
            var body = Convert.FromHexString(f.GetProperty("hex").GetString()!);
            full.OnFrame(t, op, body); cubes.OnFrame(t, op, body);
            sawContact |= full.Snapshot().Players.Count > 0;
            Assert.Empty(cubes.Snapshot().Players);
        }
        Assert.True(sawContact); // ensure this fixture exercises the disabled path
    }

    [Fact]
    public void French_resources_load_and_format_numbers()
    {
        Assert.Equal("Paramètres", Text.T("settings"));
        Assert.Equal("Plus proche : 42 m", Text.T("nearest", 42));
        Assert.Contains("Vérifie", Text.T("adapterUnavailable"));
    }
}
