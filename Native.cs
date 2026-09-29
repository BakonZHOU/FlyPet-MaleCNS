using System.ComponentModel;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace FlyPet;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] public struct POINT {public int X,Y;public POINT(int x,int y){X=x;Y=y;}}
    [StructLayout(LayoutKind.Sequential)] public struct SIZE {public int X,Y;public SIZE(int x,int y){X=x;Y=y;}}
    [StructLayout(LayoutKind.Sequential,Pack=1)] public struct BLEND {public byte Op,Flags,Alpha,Format;}
    [StructLayout(LayoutKind.Sequential)] public struct MouseData {public POINT Point; public uint Data,Flags,Time; public nuint Extra;}
    public delegate nint HookProc(int code,nint w,nint l);
    [DllImport("user32.dll")] public static extern nint GetDC(nint window);
    [DllImport("user32.dll")] public static extern int ReleaseDC(nint window,nint dc);
    [DllImport("gdi32.dll")] public static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] public static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll")] public static extern nint SelectObject(nint dc,nint obj);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(nint obj);
    [DllImport("user32.dll",SetLastError=true)] public static extern bool UpdateLayeredWindow(nint w,nint dest,ref POINT p,ref SIZE size,nint src,ref POINT sp,uint key,ref BLEND blend,uint flags);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(nint w,nint after,int x,int y,int cx,int cy,uint flags);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll",SetLastError=true)] public static extern nint SetWindowsHookEx(int type,HookProc proc,nint module,uint thread);
    [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] public static extern nint CallNextHookEx(nint hook,int code,nint w,nint l);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] public static extern nint GetModuleHandle(string? name);
    [DllImport("user32.dll")] public static extern bool DestroyIcon(nint icon);
    [DllImport("winmm.dll")] public static extern uint timeBeginPeriod(uint ms);
    [DllImport("winmm.dll")] public static extern uint timeEndPeriod(uint ms);
    [DllImport("user32.dll")] public static extern bool PostMessage(nint hwnd,uint msg,nint w,nint l);
}

public sealed class LayerWindow : Form
{
    Bitmap canvas;
    public LayerWindow(int size,string name):this(size,size,name){}
    public LayerWindow(int width,int height,string name)
    {
        Text=name;FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;AutoScaleMode=AutoScaleMode.None;
        StartPosition=FormStartPosition.Manual;Size=new(width,height);canvas=new(width,height,PixelFormat.Format32bppPArgb);
    }
    protected override bool ShowWithoutActivation=>true;
    protected override CreateParams CreateParams {get{var p=base.CreateParams;p.ExStyle|=0x80000|0x20|0x80|0x08000000;return p;}}
    public void Render(int x,int y,int size,Action<Graphics> draw)
        =>Render(x,y,size,size,draw);
    public void Render(int x,int y,int width,int height,Action<Graphics> draw)
    {
        if(canvas.Width!=width||canvas.Height!=height)
        {
            canvas.Dispose();canvas=new(width,height,PixelFormat.Format32bppPArgb);
            // A layered window's bitmap and native bounds must grow together. A
            // managed Size change alone can leave the old hit/placement bounds.
            SetBounds(x,y,width,height,BoundsSpecified.All);
            Native.SetWindowPos(Handle,0,x,y,width,height,0x0004|0x0010);
        }
        using(var g=Graphics.FromImage(canvas)){g.Clear(Color.Transparent);draw(g);}
        nint screen=Native.GetDC(0),mem=Native.CreateCompatibleDC(screen),bmp=canvas.GetHbitmap(Color.FromArgb(0)),old=Native.SelectObject(mem,bmp);
        try
        {
            var point=new Native.POINT(x,y);var source=new Native.POINT();var dimensions=new Native.SIZE(width,height);var blend=new Native.BLEND{Alpha=255,Format=1};
            if(!Native.UpdateLayeredWindow(Handle,screen,ref point,ref dimensions,mem,ref source,0,ref blend,2))throw new Win32Exception();
        }
        finally {Native.SelectObject(mem,old);Native.DeleteObject(bmp);Native.DeleteDC(mem);Native.ReleaseDC(0,screen);}
    }
    protected override void Dispose(bool disposing){if(disposing)canvas.Dispose();base.Dispose(disposing);}
}

public sealed class MouseHook : IDisposable
{
    readonly Native.HookProc proc;
    readonly nint handle;
    readonly Func<Point,bool> onDown;
    bool consumeRelease;
    public MouseHook(Func<Point,bool> handler)
    {
        onDown=handler;proc=Callback;handle=Native.SetWindowsHookEx(14,proc,Native.GetModuleHandle(null),0);
        if(handle==0)throw new Win32Exception(Marshal.GetLastWin32Error(),"无法启用鼠标交互");
    }
    nint Callback(int code,nint w,nint l)
    {
        if(code>=0)
        {
            if(w==0x201){var d=Marshal.PtrToStructure<Native.MouseData>(l);consumeRelease=onDown(new(d.Point.X,d.Point.Y));if(consumeRelease)return 1;}
            if(w==0x202&&consumeRelease){consumeRelease=false;return 1;}
        }
        return Native.CallNextHookEx(handle,code,w,l);
    }
    public void Dispose()=>Native.UnhookWindowsHookEx(handle);
}
