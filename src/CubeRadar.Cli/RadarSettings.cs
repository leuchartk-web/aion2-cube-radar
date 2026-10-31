using System.Text.Json;
namespace CubeRadar.Cli;

internal sealed class RadarSettings
{
    private readonly object _gate = new();
    private string _path = "";
    public string? North { get; set; }
    public int? OverlayX { get; set; }
    public int? OverlayY { get; set; }
    public bool OverlayVisible { get; set; } = true;
    public bool CapturePaused { get; set; }
    public int OverlayRangeM { get; set; } = 120;
    public bool OverlayHideInMenus { get; set; } = true;
    public bool OverlayOnlyWhenGameFocused { get; set; } = true;
    public int OverlayScalePct { get; set; } = 125;
    public int OverlayOpacityPct { get; set; } = 95;
    public bool SoundEnabled { get; set; } = true;
    public string? SoundFile { get; set; }
    public int SoundVolumePct { get; set; } = 65;
    public static RadarSettings Load(string path)
    {
        RadarSettings result;
        try { result = File.Exists(path) ? JsonSerializer.Deserialize<RadarSettings>(File.ReadAllText(path)) ?? new() : new(); }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException) { result = new(); }
        result._path = path;
        if (!OverlayWindow.Ranges.Contains(result.OverlayRangeM)) result.OverlayRangeM = 120;
        if (!new[] { 75, 100, 125, 150 }.Contains(result.OverlayScalePct)) result.OverlayScalePct = 125;
        result.OverlayOpacityPct = Math.Clamp(result.OverlayOpacityPct, 40, 100);
        result.SoundVolumePct = Math.Clamp(result.SoundVolumePct, 5, 100);
        return result;
    }
    public void Save()
    {
        if (_path.Length == 0) return;
        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                var temp = _path + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
                File.Move(temp, _path, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
}
