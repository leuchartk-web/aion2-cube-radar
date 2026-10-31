using System.Globalization;
using CubeRadar.Cli;
using CubeRadar.Core.Dump;
using static CubeRadar.Core.Localization.Text;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
Console.OutputEncoding = System.Text.Encoding.UTF8;
try
{
    AppPaths.Init();
    if (args is ["--version"]) { Console.WriteLine($"Radar de cubes FR {DumpSession.Version}"); return 0; }
    if (args is ["--preview", var output]) { Preview.Save(output); return 0; }
    if (args is ["--quit"])
    {
        try
        {
            if (!EventWaitHandle.TryOpenExisting(RadarCommand.QuitSignalName, out var signal))
            { NativeConsole.Notify(T("notRunning"), false); return 1; }
            using (signal) signal.Set();
            NativeConsole.Notify(T("quitSent"), false); return 0;
        }
        catch (UnauthorizedAccessException) { NativeConsole.Notify(T("quitDenied")); return 1; }
    }
    if (args.Length == 0) return RadarCommand.Run([]);
    if (args[0] == "radar") return RadarCommand.Run(args[1..]);
    NativeConsole.Notify(T("usage", DumpSession.Version), false); return 2;
}
catch (Exception ex)
{
    NativeConsole.Notify(T("error", ex.Message, AppPaths.DataRoot)); return 1;
}
