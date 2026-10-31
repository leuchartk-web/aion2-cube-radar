namespace CubeRadar.Cli;

/// <summary>Dedicated fork data. Never reads or writes the upstream application's settings.</summary>
internal static class AppPaths
{
    public static string DataRoot { get; private set; } = "";
    public static string DataDir => Path.Combine(DataRoot, "data");
    public static string Config(string name) => Path.Combine(DataRoot, "app", "0.9.3", "config", name);
    public static void Init()
    {
        DataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CubeRadarFR");
        var assembly = typeof(AppPaths).Assembly;
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith("config/", StringComparison.Ordinal)))
        {
            var path = Config(Path.GetFileName(name));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var source = assembly.GetManifestResourceStream(name)!;
            using var file = File.Create(path);
            source.CopyTo(file);
        }
    }
}

internal static class NativeConsole
{
    public static bool HasConsole => GetConsoleWindow() != IntPtr.Zero;
    public static void Notify(string message, bool error = true)
    {
        if (error) Console.Error.WriteLine(message); else Console.WriteLine(message);
        if (!HasConsole) System.Windows.Forms.MessageBox.Show(message, Core.Localization.Text.T("app"),
            System.Windows.Forms.MessageBoxButtons.OK, error ? System.Windows.Forms.MessageBoxIcon.Warning : System.Windows.Forms.MessageBoxIcon.Information);
    }
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();
}
