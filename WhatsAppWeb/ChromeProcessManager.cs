using System.Collections.Generic;
using System.Diagnostics;

namespace WhatsAppWeb;

public static class ChromeProcessManager
{
    static readonly HashSet<int> initialProcessIds = new();
    const string ChromeProcessName = "chrome";

    public static void CaptureInitialProcesses()
    {
        foreach (var process in Process.GetProcesses())
            if (process.ProcessName.Contains(ChromeProcessName))
                initialProcessIds.Add(process.Id);
    }

    public static void KillExtraProcesses()
    {
        bool found;
        do
        {
            found = false;
            foreach (var process in Process.GetProcesses())
            {
                if (!initialProcessIds.Contains(process.Id) && process.ProcessName.Contains(ChromeProcessName))
                {
                    try { process.Kill(); } catch { }
                    found = true;
                }
            }
        } while (found);
    }
}
