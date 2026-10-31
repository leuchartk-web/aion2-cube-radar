using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;

namespace CubeRadar.Cli;

/// <summary>No elevation request: this fork runs with the rights granted by Windows/Npcap.</summary>
internal static class Elevation
{
    public static void Open(string target)
    {
        var elevated = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        Process.Start(elevated ? new ProcessStartInfo("explorer.exe") { ArgumentList = { target } }
            : new ProcessStartInfo(target) { UseShellExecute = true });
    }
    public static Mutex SharedMutex(bool initiallyOwned, string name, out bool createdNew)
    {
        var security = new MutexSecurity();
        security.AddAccessRule(new MutexAccessRule(WindowsIdentity.GetCurrent().User!, MutexRights.FullControl, AccessControlType.Allow));
        return MutexAcl.Create(initiallyOwned, name, out createdNew, security);
    }
    public static EventWaitHandle SharedEvent(string name)
    {
        var security = new EventWaitHandleSecurity();
        security.AddAccessRule(new EventWaitHandleAccessRule(WindowsIdentity.GetCurrent().User!, EventWaitHandleRights.FullControl, AccessControlType.Allow));
        return EventWaitHandleAcl.Create(false, EventResetMode.AutoReset, name, out _, security);
    }
}
