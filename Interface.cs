using System.Diagnostics;
using System.Drawing.Drawing2D;
using Microsoft.Win32;

namespace FlyPet;

static class Theme
{
    public static Color Bg=Color.FromArgb(20,23,21),Panel=Color.FromArgb(29,33,29),Line=Color.FromArgb(58,64,53),Text=Color.FromArgb(227,230,210),Muted=Color.FromArgb(141,151,130),Accent=Color.FromArgb(198,216,138);
    public static Font Font(float size=10,FontStyle style=FontStyle.Regular)=>new("Microsoft YaHei UI",size,style);
    public static Label Label(string text,int x,int y,int w,int h,float size=10,Color? color=null)
        =>new(){Text=text,Location=new(x,y),Size=new(w,h),Font=Font(size),ForeColor=color??Text,BackColor=Color.Transparent};
    public static Button Button(string text,int x,int y,int w,Action action,bool primary=false)
    {
        var b=new Button{Text=text,Location=new(x,y),Size=new(w,38),FlatStyle=FlatStyle.Flat,Font=Font(),BackColor=primary?Accent:Panel,ForeColor=primary?Bg:Text,Cursor=Cursors.Hand};
        b.FlatAppearance.BorderColor=Line;b.Click+=(_,_)=>action();return b;
    }
    public static void Form(Form f){f.SuspendLayout();f.BackColor=Bg;f.ForeColor=Text;f.Font=Font();f.AutoScaleDimensions=new SizeF(96,96);f.AutoScaleMode=AutoScaleMode.Dpi;}
}

public sealed class PreviewPanel : Panel
{
    readonly FlyRenderer renderer=new();
    readonly PetApplication app;
    public PreviewPanel(PetApplication app){this.app=app;DoubleBuffered=true;BackColor=Theme.Panel;}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);var g=e.Graphics;
        using var grid=new Pen(Color.FromArgb(39,45,37));
        for(int x=0;x<Width;x+=24)g.DrawLine(grid,x,0,x,Height);
        for(int y=0;y<Height;y+=24)g.DrawLine(grid,0,y,Width,y);
        float scale=DeviceDpi/96f;
        using var line=new Pen(Theme.Line);g.DrawEllipse(line,Width/2-115*scale,Height/2-95*scale,230*scale,190*scale);
        var sim=app.Sim;int sprite=(int)(300*scale);renderer.Draw(g,new(Width/2-sprite/2,Height/2-(int)(162*scale),sprite,sprite),sim,app.Area,false);
        using var mono=new Font("Consolas",9);using var brush=new SolidBrush(Theme.Muted);
        g.DrawString("SPECIMEN 01  /  DROSOPHILA",mono,brush,16*scale,16*scale);
        g.DrawString("LOW POLY · LIVE NEURAL CIRCUIT",mono,brush,16*scale,Height-30*scale);
    }
    protected override void Dispose(bool disposing){if(disposing)renderer.Dispose();base.Dispose(disposing);}
}

public sealed class Dashboard : Form
{
    readonly PetApplication app;
    readonly Label status,metrics;
    readonly PreviewPanel preview;
    bool exiting;
    public Dashboard(PetApplication app)
    {
        this.app=app;Text="FlyPet · 果蝇桌宠 / 控制中心";Theme.Form(this);ClientSize=new(840,545);FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;
        Controls.Add(Theme.Label("FLYPET   /   01",28,23,500,34,19,Theme.Accent));
        Controls.Add(Theme.Label("一只住在桌面上的小苍蝇。",29,66,650,30,11,Theme.Muted));
        preview=new(app){Location=new(28,115),Size=new(360,328)};Controls.Add(preview);
        Controls.Add(Theme.Label("自由飞行，偶尔捣乱。",418,115,390,40,20));
        Controls.Add(Theme.Label("投下一粒糖，看它靠近。\n鼠标靠近时它会躲开，直接点击苍蝇即可击中。\n生命归零后，在随机的时间重新醒来。",420,171,390,86,11,Theme.Muted));
        status=Theme.Label("",420,272,390,34,13,Theme.Accent);Controls.Add(status);
        metrics=Theme.Label("",420,311,390,65,9,Theme.Muted);Controls.Add(metrics);
        Controls.Add(Theme.Button("放飞苍蝇  →",420,397,220,()=>{app.StartPet();Hide();},true));
        Controls.Add(Theme.Button("设置",650,397,158,()=>app.ShowSettings()));
        Controls.Add(Theme.Label("右下角托盘菜单管理全部功能  /  鼠标靠近会自动躲避",29,465,760,24,9,Theme.Muted));
        Controls.Add(Theme.Label("真实连接子图 + 可调行为模型  ·  CPU 实时运行  ·  本地离线",29,496,760,22,9,Theme.Muted));
        FormClosing+=(_,e)=>{if(!exiting){e.Cancel=true;Hide();}};ResumeLayout(false);
    }
    public void RefreshStatus()
    {
        var s=app.Sim;status.Text=s.Dead?$"等待复活 · {s.DeathRemaining:0} 秒":$"{s.Behavior}  ·  生命 {s.Health:0}%  /  饱腹 {s.Fullness:0}%";
        metrics.Text=$"{s.Brain.NeuronCount:N0} 神经元  /  {s.Brain.EdgeCount:N0} 连接\n模拟 {app.RealTimeRatio:0.00}×  ·  计算 {app.ComputeMs:0.00} ms/帧\n显示 {app.MeasuredFps:0} FPS  ·  {app.ModeLabel}";preview.Invalidate();
    }
    public void Shutdown(){exiting=true;Close();}
}

