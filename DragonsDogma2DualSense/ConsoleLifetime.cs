using System.Runtime.InteropServices;

namespace DragonsDogma2DualSense;

// Give the foreground loop time to release HID/audio when Windows closes its console.
sealed class ConsoleLifetime : IDisposable
{
    delegate bool Handler(uint signal);
    [DllImport("kernel32.dll", SetLastError=true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool SetConsoleCtrlHandler(Handler handler, [MarshalAs(UnmanagedType.Bool)] bool add);
    readonly Handler handler;
    readonly ManualResetEventSlim finished = new(false);
    readonly ConsoleCancelEventHandler cancel;
    volatile bool stopping;
    public bool Stopping => stopping;
    public ConsoleLifetime()
    {
        cancel=(_,e)=>{e.Cancel=true;stopping=true;};
        handler=signal=>
        {
            if(signal is not (2 or 5 or 6))return false;
            stopping=true;
            finished.Wait(TimeSpan.FromSeconds(4));
            return true;
        };
        Console.CancelKeyPress+=cancel;
        SetConsoleCtrlHandler(handler,true);
    }
    public void Dispose()
    {
        finished.Set();
        SetConsoleCtrlHandler(handler,false);
        Console.CancelKeyPress-=cancel;
        GC.KeepAlive(handler);
    }
}
