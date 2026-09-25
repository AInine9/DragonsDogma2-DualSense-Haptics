using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
namespace DragonsDogma2DualSense;

// A monotonic, expiring lease. Console death cannot leave suppression latched.
sealed class NativeAudioLease : IDisposable
{
    public const string Name=@"Local\DD2DualSenseNativeAudioLeaseV1";
    readonly MemoryMappedFile mapping;
    readonly MemoryMappedViewAccessor view;
    bool disposed;
    [DllImport("kernel32.dll")] static extern ulong GetTickCount64();
    public NativeAudioLease(string name=Name)
    {
        mapping=MemoryMappedFile.CreateOrOpen(name,8,MemoryMappedFileAccess.ReadWrite);
        view=mapping.CreateViewAccessor(0,8,MemoryMappedFileAccess.ReadWrite);
        Renew(false);
    }
    public unsafe void Renew(bool enabled)
    {
        if(disposed)return;
        byte* pointer=null;
        view.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
        try { Volatile.Write(ref *(long*)(pointer+view.PointerOffset),enabled?checked((long)GetTickCount64()+1500):0); }
        finally { view.SafeMemoryMappedViewHandle.ReleasePointer(); }
    }
    public void Dispose()
    {
        if(disposed)return;
        Renew(false);disposed=true;view.Dispose();mapping.Dispose();
    }
}
