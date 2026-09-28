using System.Drawing.Drawing2D;

namespace FlyPet;

public sealed class BrainMapWindow : Form
{
    readonly PetApplication app;
    readonly SomaMap map;
    readonly ActivityTimeline timeline;
    readonly Label details,summary;
    readonly ComboBox view;
    readonly CheckBox synapses,edge,neural;
    int selected=-1;
    readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();
    double lastSample;
    public BrainMapWindow(PetApplication app)
    {
        this.app=app;Theme.Form(this);Text="FlyPet · 大脑神经元活动图";ClientSize=new(1190,760);MinimumSize=new(1190,760);StartPosition=FormStartPosition.CenterScreen;
        Controls.Add(Theme.Label("MaleCNS 神经元活动",22,15,640,40,19,Theme.Accent));
        Controls.Add(Theme.Label("点表示原始数据标注的胞体位置；亮度是本程序计算的近期放电率。XY/XZ 是解剖投影，不是手绘示意。",22,58,1100,28,9,Theme.Muted));
        view=new ComboBox{Location=new(25,94),Size=new(180,28),DropDownStyle=ComboBoxStyle.DropDownList,BackColor=Theme.Panel,ForeColor=Theme.Text};
        view.Items.AddRange(["背视 XY","侧视 XZ"]);view.SelectedIndex=0;Controls.Add(view);
        synapses=new CheckBox{Text="突触传播",Checked=app.Settings.NeuralGain>0,Location=new(230,93),Size=new(110,29)};
        synapses.CheckedChanged+=(_,_)=>{app.Settings.NeuralGain=synapses.Checked?1:0;app.Settings.Save();};Controls.Add(synapses);
        edge=new CheckBox{Text="边缘感觉输入",Checked=app.Settings.EdgeSensing,Location=new(350,93),Size=new(140,29)};
        edge.CheckedChanged+=(_,_)=>{app.Settings.EdgeSensing=edge.Checked;app.Settings.Save();};Controls.Add(edge);
        neural=new CheckBox{Text="神经转向读出",Checked=app.Settings.NeuralSteering,Location=new(500,93),Size=new(140,29)};
        neural.CheckedChanged+=(_,_)=>{app.Settings.NeuralSteering=neural.Checked;app.Settings.Save();};Controls.Add(neural);
        Controls.Add(Theme.Label("滚轮缩放 · 拖动平移 · 点击节点查看 ID",650,94,480,28,9,Theme.Muted));
        map=new SomaMap(app){Location=new(22,134),Size=new(837,409)};map.NodeSelected+=index=>{selected=index;UpdateDetails();};Controls.Add(map);
        view.SelectedIndexChanged+=(_,_)=>{map.View=view.SelectedIndex;map.Invalidate();};
        details=Theme.Label("点击亮点或灰点查看单个神经元",877,141,282,310,9,Theme.Muted);Controls.Add(details);
        summary=Theme.Label("",877,452,280,86,9,Theme.Accent);Controls.Add(summary);
        Controls.Add(Theme.Label("最近 60 秒 · 各组放电率热图（每格约 100 ms）",22,552,800,26,10));
        timeline=new ActivityTimeline(app){Location=new(22,580),Size=new(1137,130)};Controls.Add(timeline);
        Controls.Add(Theme.Label("按图选择神经元可追踪最强输入与输出连接；右下角托盘另有确定性连接消融对照。亮起并不等于生物学功能得到验证。",22,719,1125,24,9,Theme.Muted));
        ResumeLayout(false);
    }
    public void RefreshActivity()
    {
        if(IsDisposed||!Visible||clock.Elapsed.TotalSeconds-lastSample<.1)return;
        lastSample=clock.Elapsed.TotalSeconds;timeline.Record();map.Invalidate();timeline.Invalidate();
        if(selected>=0)UpdateDetails();
        var b=app.Sim.Brain;summary.Text=$"神经元 {b.NeuronCount:N0} · 连接 {b.EdgeCount:N0}\n胞体坐标 {(app.Circuit.Nodes.Count(n=>n.Soma?.Length==3)):N0} 个\n视觉 {b.VisualRate:0.0} Hz · 嗅觉 {b.OlfactoryRate:0.0} Hz\n记忆置信 {b.MemoryConfidence:0.00} · 奖励 {b.RewardSignal:+0.00;-0.00;0.00}";
    }
    void UpdateDetails()
    {
        var n=app.Circuit.Nodes[selected];var edges=app.Circuit.Edges;
        var incoming=edges.Where(e=>e[1]==selected).OrderByDescending(e=>Math.Abs(e[2])).Take(5).Select(e=>$"  ← {app.Circuit.Nodes[e[0]].Type} {app.Circuit.Nodes[e[0]].BodyId}  {e[2]:+0;-0}");
        var outgoing=edges.Where(e=>e[0]==selected).OrderByDescending(e=>Math.Abs(e[2])).Take(5).Select(e=>$"  → {app.Circuit.Nodes[e[1]].Type} {app.Circuit.Nodes[e[1]].BodyId}  {e[2]:+0;-0}");
        details.Text=$"bodyId {n.BodyId}\n类型 {n.Type}\n侧别 {n.Side}　递质 {n.Nt}\n分组 {string.Join(", ",n.Groups)}\n胞体 [{string.Join(", ",n.Soma??[])}]\n\n放电率 {app.Sim.Brain.GetRate(selected):0.0} Hz\n膜电位 {app.Sim.Brain.GetVoltage(selected):0.00} mV\n\n最强输入：\n{string.Join("\n",incoming)}\n\n最强输出：\n{string.Join("\n",outgoing)}";
        map.Selected=selected;map.Invalidate();
    }
}

