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
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
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
    public void MoveTo(int x,int y)
        =>Native.SetWindowPos(Handle,0,x,y,0,0,0x0001|0x0004|0x0010);
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
    readonly Func<Point,Point?>? onMove;
    readonly Func<Point,bool>? onRightDown;
    readonly Action<Point>? onLeftUp;
    bool consumeLeftRelease,consumeRightRelease;
    bool correctingMove;
    Point? expectedCorrection;
    public MouseHook(Func<Point,bool> handler,Func<Point,Point?>? moveHandler=null,Func<Point,bool>? rightHandler=null,Action<Point>? leftUpHandler=null)
    {
        onDown=handler;onMove=moveHandler;onRightDown=rightHandler;onLeftUp=leftUpHandler;proc=Callback;handle=Native.SetWindowsHookEx(14,proc,Native.GetModuleHandle(null),0);
        if(handle==0)throw new Win32Exception(Marshal.GetLastWin32Error(),"无法启用鼠标交互");
    }
    nint Callback(int code,nint w,nint l)
    {
        if(code>=0)
        {
            if(w==0x200&&onMove!=null)
            {
                var d=Marshal.PtrToStructure<Native.MouseData>(l);
                if(expectedCorrection is Point expected&&expected.X==d.Point.X&&expected.Y==d.Point.Y)
                {expectedCorrection=null;return Native.CallNextHookEx(handle,code,w,l);}
                expectedCorrection=null;
                // SetCursorPos emits an injected move. Pass it through unchanged.
                if(!correctingMove&&(d.Flags&1)==0&&onMove(new(d.Point.X,d.Point.Y)) is Point slowed)
                {
                    correctingMove=true;
                    try
                    {
                        if(slowed.X!=d.Point.X||slowed.Y!=d.Point.Y)
                        {expectedCorrection=slowed;Native.SetCursorPos(slowed.X,slowed.Y);}
                    }
                    finally{correctingMove=false;}
                    return 1;
                }
            }
            if(w==0x201){var d=Marshal.PtrToStructure<Native.MouseData>(l);consumeLeftRelease=onDown(new(d.Point.X,d.Point.Y));if(consumeLeftRelease)return 1;}
            if(w==0x202)
            {
                var d=Marshal.PtrToStructure<Native.MouseData>(l);onLeftUp?.Invoke(new(d.Point.X,d.Point.Y));
                if(consumeLeftRelease){consumeLeftRelease=false;return 1;}
            }
            if(w==0x204&&onRightDown!=null){var d=Marshal.PtrToStructure<Native.MouseData>(l);consumeRightRelease=onRightDown(new(d.Point.X,d.Point.Y));if(consumeRightRelease)return 1;}
            if(w==0x205&&consumeRightRelease){consumeRightRelease=false;return 1;}
        }
        return Native.CallNextHookEx(handle,code,w,l);
    }
    public void Dispose()=>Native.UnhookWindowsHookEx(handle);
}
