using System.Drawing.Drawing2D;

namespace FlyPet;

// A compact grimy choice card for the corpse, kept separate from the tray UI.
public sealed class DeathMenuWindow : Form
{
    readonly Button revive,clean;
    public DeathMenuWindow(Action onRevive,Action onClean)
    {
        FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;
        AutoScaleMode=AutoScaleMode.None;ClientSize=new(292,62);BackColor=Color.FromArgb(22,23,20);DoubleBuffered=true;
        // Give the longer Chinese label enough room and use a CJK font with stable metrics.
        revive=Choice("复活吧我的爱人!",new(8,9),new(202,44),onRevive,Color.FromArgb(183,194,133));
        clean=Choice("清理",new(218,9),new(66,44),onClean,Color.FromArgb(187,113,92));
        Controls.Add(revive);Controls.Add(clean);Paint+=PaintCard;
    }
    Button Choice(string text,Point location,Size size,Action action,Color accent)
    {
        var b=new Button{Text=text,Location=location,Size=size,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(37,39,32),ForeColor=accent,Font=new Font("Microsoft YaHei UI",10,FontStyle.Bold),TextAlign=ContentAlignment.MiddleCenter,Padding=Padding.Empty,UseCompatibleTextRendering=true,TabStop=false,Cursor=Cursors.Hand};
        b.UseVisualStyleBackColor=false;b.FlatAppearance.BorderSize=1;b.FlatAppearance.BorderColor=Color.FromArgb(92,82,60);b.FlatAppearance.MouseOverBackColor=Color.FromArgb(58,51,39);b.Click+=(_,_)=>{Hide();action();};return b;
    }
    void PaintCard(object? sender,PaintEventArgs e)
    {
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.None;
        using var border=new Pen(Color.FromArgb(132,108,72),2);g.DrawRectangle(border,1,1,Width-3,Height-3);
        using var inner=new Pen(Color.FromArgb(55,60,48),1);g.DrawRectangle(inner,5,5,Width-11,Height-11);
    }
    public void ShowAt(Rectangle area,Point corpse)
    {
        int x=Math.Clamp(corpse.X-ClientSize.Width/2,area.Left+8,area.Right-ClientSize.Width-8);
        int y=Math.Clamp(corpse.Y-ClientSize.Height-18,area.Top+8,area.Bottom-ClientSize.Height-8);
        Location=new(x,y);if(!Visible)Show();BringToFront();
    }
}