public sealed class NeuralEvidenceWindow : Form
{
    readonly PetApplication app;
    readonly EvidenceBars bars;
    readonly Label values,result;
    readonly Button run;
    public NeuralEvidenceWindow(PetApplication app)
    {
        this.app=app;Theme.Form(this);Text="FlyPet · 神经连接证据";ClientSize=new(760,610);MinimumSize=new(760,610);StartPosition=FormStartPosition.CenterScreen;
        Controls.Add(Theme.Label("连接在运行，但行为不是纯生物学涌现。",26,22,700,40,19,Theme.Accent));
        Controls.Add(Theme.Label("内置回路含 1,800 个 MaleCNS 神经元和 136,027 条连接。外界只写入视觉与甜味感觉群；飞行、转向和逃逸从下游群读取。当前缩减图未让 MN9 稳定放电，所以进食使用甜味 GRN 率作为公开的回退。血量、目标方向与复活仍是工程规则。",28,70,700,72,10,Theme.Muted));
        Controls.Add(Theme.Label("实时群体活动（相对放电率）",28,158,500,30,12));
        bars=new EvidenceBars(app){Location=new(28,194),Size=new(420,224)};Controls.Add(bars);
        values=Theme.Label("",470,194,260,224,9,Theme.Muted);Controls.Add(values);
        run=Theme.Button("运行连接消融对照",28,446,220,RunAudit,true);Controls.Add(run);
        result=Theme.Label("同一随机种子、同一糖和威胁输入；只把全部连接增益从 1 改为 0。\n这能验证代码中的感觉→连接→运动因果链，但不能证明该简化模型等同活体果蝇。",270,443,456,82,9,Theme.Muted);Controls.Add(result);
        Controls.Add(Theme.Label("可独立复核：Assets/circuit.json 保存 bodyId、类型和每条边；tools/extract_circuit.py 可从本地 MaleCNS 缓存重新生成；SelfTest 的 connectome 消融检查会在发布前运行。",28,548,700,45,9,Theme.Muted));
        ResumeLayout(false);
    }
    public void RefreshEvidence()
    {
        if(IsDisposed)return;var b=app.Sim.Brain;bars.Invalidate();
        values.Text=$"视觉 LC4/LPLC2     {b.VisualRate,7:0.0} Hz\n逃逸 DNp01          {b.EscapeRate,7:0.0} Hz\n飞行 DNg02/DNa08    {b.FlightRate,7:0.0} Hz\n翅肌 DLMn/DVMn      {b.WingRate,7:0.0} Hz\n甜味 GRN            {b.TasteRate,7:0.0} Hz\nMN9（仅观察）       {b.FeedRate,7:0.0} Hz\n\n累计放电            {b.TotalSpikes,10:N0}";
    }
    async void RunAudit()
    {
        run.Enabled=false;result.Text="正在运行两套相同输入的确定性模拟……";
        try
        {
            var a=await Task.Run(app.RunCausalAudit);
            result.ForeColor=a.Passed?Theme.Accent:Color.IndianRed;
            result.Text=$"{(a.Passed?"通过":"未通过")}：连接开启 / 全部切断\n位移 {a.ConnectedDistance:0.0} / {a.SeveredDistance:0.0} px　平均飞行输出 {a.ConnectedFlight:0.000} / {a.SeveredFlight:0.000}\n放电 {a.ConnectedSpikes:N0} / {a.SeveredSpikes:N0}　逃逸 {a.ConnectedFear:0.000} / {a.SeveredFear:0.000}　进食 {a.ConnectedFeed:0.000} / {a.SeveredFeed:0.000}";
        }
        catch(Exception e){result.ForeColor=Color.IndianRed;result.Text="对照运行失败："+e.Message;}
        finally{run.Enabled=true;}
    }
}

