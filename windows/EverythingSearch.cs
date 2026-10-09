using System.IO.Compression;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace FlyPet;

public static class EverythingSearch
{
    const string Library="Everything64.dll";
    public static string ToolFolder=>Path.Combine(Settings.Folder,"tools","everything-sdk");
    static string LibraryPath=>Path.Combine(ToolFolder,Library);
    static EverythingSearch()=>NativeLibrary.SetDllImportResolver(typeof(EverythingSearch).Assembly,Resolve);
    static IntPtr Resolve(string name,Assembly assembly,DllImportSearchPath? path)=>name==Library&&File.Exists(LibraryPath)?NativeLibrary.Load(LibraryPath):IntPtr.Zero;
    public static bool HasSdk=>File.Exists(LibraryPath);
    public static bool IsRunning=>Process.GetProcessesByName("Everything").Length>0;

    public static bool TryOpenUi(string query)
    {
        Process? running=null;
        try
        {
            running=Process.GetProcessesByName("Everything").FirstOrDefault();
            string? executable=running?.MainModule?.FileName;
            if(string.IsNullOrWhiteSpace(executable)||!File.Exists(executable))executable=EverythingCandidates().FirstOrDefault(File.Exists);
            if(string.IsNullOrWhiteSpace(executable))return false;
            var start=new ProcessStartInfo(executable){UseShellExecute=true};start.ArgumentList.Add("-search");start.ArgumentList.Add(query);Process.Start(start);return true;
        }
        catch(InvalidOperationException){return false;}
        catch(System.ComponentModel.Win32Exception){return false;}
        finally{running?.Dispose();}
    }

    static IEnumerable<string> EverythingCandidates()
    {
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Everything","Everything.exe");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Everything","Everything.exe");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Everything","Everything.exe");
    }

    public static bool TrySearch(string query,out IReadOnlyList<string> results,out string error)
    {
        results=[];error="";if(!HasSdk){error="Everything 查询组件尚未安装。";return false;}
        try
        {
            Everything_SetSearchW(query);Everything_SetMax(40);
            if(!Everything_QueryW(true)){error=Everything_GetLastError()==2?"Everything 未在后台运行。":"Everything 查询失败。";return false;}
            var found=new List<string>();uint count=Everything_GetNumResults();
            for(uint i=0;i<count;i++)
            {
                var folder=Marshal.PtrToStringUni(Everything_GetResultPathW(i))??"";var name=Marshal.PtrToStringUni(Everything_GetResultFileNameW(i))??"";
                if(!string.IsNullOrWhiteSpace(name))found.Add(Path.Combine(folder,name));
            }
            results=found;return true;
        }
        catch(Exception e){error="Everything 组件无法加载："+e.Message;return false;}
    }

    public static async Task InstallSdkAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(ToolFolder);string zip=Path.Combine(Path.GetTempPath(),"FlyPet-Everything-SDK.zip");
        try
        {
            using var client=new HttpClient();await using var input=await client.GetStreamAsync("https://www.voidtools.com/Everything-SDK.zip",cancellationToken);await using(var output=File.Create(zip))await input.CopyToAsync(output,cancellationToken);
            string unpack=Path.Combine(ToolFolder,"unpack");if(Directory.Exists(unpack))Directory.Delete(unpack,true);ZipFile.ExtractToDirectory(zip,unpack);
            string dll=Directory.EnumerateFiles(unpack,Library,SearchOption.AllDirectories).FirstOrDefault()??throw new InvalidDataException("SDK 包未包含 Everything64.dll。");
            File.Copy(dll,LibraryPath,true);Directory.Delete(unpack,true);
        }
        finally{if(File.Exists(zip))File.Delete(zip);}
    }

    [DllImport(Library,CharSet=CharSet.Unicode)] static extern void Everything_SetSearchW(string value);
    [DllImport(Library)] static extern void Everything_SetMax(uint value);
    [DllImport(Library)] [return:MarshalAs(UnmanagedType.Bool)] static extern bool Everything_QueryW([MarshalAs(UnmanagedType.Bool)] bool wait);
    [DllImport(Library)] static extern uint Everything_GetNumResults();
    [DllImport(Library)] static extern IntPtr Everything_GetResultPathW(uint index);
    [DllImport(Library)] static extern IntPtr Everything_GetResultFileNameW(uint index);
    [DllImport(Library)] static extern uint Everything_GetLastError();
}
