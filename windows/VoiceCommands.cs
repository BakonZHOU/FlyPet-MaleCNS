using System.Diagnostics;

namespace FlyPet;

public enum VoiceIntentKind { SearchFiles, LaunchApp }

public sealed record VoiceIntent(VoiceIntentKind Kind,string Value);

public static class VoiceCommandParser
{
    static readonly string[] WakeWords=["强强","枪枪","蝇蝇","营营","阴阴","音音","英英","银银","赢赢","yingying","yinyin"];
    static readonly string[] SearchPrefixes=["搜索文件","查找文件","搜索","查找","找一下","找"];
    static readonly string[] LaunchPrefixes=["打开软件","启动软件","打开","启动"];

    public static bool TrySplitWakeWord(string text,out string command)
    {
        text=Normalize(text);
        foreach(var wake in WakeWords)
        {
            int at=text.IndexOf(wake,StringComparison.Ordinal);
            if(at<0)continue;
            command=text[(at+wake.Length)..];
            return true;
        }
        command="";return false;
    }

    public static VoiceIntent? Parse(string text)
    {
        text=Normalize(text);
        foreach(var prefix in SearchPrefixes)
        {int at=text.IndexOf(prefix,StringComparison.Ordinal);if(at>=0)return Create(VoiceIntentKind.SearchFiles,text[(at+prefix.Length)..]);}
        foreach(var prefix in LaunchPrefixes)
        {int at=text.IndexOf(prefix,StringComparison.Ordinal);if(at>=0)return Create(VoiceIntentKind.LaunchApp,text[(at+prefix.Length)..]);}
        return null;
    }

    static VoiceIntent? Create(VoiceIntentKind kind,string value)
    {
        value=value.Trim('的','吧','。','！','？','，',',',' ');
        foreach(var suffix in new[]{"一下","好吗","好不好","可以吗","谢谢","呀","啊","喔"})if(value.EndsWith(suffix,StringComparison.Ordinal))value=value[..^suffix.Length];
        return string.IsNullOrWhiteSpace(value)?null:new(kind,value);
    }

    public static string Normalize(string value)=>string.Concat(value.Where(c=>!char.IsWhiteSpace(c)&&!char.IsPunctuation(c))).ToLowerInvariant();
}

public static class LocalFileSearch
{
    public static async Task<IReadOnlyList<string>> SearchAsync(IEnumerable<string> roots,string query,CancellationToken cancellationToken)
        =>await Task.Run(()=>(IReadOnlyList<string>)Search(roots,query,cancellationToken),cancellationToken);

    static List<string> Search(IEnumerable<string> roots,string query,CancellationToken cancellationToken)
    {
        var results=new List<string>();query=VoiceCommandParser.Normalize(query);
        foreach(var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if(!Directory.Exists(root))continue;
            try
            {
                foreach(var path in Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if(!VoiceCommandParser.Normalize(Path.GetFileName(path)).Contains(query,StringComparison.Ordinal))continue;
                    results.Add(path);
                    if(results.Count>=40)return results;
                }
            }
            catch(UnauthorizedAccessException){}catch(IOException){}
        }
        return results.OrderBy(path=>Path.GetFileName(path).Length).ThenBy(path=>path,StringComparer.OrdinalIgnoreCase).ToList();
    }
}

public static class LocalAppLauncher
{
    sealed record AppEntry(string Name,string Path);
    static readonly Lazy<IReadOnlyList<AppEntry>> index=new(BuildIndex);
    public static bool TryLaunch(string name,out string detail)
    {
        string wanted=VoiceCommandParser.Normalize(name);
        if(wanted is "浏览器" or "browser")
        {Process.Start(new ProcessStartInfo("https://www.google.com/"){UseShellExecute=true});detail="默认浏览器";return true;}
        if((wanted is "微信" or "weixin" or "wechat")&&TryActivateExisting(["WeChat","Weixin"],out detail))return true;
        if(wanted is "音乐" or "am" or "applemusic")wanted="applemusic";
        if(wanted is "gpt" or "chatgpt" or "openai")wanted="chatgpt";
        var found=index.Value.OrderBy(entry=>Score(entry.Name,wanted)).FirstOrDefault(entry=>Score(entry.Name,wanted)<3);
        if(found!=null)
        {
            Process.Start(new ProcessStartInfo(found.Path){UseShellExecute=true});detail=found.Name;return true;
        }
        if(wanted is "微信" or "weixin" or "wechat")
        {
            foreach(var path in WeChatCandidates())
            {
                if(!File.Exists(path))continue;
                Process.Start(new ProcessStartInfo(path){UseShellExecute=true});detail="微信";return true;
            }
        }
        detail="未在桌面、开始菜单或常见安装目录找到“"+name+"”";return false;
    }

    static bool TryActivateExisting(IEnumerable<string> names,out string detail)
    {
        foreach(var process in Process.GetProcesses())
        {
            try
            {
                if(!names.Any(name=>process.ProcessName.StartsWith(name,StringComparison.OrdinalIgnoreCase))||process.MainWindowHandle==0)continue;
                Native.ShowWindow(process.MainWindowHandle,9);Native.SetForegroundWindow(process.MainWindowHandle);detail="已切换到现有微信窗口";return true;
            }
            finally{process.Dispose();}
        }
        detail="";return false;
    }

    static int Score(string app,string wanted)
    {
        string normalized=VoiceCommandParser.Normalize(app);
        if(normalized==wanted)return 0;
        if(normalized.StartsWith(wanted,StringComparison.Ordinal))return 1;
        return normalized.Contains(wanted,StringComparison.Ordinal)?2:3;
    }

    static IReadOnlyList<AppEntry> BuildIndex()
    {
        var entries=new List<AppEntry>();var roots=new[]{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)};
        foreach(var root in roots.Where(Directory.Exists))
        {
            IEnumerable<string> shortcuts=[];
            try{shortcuts=Directory.EnumerateFiles(root,"*.lnk",new EnumerationOptions{RecurseSubdirectories=true,IgnoreInaccessible=true,AttributesToSkip=FileAttributes.ReparsePoint});}catch(UnauthorizedAccessException){}
            try{entries.AddRange(shortcuts.Select(path=>new AppEntry(Path.GetFileNameWithoutExtension(path),path)));}catch(UnauthorizedAccessException){}
        }
        return entries.DistinctBy(entry=>entry.Path,StringComparer.OrdinalIgnoreCase).ToList();
    }

    static IEnumerable<string> WeChatCandidates()
    {
        var programFiles=Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86=Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming=Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        yield return Path.Combine(programFiles,"Tencent","WeChat","WeChat.exe");
        yield return Path.Combine(programFilesX86,"Tencent","WeChat","WeChat.exe");
        yield return Path.Combine(local,"Tencent","WeChat","WeChat.exe");
        yield return Path.Combine(roaming,"Tencent","WeChat","WeChat.exe");
    }
}