public sealed class SomaMap : Control
{
    readonly PetApplication app;
    readonly PointF[] points;
    readonly Color[] colors;
    public event Action<int>? NodeSelected;
    public int Selected=-1,View;
    float zoom=1,panX,panY;
    Point? drag;
    public SomaMap(PetApplication app)
    {
        this.app=app;DoubleBuffered=true;BackColor=Color.FromArgb(17,20,19);
        points=new PointF[app.Circuit.Nodes.Length];colors=new Color[points.Length];
        for(int i=0;i<points.Length;i++)colors[i]=GroupColor(app.Circuit.Nodes[i].Groups);
        MouseWheel+=(_,e)=>{zoom=Math.Clamp(zoom*(e.Delta>0?1.2f:1/1.2f),1,10);Invalidate();};
        MouseDown+=(_,e)=>{if(e.Button==MouseButtons.Left)drag=e.Location;};
        MouseMove+=(_,e)=>{if(drag is Point d && e.Button==MouseButtons.Left){panX+=e.X-d.X;panY+=e.Y-d.Y;drag=e.Location;Invalidate();}};
        MouseUp+=(_,e)=>{if(drag is Point d&&Math.Abs(e.X-d.X)+Math.Abs(e.Y-d.Y)<4)Pick(e.Location);drag=null;};
    }
    static Color GroupColor(string[] groups)
    {
        if(groups.Contains("reward"))return Color.FromArgb(234,104,146);
        if(groups.Contains("memory"))return Color.FromArgb(218,170,72);
        if(groups.Contains("olfactory"))return Color.FromArgb(112,205,126);
        if(groups.Contains("navigation"))return Color.FromArgb(91,197,205);
        if(groups.Contains("descending"))return Color.FromArgb(224,89,68);
        if(groups.Contains("motor"))return Color.FromArgb(84,139,222);
        if(groups.Contains("visual"))return Color.FromArgb(175,193,100);
        if(groups.Contains("taste"))return Color.FromArgb(210,177,87);
        if(groups.Contains("escape"))return Color.FromArgb(227,73,58);
        if(groups.Contains("flight"))return Color.FromArgb(80,186,227);
        if(groups.Contains("wing"))return Color.FromArgb(93,141,195);
        if(groups.Contains("steer")||groups.Contains("optomotor"))return Color.FromArgb(189,126,221);
        if(groups.Contains("feed"))return Color.FromArgb(117,197,119);
        return Color.FromArgb(109,116,105);
    }
    void Pick(Point p)
    {
        int hit=-1;float closest=144;
        for(int i=0;i<points.Length;i++){float dx=p.X-points[i].X,dy=p.Y-points[i].Y,d=dx*dx+dy*dy;if(d<closest){closest=d;hit=i;}}
        if(hit>=0){Selected=hit;NodeSelected?.Invoke(hit);}
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);var g=e.Graphics;var nodes=app.Circuit.Nodes;
        long minX=long.MaxValue,maxX=long.MinValue,minY=long.MaxValue,maxY=long.MinValue;
        foreach(var n in nodes)if(n.Soma?.Length==3){minX=Math.Min(minX,n.Soma[0]);maxX=Math.Max(maxX,n.Soma[0]);long y=n.Soma[View==0?1:2];minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);}
        float sx=(Width-50f)/Math.Max(1,maxX-minX),sy=(Height-48f)/Math.Max(1,maxY-minY),scale=Math.Min(sx,sy);
        using var grid=new Pen(Color.FromArgb(34,43,42,38));for(int x=0;x<Width;x+=40)g.DrawLine(grid,x,0,x,Height);for(int y=0;y<Height;y+=40)g.DrawLine(grid,0,y,Width,y);
        using var caption=new Font("Consolas",8);using var captionBrush=new SolidBrush(Theme.Muted);
        g.DrawString(View==0?"SOMA XY / DORSAL":"SOMA XZ / LATERAL",caption,captionBrush,12,9);
        for(int i=0;i<nodes.Length;i++)
        {
            var loc=nodes[i].Soma;if(loc?.Length!=3){points[i]=new(-100,-100);continue;}
            float x=Width*.5f+((loc[0]-(minX+maxX)*.5f)*scale*zoom)+panX;
            float y=Height*.5f+((loc[View==0?1:2]-(minY+maxY)*.5f)*scale*zoom)+panY;
            points[i]=new(x,y);if(x<-10||x>Width+10||y<-10||y>Height+10)continue;
            float rate=app.Sim.Brain.GetRate(i);float size=i==Selected?10:Math.Clamp(2.2f+rate*.045f,2.2f,7f);
            var c=colors[i];int alpha=i==Selected?255:Math.Clamp((int)(56+rate*2),56,235);
            using var brush=new SolidBrush(Color.FromArgb(alpha,c));g.FillEllipse(brush,x-size/2,y-size/2,size,size);
            if(i==Selected){using var ring=new Pen(Theme.Text,1.5f);g.DrawEllipse(ring,x-8,y-8,16,16);}
        }
        using var legend=new Font("Consolas",8);using var legendBrush=new SolidBrush(Theme.Muted);
        g.DrawString("视觉  嗅觉  记忆/奖励  导航  下行/运动  甜味  中间神经元",legend,legendBrush,13,Height-24);
    }
}

