using System.Globalization;
using System.Text.Json;

namespace CubeRadar.Core.Localization;

/// <summary>Stable keys keep French copy separate from capture and rendering code.</summary>
public static class Text
{
    private static readonly Dictionary<string, string> French = Load();
    private static Dictionary<string, string> Load()
    {
        using var stream = typeof(Text).Assembly.GetManifestResourceStream("CubeRadar.fr.json")
            ?? throw new InvalidOperationException("Ressources françaises introuvables.");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
    }
    public static string T(string key, params object?[] args)
    {
        var value = French.TryGetValue(key, out var text) ? text : throw new KeyNotFoundException(key);
        return args.Length == 0 ? value : string.Format(CultureInfo.GetCultureInfo("fr-FR"), value, args);
    }
}
