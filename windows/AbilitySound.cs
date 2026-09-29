using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace FlyPet;

// Independent PCM voices prevent a new ability sound from cutting off an older one.
public sealed class AbilitySound : IDisposable
{
    readonly WaveClip dodgeCast,solid,warm;
    readonly WaveClip? dodgeHit;
    readonly System.Threading.Timer warmTimer;
    long lastEffectTick;
    public AbilitySound()
    {
        string evasionFile=Path.Combine(Settings.Folder,"evanescence.wav");
        string solidFile=Path.Combine(Settings.Folder,"rock-solid.wav");
        var evasionOverride=LoadFile(evasionFile);
        dodgeCast=evasionOverride??LoadResource("evanescence-cast.wav")??WaveClip.FromWav(Make(false));
        dodgeHit=evasionOverride is null?LoadResource("evanescence-hit.wav")??WaveClip.FromWav(Make(false)):null;
        solid=LoadFile(solidFile)??LoadResource("rock-solid.wav")??WaveClip.FromWav(Make(true));
        warm=WaveClip.FromWav(MakeWarm());
        warm.Play();lastEffectTick=Environment.TickCount64;
        warmTimer=new(_=>WarmAudio(),null,2000,2000);
    }
    static WaveClip? LoadFile(string path){try{return File.Exists(path)?WaveClip.FromBytes(File.ReadAllBytes(path)):null;}catch{return null;}}
    WaveClip? LoadResource(string file)
    {
        try{using var resource=GetType().Assembly.GetManifestResourceStream($"FlyPet.Assets.Audio.{file}");if(resource is null)return null;using var data=new MemoryStream();resource.CopyTo(data);return WaveClip.FromBytes(data.ToArray());}
        catch{return null;}
    }
    public void Play(DefenseMove move)
    {
        if(move==DefenseMove.None)return;lastEffectTick=Environment.TickCount64;
        // Device/voice creation may block inside an audio driver. Never perform it
        // on the WinForms animation thread.
        ThreadPool.QueueUserWorkItem(_=>
        {
            try{if(move==DefenseMove.Evanescence){dodgeCast.Play();dodgeHit?.Play();}else if(move==DefenseMove.RockSolid)solid.Play();}catch(InvalidOperationException){}
        });
    }
    void WarmAudio(){if(Environment.TickCount64-lastEffectTick>=3200)warm.Play();}
    internal void StressVoicesForTest(int count){for(int i=0;i<count;i++)warm.Play();}
    static MemoryStream MakeWarm()
    {
        const int rate=22050,count=880;var stream=new MemoryStream(44+count*2);
        using(var writer=new BinaryWriter(stream,System.Text.Encoding.ASCII,true)){writer.Write("RIFF"u8);writer.Write(36+count*2);writer.Write("WAVEfmt "u8);writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);writer.Write("data"u8);writer.Write(count*2);for(int i=0;i<count;i++)writer.Write((short)((i&1)==0?1:-1));}
        stream.Position=0;return stream;
    }
    static MemoryStream Make(bool metallic)
    {
        const int rate=22050;int count=(int)(rate*(metallic?.50:.62));var stream=new MemoryStream(44+count*2);
        using(var writer=new BinaryWriter(stream,System.Text.Encoding.ASCII,true))
        {
            writer.Write("RIFF"u8);writer.Write(36+count*2);writer.Write("WAVEfmt "u8);writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);writer.Write("data"u8);writer.Write(count*2);
            var random=new Random(metallic?4281:7174);float filtered=0;
            for(int i=0;i<count;i++){double t=(double)i/rate,p=(double)i/count,noise=random.NextDouble()*2-1;filtered=filtered*.82f+(float)noise*.18f;double sample;if(metallic){double strike=Math.Exp(-t*15),ring=Math.Exp(-t*5.7);sample=.52*strike*noise+.30*ring*Math.Sin(2*Math.PI*(610*t+180*t*t))+.23*ring*Math.Sin(2*Math.PI*1049*t)+.18*Math.Exp(-t*10)*Math.Sin(2*Math.PI*1831*t);}else{double envelope=Math.Sin(Math.PI*p)*Math.Exp(-p*.75),phase=2*Math.PI*(110*t+500*t*t);sample=envelope*(.43*filtered+.29*Math.Sin(phase)+.19*Math.Sin(phase*1.51))+.12*Math.Exp(-t*18)*Math.Sin(2*Math.PI*950*t);}writer.Write((short)(Math.Clamp(sample,-1,1)*12500));}
        }
        stream.Position=0;return stream;
    }
    public void Dispose(){warmTimer.Dispose();dodgeCast.Dispose();dodgeHit?.Dispose();solid.Dispose();warm.Dispose();}

    sealed class WaveClip : IDisposable
    {
        readonly byte[] pcm;WAVEFORMATEX format;readonly ConcurrentDictionary<Voice,byte> voices=[];bool disposed;
        WaveClip(byte[] pcm,WAVEFORMATEX format){this.pcm=pcm;this.format=format;}
        public static WaveClip FromWav(Stream stream)=>FromBytes(ReadAll(stream));
        public static WaveClip FromBytes(byte[] bytes)
        {
            int fmt=-1,data=-1,fmtSize=0,dataSize=0;
            for(int at=12;at+8<=bytes.Length;){string id=System.Text.Encoding.ASCII.GetString(bytes,at,4);int size=BitConverter.ToInt32(bytes,at+4);if(size<0||at+8+size>bytes.Length)break;if(id=="fmt "){fmt=at+8;fmtSize=size;}if(id=="data"){data=at+8;dataSize=size;break;}at+=8+size+(size&1);}
            if(fmt<0||data<0||fmtSize<16)throw new InvalidDataException("不是可播放的 PCM WAV");
            var f=new WAVEFORMATEX{wFormatTag=BitConverter.ToUInt16(bytes,fmt),nChannels=BitConverter.ToUInt16(bytes,fmt+2),nSamplesPerSec=BitConverter.ToUInt32(bytes,fmt+4),nAvgBytesPerSec=BitConverter.ToUInt32(bytes,fmt+8),nBlockAlign=BitConverter.ToUInt16(bytes,fmt+12),wBitsPerSample=BitConverter.ToUInt16(bytes,fmt+14),cbSize=0};
            if(f.wFormatTag!=1||f.wBitsPerSample!=16||f.nChannels==0)throw new InvalidDataException("只支持 PCM16 WAV");
            var source=new byte[dataSize];Buffer.BlockCopy(bytes,data,source,0,dataSize);
            if(f.nChannels==2)return new WaveClip(source,f);
            int channels=f.nChannels,frames=source.Length/(channels*2);var stereo=new byte[frames*4];
            int Sample(int frame,int channel)=>BitConverter.ToInt16(source,(frame*channels+Math.Min(channel,channels-1))*2);
            for(int frame=0;frame<frames;frame++)
            {
                float left,right;
                if(channels==1)left=right=Sample(frame,0);
                else
                {
                    float center=channels>2?Sample(frame,2):0,lfe=channels>3?Sample(frame,3):0;
                    left=Sample(frame,0)+center*.62f+lfe*.10f+(channels>4?Sample(frame,4)*.32f:0);
                    right=Sample(frame,1)+center*.62f+lfe*.10f+(channels>5?Sample(frame,5)*.32f:0);
                    left*=.56f;right*=.56f;
                }
                short l=(short)Math.Clamp((int)left,short.MinValue,short.MaxValue),r=(short)Math.Clamp((int)right,short.MinValue,short.MaxValue);
                stereo[frame*4]=(byte)l;stereo[frame*4+1]=(byte)(l>>8);stereo[frame*4+2]=(byte)r;stereo[frame*4+3]=(byte)(r>>8);
            }
            f.nChannels=2;f.nBlockAlign=4;f.nAvgBytesPerSec=f.nSamplesPerSec*4;return new WaveClip(stereo,f);
        }
        static byte[] ReadAll(Stream s){using var m=new MemoryStream();s.CopyTo(m);return m.ToArray();}
        public void Play(){if(disposed||voices.Count>=8)return;var voice=new Voice(this,pcm,format);voices.TryAdd(voice,0);voice.Start();}
        public void StopAll(){foreach(var v in voices.Keys)v.Stop();}
        void Remove(Voice v)=>voices.TryRemove(v,out _);
        public void Dispose(){disposed=true;StopAll();}
        [StructLayout(LayoutKind.Sequential)]internal struct WAVEFORMATEX{public ushort wFormatTag,nChannels;public uint nSamplesPerSec,nAvgBytesPerSec;public ushort nBlockAlign,wBitsPerSample,cbSize;}
        [StructLayout(LayoutKind.Sequential)]internal struct WAVEHDR{public nint lpData;public uint dwBufferLength,dwBytesRecorded;public nint dwUser;public uint dwFlags,dwLoops;public nint lpNext,dwReserved;}
        sealed class Voice
        {
            readonly WaveClip owner;readonly byte[] data;WAVEFORMATEX format;nint dataPointer,headerPointer,handle;int stopped;
            public Voice(WaveClip owner,byte[] data,WAVEFORMATEX format){this.owner=owner;this.data=data;this.format=format;}
            public void Start()
            {
                try
                {
                    dataPointer=Marshal.AllocHGlobal(data.Length);Marshal.Copy(data,0,dataPointer,data.Length);
                    int headerSize=Marshal.SizeOf<WAVEHDR>();headerPointer=Marshal.AllocHGlobal(headerSize);
                    Marshal.StructureToPtr(new WAVEHDR{lpData=dataPointer,dwBufferLength=(uint)data.Length},headerPointer,false);
                    if(waveOutOpen(out handle,0xffffffff,ref format,0,0,0)!=0)throw new InvalidOperationException();
                    if(waveOutPrepareHeader(handle,headerPointer,(uint)headerSize)!=0||waveOutWrite(handle,headerPointer,(uint)headerSize)!=0)throw new InvalidOperationException();
                    _ = Finish((int)Math.Clamp((double)data.Length/(format.nAvgBytesPerSec)*1000+100,100,60000));
                }
                catch{Stop();}
            }
            async Task Finish(int ms){await Task.Delay(ms).ConfigureAwait(false);Stop();}
            public void Stop()
            {
                if(Interlocked.Exchange(ref stopped,1)!=0)return;
                try{if(handle!=0){waveOutReset(handle);if(headerPointer!=0)waveOutUnprepareHeader(handle,headerPointer,(uint)Marshal.SizeOf<WAVEHDR>());waveOutClose(handle);handle=0;}}catch{}
                if(headerPointer!=0){Marshal.FreeHGlobal(headerPointer);headerPointer=0;}
                if(dataPointer!=0){Marshal.FreeHGlobal(dataPointer);dataPointer=0;}
                owner.Remove(this);
            }
        }
        [DllImport("winmm.dll")]static extern int waveOutOpen(out nint hwo,uint uDeviceID,ref WAVEFORMATEX pwfx,nint dwCallback,nint dwInstance,uint fdwOpen);
        [DllImport("winmm.dll")]static extern int waveOutPrepareHeader(nint hwo,nint pwh,uint cbwh);
        [DllImport("winmm.dll")]static extern int waveOutWrite(nint hwo,nint pwh,uint cbwh);
        [DllImport("winmm.dll")]static extern int waveOutReset(nint hwo);
        [DllImport("winmm.dll")]static extern int waveOutUnprepareHeader(nint hwo,nint pwh,uint cbwh);
        [DllImport("winmm.dll")]static extern int waveOutClose(nint hwo);
    }
}
