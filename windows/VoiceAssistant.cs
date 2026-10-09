using System.Text.Json;
using NAudio.Wave;
using Vosk;

namespace FlyPet;

public sealed class VoiceAssistant : IDisposable
{
    readonly string modelPath;
    readonly Action<string> status;
    readonly Action<VoiceIntent> intent;
    readonly Action<bool> listening;
    readonly CancellationTokenSource cancellation=new();
    WaveInEvent? microphone;
    Model? model;
    VoskRecognizer? recognizer;
    DateTime commandDeadline=DateTime.MinValue;
    bool disposed;
    System.Threading.Timer? timeout;

    public VoiceAssistant(string modelPath,Action<string> status,Action<VoiceIntent> intent,Action<bool> listening)
    {this.modelPath=modelPath;this.status=status;this.intent=intent;this.listening=listening;}

    public void Start()=>_ = Task.Run(Initialize);

    void Initialize()
    {
        try
        {
            if(!Directory.Exists(modelPath)){status("本地语音模型未安装。请运行包内的 Install-OfflineVoiceModel.ps1。");return;}
            Vosk.Vosk.SetLogLevel(-1);
            model=new Model(modelPath);
            recognizer=new VoskRecognizer(model,16000.0f);
            microphone=new WaveInEvent{WaveFormat=new WaveFormat(16000,16,1),BufferMilliseconds=160};
            microphone.DataAvailable+=OnAudio;
            microphone.RecordingStopped+=(_,e)=>{if(e.Exception!=null&&!disposed)status("麦克风已停止："+e.Exception.Message);};
            if(cancellation.IsCancellationRequested)return;
            microphone.StartRecording();status("本地语音控制已开启：说“强强”或“蝇蝇”后再说指令。");
        }
        catch(Exception e) when(e is not OutOfMemoryException){status("无法启动本地语音控制："+e.Message);}
    }

    void OnAudio(object? sender,WaveInEventArgs e)
    {
        try
        {
            var current=recognizer;if(current==null||cancellation.IsCancellationRequested)return;
            if(!current.AcceptWaveform(e.Buffer,e.BytesRecorded))return;
            using var result=JsonDocument.Parse(current.Result());
            if(!result.RootElement.TryGetProperty("text",out var textNode))return;
            HandleText(textNode.GetString()??"");
        }
        catch(Exception ex) when(!disposed){status("语音识别已暂停："+ex.Message);}
    }

    void HandleText(string text)
    {
        if(string.IsNullOrWhiteSpace(text))return;
        var now=DateTime.UtcNow;
        if(VoiceCommandParser.TrySplitWakeWord(text,out var inline))
        {
            commandDeadline=now.AddSeconds(5);timeout?.Dispose();timeout=new System.Threading.Timer(_=>CancelCommandWindow(),null,TimeSpan.FromSeconds(5),Timeout.InfiniteTimeSpan);listening(true);status("已唤醒，等待“搜索文件…”或“打开…”。");
            if(!string.IsNullOrWhiteSpace(inline))Dispatch(inline);
            return;
        }
        if(now<=commandDeadline)Dispatch(text);
    }

    void Dispatch(string text)
    {
        var parsed=VoiceCommandParser.Parse(text);
        if(parsed==null){status("没有听懂指令。可以说“搜索文件 xxx”或“打开微信”。");return;}
        CloseCommandWindow();intent(parsed);
    }

    void CancelCommandWindow(){if(commandDeadline==DateTime.MinValue)return;CloseCommandWindow();status("5 秒内未收到指令，已取消聆听。");}
    void CloseCommandWindow(){commandDeadline=DateTime.MinValue;timeout?.Dispose();timeout=null;listening(false);}

    public void Dispose()
    {
        if(disposed)return;disposed=true;CloseCommandWindow();cancellation.Cancel();
        if(microphone!=null){microphone.DataAvailable-=OnAudio;try{microphone.StopRecording();}catch{}microphone.Dispose();}
        recognizer?.Dispose();model?.Dispose();cancellation.Dispose();
    }
}
