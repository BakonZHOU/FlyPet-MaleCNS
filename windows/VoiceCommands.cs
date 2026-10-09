using System.Diagnostics;

namespace FlyPet;

public enum VoiceIntentKind { SearchFiles, LaunchApp }

public sealed record VoiceIntent(VoiceIntentKind Kind,string Value);

public static class VoiceCommandParser
{
    static readonly string[] WakeWords=["强强","枪枪","蝇蝇","营营"];
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
            if(text.StartsWith(prefix,StringComparison.Ordinal))return Create(VoiceIntentKind.SearchFiles,text[prefix.Length..]);
        foreach(var prefix in LaunchPrefixes)
            if(text.StartsWith(prefix,StringComparison.Ordinal))return Create(VoiceIntentKind.LaunchApp,text[prefix.Length..]);
        return null;
    }

    static VoiceIntent? Create(VoiceIntentKind kind,string value)
    {
        value=value.Trim('的','吧','。','！','？','，',',',' ');
        if(value.EndsWith("一下",StringComparison.Ordinal))value=value[..^2];
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
    public static bool TryLaunch(string name,out string detail)
    {
        string wanted=VoiceCommandParser.Normalize(name);
        foreach(var shortcut in StartMenuShortcuts())
        {
            if(!VoiceCommandParser.Normalize(Path.GetFileNameWithoutExtension(shortcut)).Contains(wanted,StringComparison.Ordinal))continue;
            Process.Start(new ProcessStartInfo(shortcut){UseShellExecute=true});detail=Path.GetFileNameWithoutExtension(shortcut);return true;
        }
        if(wanted is "微信" or "weixin" or "wechat")
        {
            foreach(var path in WeChatCandidates())
            {
                if(!File.Exists(path))continue;
                Process.Start(new ProcessStartInfo(path){UseShellExecute=true});detail="微信";return true;
            }
        }
        detail="未在开始菜单或常见安装目录找到“"+name+"”";return false;
    }

    static IEnumerable<string> StartMenuShortcuts()
    {
        var roots=new[]{Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)};
        foreach(var root in roots.Where(Directory.Exists))
        {
            IEnumerable<string> shortcuts=[];
            try{shortcuts=Directory.EnumerateFiles(root,"*.lnk",SearchOption.AllDirectories);}catch(UnauthorizedAccessException){}
            foreach(var shortcut in shortcuts)yield return shortcut;
        }
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
