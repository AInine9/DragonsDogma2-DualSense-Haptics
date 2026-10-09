using System.Diagnostics;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.Win32;

namespace DragonsDogma2DualSense;
static class Setup
{
    public const string Script="dd2_dualsense_bridge.lua";
    public const string NativePlugin="dd2_output_observer.dll";
    static (int ExitCode,string Output) Execute(string exe,string working,params string[] args)
    {
        var si=new ProcessStartInfo(exe){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=working,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(string arg in args)si.ArgumentList.Add(arg);
        using var p=Process.Start(si)??throw new IOException("Cannot start "+exe);
        var output=p.StandardOutput.ReadToEndAsync();var error=p.StandardError.ReadToEndAsync();
        p.WaitForExit();Task.WaitAll(output,error);
        return (p.ExitCode,output.Result+error.Result);
    }
    public static void Exec(string exe,string working,params string[] args)
    {
        var result=Execute(exe,working,args);
        if(result.ExitCode!=0)throw new IOException(Path.GetFileName(exe)+" failed (exit "+result.ExitCode+"): "+result.Output);
    }
    internal static bool ExtractionCompleted(int exitCode,string output) =>
        exitCode is 0 or 1 && Regex.IsMatch(output,@"(?m)^Extracted [0-9]+ files from .+\r?$");
    static void ExtractPack(string exe,string working,string list,string pack)
    {
        var result=Execute(exe,working,"-h",list,"-x","-skipUnknowns","-noExtractDir",pack);
        // RETool 0.241 returns 1 even after successful extraction, including empty patches.
        // Only this tool accepts that code; completion text and all bank hashes are also required.
        if(!ExtractionCompleted(result.ExitCode,result.Output))throw new IOException("RETool extraction failed (exit "+result.ExitCode+"): "+result.Output);
        Console.WriteLine("Extracted "+Path.GetFileName(pack));
    }
    public static Dictionary<string,byte[]> Chunks(byte[] data)
    {
        var result=new Dictionary<string,byte[]>();int p=0;
        while(p<data.Length){if(p+8>data.Length)throw new InvalidDataException("Truncated bank");string tag=System.Text.Encoding.ASCII.GetString(data,p,4);int n=checked((int)BitConverter.ToUInt32(data,p+4));p+=8;if(n>data.Length-p)throw new InvalidDataException("Truncated chunk");result.Add(tag,data[p..(p+n)]);p+=n;}return result;
    }
    public static string? Discover()
    {
        var libs=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")){if(key?.GetValue("SteamPath") is string root)libs.Add(root);}
        foreach(string root in libs.ToArray())
        {
            string vdf=Path.Combine(root,"steamapps/libraryfolders.vdf");
            if(File.Exists(vdf))foreach(Match m in Regex.Matches(File.ReadAllText(vdf),"\"path\"\\s+\"([^\"]+)\""))libs.Add(m.Groups[1].Value.Replace(@"\\",@"\"));
        }
        var found=new List<string>();
        foreach(string lib in libs){string manifest=Path.Combine(lib,"steamapps/appmanifest_2054970.acf");if(!File.Exists(manifest))continue;var m=Regex.Match(File.ReadAllText(manifest),"\"installdir\"\\s+\"([^\"]+)\"");if(m.Success){string path=Path.GetFullPath(Path.Combine(lib,"steamapps/common",m.Groups[1].Value));if(File.Exists(Path.Combine(path,"DD2.exe")))found.Add(path);}}
        return found.Count==1?found[0]:null;
    }
    static string Download(string name,string url,string hash)
    {
        Directory.CreateDirectory(Files.Data("tools"));string path=Files.Data("tools/"+name);
        if(!File.Exists(path)||Files.Sha(path)!=hash)
        {
            using var client=new HttpClient{Timeout=TimeSpan.FromMinutes(3)};client.DefaultRequestHeaders.UserAgent.ParseAdd("DD2-DualSense-Setup/0.1.0");
            using var input=client.GetStreamAsync(url).GetAwaiter().GetResult();
            using(var output=File.Create(path+".download"))input.CopyTo(output);
            if(Files.Sha(path+".download")!=hash)throw new InvalidDataException("Tool download checksum mismatch; nothing executed");
            File.Move(path+".download",path,true);
        }return path;
    }
    public static string? Option(string[] args,string key)
    {
        int i=Array.IndexOf(args,key);if(i<0)return null;if(i+1==args.Length)throw new ArgumentException("Missing value for "+key);return args[i+1];
    }
    public static void Run(string[] args)
    {
        bool prepare=args.Contains("--prepare-only");
        if(!prepare && Files.GameRunning()!=false)throw new InvalidOperationException("Close Dragon's Dogma 2 before Setup");
        var config=Configuration.Read();string? game=Option(args,"--game")??(config.Game.Length>0?config.Game:Discover());
        if(game==null){Console.Write("Game folder containing DD2.exe: ");game=Console.ReadLine()?.Trim().Trim('"');}
        game=Path.GetFullPath(game??throw new ArgumentException("Game folder is required"));
        if(!File.Exists(Path.Combine(game,"DD2.exe")))throw new InvalidDataException("DD2.exe was not found in that folder");
        if(!prepare&&!File.Exists(Path.Combine(game,"dinput8.dll")))throw new InvalidOperationException("Install REFramework for DD2 first");
        if(!prepare)ValidateInstall(game);
        var catalog=SoundCatalog.Load();string? decoder=Option(args,"--decoder"),extractor=Option(args,"--pak-tool"),extracted=Option(args,"--extracted");
        if(decoder==null){string zip=Download("vgmstream-r2117.zip","https://github.com/vgmstream/vgmstream/releases/download/r2117/vgmstream-win64.zip","6c4a8a3813864fefed081bbd337dbc0ad93bf88e0b92f5db98d7ab258b22dc6c");string dir=Files.Data("tools/vgmstream-r2117");ZipFile.ExtractToDirectory(zip,dir,true);decoder=Path.Combine(dir,"vgmstream-cli.exe");}
        if(extracted==null)
        {
            if(extractor==null){string rar=Download("REtool-0.241.rar","https://fluffyquack.com/tools/REtool.rar","0a321982623deaa7cfe13936a871bcedf6306ec0aa1ec3a6f58b28cf88bfab00");string dir=Files.Data("tools/REtool-0.241");Directory.CreateDirectory(dir);Exec("tar.exe",dir,"-xf",rar,"-C",dir);extractor=Path.Combine(dir,"REtool.exe");}
            extracted=Files.Data("extracted-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(extracted);
            string list=Files.Data("banks.list");File.WriteAllLines(list,catalog.Banks.Keys);
            foreach(string pack in new[]{Path.Combine(game,"re_chunk_000.pak")}.Concat(Directory.GetFiles(game,"re_chunk_000.pak.patch_*.pak").Order(StringComparer.Ordinal)))
                ExtractPack(Path.GetFullPath(extractor),extracted,list,pack);
        }
        // A successful tool exit is insufficient: verify every expected output before conversion.
        foreach(var (resource,hash) in catalog.Banks)
            if(!File.Exists(Path.Combine(extracted,resource))||Files.Sha(Path.Combine(extracted,resource))!=hash)throw new InvalidDataException("Missing or unsupported game bank: "+resource);
        PreparedWaves.Prepare(catalog,Path.GetFullPath(extracted),Path.GetFullPath(decoder));
        (config with {Game=game}).Save();
        if(!prepare)Install(game);
        Console.WriteLine(prepare?"Assets prepared; game files were not changed.":"Setup complete. Use Start-Mod.cmd.");
    }
    record InstallRecord(string Game,string InstalledSha256,string RoutesSha256,string NativeSha256="");
    internal static string GameRecord(string game) => Path.Combine(game,"reframework/data/dd2_dualsense_install-record.json");
    static InstallRecord? ReadRecord(string path) => File.Exists(path)
        ? JsonSerializer.Deserialize<InstallRecord>(File.ReadAllText(path)) ?? throw new InvalidDataException("Invalid install record: "+path)
        : null;
    static InstallRecord? PreviousInstall(string game)
    {
        var local=ReadRecord(Files.Data("install-record.json"));
        var saved=ReadRecord(GameRecord(game));
        foreach(var r in new[]{local,saved})
            if(r!=null&&!Path.GetFullPath(r.Game).Equals(Path.GetFullPath(game),StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Uninstall from the previous game folder first");
        // The game-side record survives deletion of the extracted package and is authoritative.
        return saved??local;
    }
    static string Routes()
    {
        var catalog=SoundCatalog.Load();
        return JsonSerializer.Serialize(new{version=2,events=catalog.Events.Keys.ToDictionary(x=>x.ToString(),_=>true),nearby_events=catalog.NearbyEvents},Configuration.Json);
    }
    static string TextHash(string text) => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
    internal static void CheckInstallFile(string file,string? recorded,string bundled,string legacy)
    {
        if(!File.Exists(file))return;
        string actual=Files.Sha(file);
        // With a record, never bypass modification detection. Without one, accept only exact
        // bundled bytes or the original public v1.0.0 payload (before game-side records).
        if(recorded!=null ? actual==recorded : actual==bundled||actual==legacy)return;
        throw new InvalidDataException("Unowned or modified file was preserved: "+file);
    }
    static void ValidateInstall(string game)
    {
        var previous=PreviousInstall(game);
        string native=Files.Bundled(NativePlugin),script=Files.Bundled(Script);
        if(!File.Exists(native)||!File.Exists(script))throw new InvalidDataException("Bridge or native audio guard is missing from this package");
        CheckInstallFile(Path.Combine(game,"reframework/plugins",NativePlugin),previous?.NativeSha256,Files.Sha(native),"76bd7da57ac6353b04e44c5a09cb8cf51c406aa413617b70a666981f23a9e8f0");
        CheckInstallFile(Path.Combine(game,"reframework/autorun",Script),previous?.InstalledSha256,Files.Sha(script),"76ab06d0071ac5c63b2b24ba79a566576d8ee5f32999b21a555ad575f828224b");
        CheckInstallFile(Path.Combine(game,"reframework/data/dd2_dualsense_routes.json"),previous?.RoutesSha256,TextHash(Routes()),"e9c6ae87ef931aceb52e46dcdc244551967cdae56f694b1de42820b9ce9ec579");
    }
    internal static void CheckOwned(string file,string? expected)
    {
        if(File.Exists(file)&&(string.IsNullOrEmpty(expected)||Files.Sha(file)!=expected))
            throw new InvalidDataException("Unowned or modified file was preserved: "+Path.GetFileName(file));
    }
    internal static void RemoveOwned((string File,string Hash)[] files)
    {
        foreach(var (file,hash) in files)CheckOwned(file,hash);
        foreach(var (file,_) in files)if(File.Exists(file))File.Delete(file);
    }
    public static void Install(string game)
    {
        if(string.IsNullOrWhiteSpace(game)||!File.Exists(Path.Combine(game,"DD2.exe"))||!File.Exists(Path.Combine(game,"dinput8.dll")))throw new InvalidOperationException("Run Setup.cmd with a valid DD2 and REFramework installation first");
        ValidateInstall(game);
        string folder=Path.Combine(game,"reframework/autorun"),data=Path.Combine(game,"reframework/data");Directory.CreateDirectory(folder);Directory.CreateDirectory(data);
        string target=Path.Combine(folder,Script),record=Files.Data("install-record.json");
        string nativeSource=Files.Bundled(NativePlugin),nativeTarget=Path.Combine(game,"reframework/plugins",NativePlugin);
        string route=Path.Combine(data,"dd2_dualsense_routes.json");
        File.WriteAllText(Files.Data("routes.json"),Routes());
        string source=Files.Bundled(Script);File.Copy(source,target+".installing",true);File.Move(target+".installing",target,true);
        File.Copy(Files.Data("routes.json"),route+".installing",true);File.Move(route+".installing",route,true);
        Directory.CreateDirectory(Path.GetDirectoryName(nativeTarget)!);
        File.Copy(nativeSource,nativeTarget+".installing",true);File.Move(nativeTarget+".installing",nativeTarget,true);
        var installed=new InstallRecord(game,Files.Sha(source),Files.Sha(route),Files.Sha(nativeSource));
        if(!Files.Atomic(GameRecord(game),installed))throw new IOException("Cannot save game-side install record");
        if(!Files.Atomic(record,installed))throw new IOException("Cannot save local install record");
    }
    public static void Uninstall()
    {
        if(Files.GameRunning()!=false)throw new InvalidOperationException("Close Dragon's Dogma 2 before uninstalling");
        string path=Files.Data("install-record.json");
        string? game=ReadRecord(path)?.Game;
        game??=Configuration.Read().Game is {Length:>0} configured?configured:Discover();
        var r=game==null?null:PreviousInstall(game);
        if(r==null)throw new InvalidOperationException("Install record not found; run Setup.cmd first");
        string script=Path.Combine(r.Game,"reframework/autorun",Script),routes=Path.Combine(r.Game,"reframework/data/dd2_dualsense_routes.json");
        string native=Path.Combine(r.Game,"reframework/plugins",NativePlugin);
        if(r.NativeSha256.Length>0)CheckOwned(native,r.NativeSha256);
        foreach(var (file,hash) in new[]{(script,r.InstalledSha256),(routes,r.RoutesSha256)})if(File.Exists(file)&&Files.Sha(file)!=hash)throw new InvalidDataException("Modified file was preserved: "+Path.GetFileName(file));
        File.WriteAllText(Files.Data("stop.request"),"stop");
        using(var mutex=new Mutex(false,Bridge.MutexName))
        {
            bool stopped;try{stopped=mutex.WaitOne(TimeSpan.FromSeconds(5));}catch(AbandonedMutexException){stopped=true;}
            if(!stopped)throw new IOException("Companion is still stopping; retry after its console closes");
            mutex.ReleaseMutex();
        }
        var owned=new List<(string File,string Hash)>{(script,r.InstalledSha256),(routes,r.RoutesSha256)};
        if(r.NativeSha256.Length>0)owned.Add((native,r.NativeSha256));
        RemoveOwned(owned.ToArray());
        File.Delete(GameRecord(r.Game));
        if(File.Exists(path))File.Delete(path);
        Console.WriteLine("Bridge and native audio guard removed. Local generated assets remain in data.");
    }
}
