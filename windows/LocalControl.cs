using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;

namespace FlyPet;

// Current-user-only local pipe. No network listener, script execution or external dependencies.
public sealed class LocalControl : IDisposable
{
    public static string PipeName => "FlyPet.Desktop.v1."+Environment.UserName+"."+Process.GetCurrentProcess().SessionId;
    readonly CancellationTokenSource cancel=new();
    readonly Task server;
    public LocalControl(Control dispatch,Func<string[],object> handler)
    {
        server=Task.Run(async()=>
        {
            while(!cancel.IsCancellationRequested)
            {
                try
                {
                    await using var pipe=new NamedPipeServerStream(PipeName,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
                    await pipe.WaitForConnectionAsync(cancel.Token);
                    using var read=new StreamReader(pipe,leaveOpen:true);using var write=new StreamWriter(pipe,leaveOpen:true){AutoFlush=true};
                    using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancel.Token);timeout.CancelAfter(2000);
                    var line=await read.ReadLineAsync(timeout.Token);
                    if(line==null||line.Length>4096)continue;
                    var args=JsonSerializer.Deserialize<string[]>(line)??[];
                    var completion=new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
                    dispatch.BeginInvoke(()=>{try{completion.TrySetResult(handler(args));}catch(Exception e){completion.TrySetResult(new{ok=false,error=e.Message});}});
                    var response=await completion.Task.WaitAsync(timeout.Token);
                    await write.WriteLineAsync(JsonSerializer.Serialize(response));
                }
                catch(OperationCanceledException){}catch(IOException){}catch(ObjectDisposedException){}catch(InvalidOperationException){}catch(JsonException){}
            }
        });
    }
    public static string Send(string[] args)
    {
        using var pipe=new NamedPipeClientStream(".",PipeName,PipeDirection.InOut,PipeOptions.CurrentUserOnly);pipe.Connect(1500);
        using var write=new StreamWriter(pipe,leaveOpen:true){AutoFlush=true};using var read=new StreamReader(pipe,leaveOpen:true);
        write.WriteLine(JsonSerializer.Serialize(args));return read.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult()??"{}";
    }
    public void Dispose(){cancel.Cancel();}
}