public sealed class ActivityTimeline : Control
{
    readonly PetApplication app;
    readonly float[,] values=new float[9,600];
    readonly SolidBrush[] palette;
    int head,count;
    static readonly string[] names=["视觉","嗅觉","记忆","奖励","导航","逃逸","下行","运动","甜味"];
    public ActivityTimeline(PetApplication app)
    {
        this.app=app;DoubleBuffered=true;BackColor=Theme.Panel;
        palette=Enumerable.Range(0,64).Select(i=>new SolidBrush(Color.FromArgb(19+i*3,28+i*2,25+i))).ToArray();
    }
    public void Record()
    {
        var b=app.Sim.Brain;float[] v=[b.VisualRate,b.OlfactoryRate,b.MemoryRate,b.RewardRate,b.NavigationRate,b.EscapeRate,b.DescendingRate,b.MotorRate,b.TasteRate];
        for(int i=0;i<v.Length;i++)values[i,head]=Math.Clamp(v[i]/30,0,1);
        head=(head+1)%600;count=Math.Min(600,count+1);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);var g=e.Graphics;using var label=Theme.Font(8);using var ink=new SolidBrush(Theme.Text);
        float chartX=103,chartW=Width-114,cellW=chartW/600f;
        for(int row=0;row<9;row++)
        {
            float y=3+row*13.6f;g.DrawString(names[row],label,ink,6,y-2);
            for(int k=0;k<count;k++)
            {
                int index=(head-count+k+600)%600;int intensity=Math.Clamp((int)(values[row,index]*63),0,63);
                g.FillRectangle(palette[intensity],chartX+(600-count+k)*cellW,y,Math.Max(1,cellW+.2f),11);
            }
        }
        using var axis=new Pen(Color.FromArgb(65,186,192,164));g.DrawLine(axis,chartX+chartW-1,0,chartX+chartW-1,Height);
    }
    protected override void Dispose(bool disposing){if(disposing)foreach(var p in palette)p.Dispose();base.Dispose(disposing);}
}