public sealed class EvidenceBars : Control
{
    readonly PetApplication app;
    public EvidenceBars(PetApplication app){this.app=app;DoubleBuffered=true;BackColor=Theme.Panel;}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);var b=app.Sim.Brain;
        var rows=new[]{("视觉输入",b.VisualRate,Color.FromArgb(184,177,102)),("逃逸",b.EscapeRate,Color.FromArgb(190,82,59)),("飞行",b.FlightRate,Color.FromArgb(116,157,184)),("翅肌",b.WingRate,Color.FromArgb(111,137,158)),("甜味输入",b.TasteRate,Color.FromArgb(194,166,99)),("进食",b.FeedRate,Color.FromArgb(133,174,103))};
        using var label=Theme.Font(9);using var text=new SolidBrush(Theme.Text);using var track=new SolidBrush(Color.FromArgb(18,20,18));
        for(int i=0;i<rows.Length;i++)
        {
            int y=15+i*34;e.Graphics.DrawString(rows[i].Item1,label,text,12,y);e.Graphics.FillRectangle(track,112,y+3,288,15);
            using var fill=new SolidBrush(rows[i].Item3);e.Graphics.FillRectangle(fill,112,y+3,(int)(288*Math.Clamp(rows[i].Item2/100,0,1)),15);
        }
    }
}

