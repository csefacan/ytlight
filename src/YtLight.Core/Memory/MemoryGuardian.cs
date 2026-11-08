using System.Diagnostics;
using System.Runtime.InteropServices;

namespace YtLight.Core.Memory;

public sealed class MemoryGuardian
{
    [DllImport("psapi.dll")]
    private static extern int EmptyWorkingSet(IntPtr hwProc);

    public event EventHandler? TrimRequested;

    public void OnMinimizedToTray()
    {
        try
        {
            TrimRequested?.Invoke(this, EventArgs.Empty);
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);

            using var process = Process.GetCurrentProcess();
            EmptyWorkingSet(process.Handle);
        }
        catch
        {
            // Best effort memory trimming
        }
    }
}
