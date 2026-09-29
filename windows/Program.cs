using System.Diagnostics;

namespace FlyPet;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        if(args.Contains("--self-test"))return SelfTest.Run(args);
        if(args.Length>=2&&args[0]=="--command")
        {
            try
            {
                int outputIndex=Array.IndexOf(args,"--output");
                string response=LocalControl.Send(args.Skip(1).Take(outputIndex<0?args.Length-1:outputIndex-1).ToArray());
                if(outputIndex>=0&&outputIndex+1<args.Length)File.WriteAllText(args[outputIndex+1],response);
                return response.Contains("\"ok\":false")?1:0;
            }
            catch{return 2;}
        }
        using var mutex=new Mutex(true,@"Local\FlyPet.Desktop.v1",out bool first);
        if(!first)
        {
            try{LocalControl.Send(["menu"]);}catch{MessageBox.Show("FlyPet 已经在运行。请从右下角系统托盘打开控制中心。","FlyPet");}return 0;
        }
        void Log(Exception e)
        {
            Directory.CreateDirectory(Settings.Folder);File.AppendAllText(Path.Combine(Settings.Folder,"error.log"),$"{DateTimeOffset.Now:o} {e}\n");
        }
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        try{using var app=new PetApplication(args.Contains("--quiet"));Application.Run(app);return 0;}
        catch(Exception e){Log(e);MessageBox.Show("FlyPet 无法继续运行：\n"+e.Message+"\n\n详细日志："+Path.Combine(Settings.Folder,"error.log"),"FlyPet");return 1;}
    }
}