public sealed class SettingsWindow : Form
{
    readonly PetApplication app;
    readonly Dictionary<string,NumericUpDown> numbers=[];
    readonly CheckBox meters,startup,launch,pauseHidden,invincible,edge,neuralSteering,rest;
    readonly ComboBox monitor;
    public SettingsWindow(PetApplication app)
    {
        this.app=app;Theme.Form(this);Text="FlyPet · 设置";ClientSize=new(650,660);MinimumSize=new(650,660);StartPosition=FormStartPosition.CenterScreen;
        Controls.Add(Theme.Label("按你的节奏生活。",24,22,560,38,19,Theme.Accent));
        Controls.Add(Theme.Label("应用后立即生效。高级参数保存在本地 JSON 中。",26,67,580,25,10,Theme.Muted));
        var scroll=new Panel{Location=new(20,105),Size=new(610,430),AutoScroll=true,Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Bottom};Controls.Add(scroll);
        int y=8;
        void Num(string name,string label,decimal min,decimal max,decimal value,int decimals=0)
        {
            scroll.Controls.Add(Theme.Label(label,8,y+5,355,30));
            var n=new NumericUpDown{Location=new(390,y),Size=new(174,30),Minimum=min,Maximum=max,DecimalPlaces=decimals,Increment=decimals==0?1:.1m,Value=value,BackColor=Theme.Panel,ForeColor=Theme.Text,BorderStyle=BorderStyle.FixedSingle};
            numbers[name]=n;scroll.Controls.Add(n);y+=43;
        }
        var s=app.Settings;
        Num(nameof(s.FramesPerSecond),"显示帧率（FPS）",20,120,s.FramesPerSecond);
        Num(nameof(s.PetSize),"苍蝇显示大小（像素）",60,320,s.PetSize);
        Num(nameof(s.FlightSpeed),"基础飞行速度（像素 / 秒）",30,900,(decimal)s.FlightSpeed);
        Num(nameof(s.FearRadius),"鼠标威胁感知范围（像素）",80,800,(decimal)s.FearRadius);
        Num(nameof(s.SwatDamage),"每次拍打伤害",1,100,(decimal)s.SwatDamage);
        Num(nameof(s.HungerPerMinute),"每分钟饱腹下降",0,60,(decimal)s.HungerPerMinute,1);
        Num(nameof(s.FlightFullnessCostPerSecond),"飞行每秒额外饱腹消耗",0,10,(decimal)s.FlightFullnessCostPerSecond,1);
        Num(nameof(s.AlarmSeconds),"受击后快速逃离持续秒数",.1m,10,(decimal)s.AlarmSeconds,1);
        Num(nameof(s.FeedPerSecond),"进食速率（每秒）",1,100,(decimal)s.FeedPerSecond);
        Num(nameof(s.StarvationDamagePerSecond),"饥饿时每秒失血",0,20,(decimal)s.StarvationDamagePerSecond,1);
        Num(nameof(s.RespawnMinSeconds),"随机复活最短等待（秒）",1,3600,(decimal)s.RespawnMinSeconds);
        Num(nameof(s.RespawnMaxSeconds),"随机复活最长等待（秒）",1,7200,(decimal)s.RespawnMaxSeconds);
        Num(nameof(s.NeuralGain),"连接增益（0 可消融突触传播）",0,4,(decimal)s.NeuralGain,1);
        Num(nameof(s.SensoryGain),"感觉输入增益（0 可关闭输入）",0,4,(decimal)s.SensoryGain,1);
        Num(nameof(s.SugarAttractionRadius),"糖的吸引范围（像素）",100,10000,(decimal)s.SugarAttractionRadius);
        Num(nameof(s.MaxSugar),"最多保留糖粒数",1,30,s.MaxSugar);
        scroll.Controls.Add(Theme.Label("桌宠所在显示器",8,y+4,300,32));
        monitor=new(){Location=new(335,y),Size=new(228,30),DropDownStyle=ComboBoxStyle.DropDownList,BackColor=Theme.Panel,ForeColor=Theme.Text};
        for(int i=0;i<Screen.AllScreens.Length;i++){var screen=Screen.AllScreens[i];monitor.Items.Add($"{i+1} · {screen.Bounds.Width}×{screen.Bounds.Height}");}
        monitor.SelectedIndex=Math.Min(s.MonitorIndex,monitor.Items.Count-1);scroll.Controls.Add(monitor);y+=45;
        CheckBox Check(string label,bool value){var c=new CheckBox{Text=label,Checked=value,Location=new(8,y),Size=new(540,34),ForeColor=Theme.Text};scroll.Controls.Add(c);y+=39;return c;}
        meters=Check("显示生命与饱腹条",s.ShowMeters);startup=Check("登录 Windows 时自动启动桌宠",s.StartWithWindows);
        launch=Check("手动启动时显示启动菜单",s.ShowLaunchMenu);pauseHidden=Check("隐藏桌宠时暂停模拟，降低功耗",s.PauseWhenHidden);
        invincible=Check("无敌模式（受击仍会逃离）",s.Invincible);
        edge=Check("屏幕边缘视觉输入",s.EdgeSensing);neuralSteering=Check("神经转向读出（关闭为直接规则对照）",s.NeuralSteering);
        rest=Check("允许停歇",s.RestEnabled);
        scroll.Controls.Add(Theme.Label("饱腹 100 = 吃饱，0 = 饥饿；飞行会额外消耗。\n感觉编码与身体解码是工程模型，不是活体参数。",8,y+6,550,84,9,Theme.Muted));
        Controls.Add(Theme.Button("应用设置",22,555,180,Apply,true));Controls.Add(Theme.Button("打开配置目录",218,555,185,()=>Process.Start(new ProcessStartInfo(Settings.Folder){UseShellExecute=true})));
        Controls.Add(Theme.Button("关闭",419,555,205,Close));
        Controls.Add(Theme.Label("修改 JSON 后从托盘选择「重新加载配置」。",24,615,590,26,9,Theme.Muted));ResumeLayout(false);
    }
    void Apply()
    {
        try
        {
            var s=app.Settings;
            if(numbers[nameof(s.RespawnMaxSeconds)].Value<numbers[nameof(s.RespawnMinSeconds)].Value){MessageBox.Show(this,"最长等待不能小于最短等待。","设置");return;}
            foreach(var (name,n) in numbers){var p=typeof(Settings).GetProperty(name)!;p.SetValue(s,Convert.ChangeType(n.Value,p.PropertyType));}
            s.MonitorIndex=monitor.SelectedIndex;s.ShowMeters=meters.Checked;s.ShowLaunchMenu=launch.Checked;s.PauseWhenHidden=pauseHidden.Checked;
            s.Invincible=invincible.Checked;s.EdgeSensing=edge.Checked;s.NeuralSteering=neuralSteering.Checked;s.RestEnabled=rest.Checked;
            using var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if(startup.Checked)key.SetValue("FlyPet",$"\"{Environment.ProcessPath}\" --quiet");else key.DeleteValue("FlyPet",false);
            s.StartWithWindows=startup.Checked;app.ApplySettings();Close();
        }
        catch(Exception e){MessageBox.Show(this,"无法保存设置："+e.Message,"FlyPet");}
    }
}
