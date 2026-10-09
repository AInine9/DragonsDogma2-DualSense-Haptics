using System.Diagnostics;
namespace DragonsDogma2DualSense;
static class Program
{
    static int Main(string[] args)
    {
        Console.OutputEncoding=System.Text.Encoding.UTF8;
        AppHost.Initialize();
        Files.EchoLogsToConsole = args.FirstOrDefault() is not ("run" or "start" or "launch");
        try
        {
            switch(args.FirstOrDefault()??"help")
            {
                case "setup":Setup.Run(args);break;
                case "install":
                    if(Files.GameRunning()!=false)throw new InvalidOperationException("Close the game before installation");
                    Setup.Install(Configuration.Read().Game);break;
                case "uninstall":Setup.Uninstall();break;
                case "run":Bridge.Run();break;
                case "start":
                case "launch":
                    _=PreparedWaves.Load();
                    bool autoLaunch=Configuration.Read().AutoLaunchGame&&!args.Contains("--no-game");
                    foreach(string line in LaunchInstructions(autoLaunch))Console.WriteLine(line);
                    if(autoLaunch&&Files.GameRunning()==false)Process.Start(new ProcessStartInfo("steam://rungameid/2054970"){UseShellExecute=true});
                    Bridge.Run();break;
                case "stop":File.WriteAllText(Files.Data("stop.request"),"stop");break;
                case "status":Console.WriteLine(File.Exists(Files.Data("status.json"))?File.ReadAllText(Files.Data("status.json")):"Not started");break;
                case "verify-prepared":PreparedWaves.Verify();break;
                case "diagnose":foreach(var d in Hid.Find())Console.WriteLine($"{d.Model}: {d.Transport}, {d.ReportLength} bytes");Audio.Diagnose();break;
#if DEVELOPER
                case "channel-check":ChannelCheck.Run();break;
                case "source-check":SourceCheck.Run(args);break;
                case "steady-check":ChannelCheck.Steady();break;
                case "suppression-check":ChannelCheck.Suppression();break;
                case "test":Tests.Run();break;
                case "rebuild-waves":RebuildWaves.Run(args);break;
                case "compact-wave-index":CompactWaveIndex.Run(args);break;
                case "waveform-study":WaveformStudy.Run(args);break;
#endif
                default:Console.WriteLine("Commands: setup, launch, run, stop, status, uninstall, diagnose, verify-prepared");return args.Length==0||args[0]=="help"?0:1;
            }return 0;
        }
        catch(Exception e){Console.Error.WriteLine("ERROR: "+e.Message);try{Files.Log(e.ToString());}catch{}return 1;}
    }
    internal static string[] LaunchInstructions(bool autoLaunch) => autoLaunch
        ? ["The Dragon's Dogma 2 DualSense MOD is running while this window is open.",
           "To stop the MOD, close this window (or press Ctrl+C)."]
        : ["The Dragon's Dogma 2 DualSense MOD is running while this window is open.",
           "To stop the MOD, close this window (or press Ctrl+C).",
           "Automatic game launch is disabled. Start Dragon's Dogma 2 manually from Steam."];

}
