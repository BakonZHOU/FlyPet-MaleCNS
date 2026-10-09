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
        var b=new Button{Text=text,Location=new(x,y),Size=new(w,38),FlatStyle=FlatStyle.Flat,Font=Font(10,primary?FontStyle.Bold:FontStyle.Regular),BackColor=primary?Accent:Panel,ForeColor=primary?Bg:Text,Cursor=Cursors.Hand,UseVisualStyleBackColor=false};
        b.FlatAppearance.BorderColor=Line;b.FlatAppearance.MouseOverBackColor=primary?Color.FromArgb(218,229,169):Color.FromArgb(48,55,46);b.FlatAppearance.MouseDownBackColor=Color.FromArgb(111,129,87);b.Click+=(_,_)=>action();return b;
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
        float orbit=Math.Min(Width,Height)*.38f;
        using var line=new Pen(Theme.Line);g.DrawEllipse(line,Width/2-orbit,Height/2-orbit,orbit*2,orbit*2);
        var sim=app.Sim;int sprite=(int)(Math.Min(Width,Height)*.94f);renderer.Draw(g,new(Width/2-sprite/2,Height/2-sprite/2,sprite,sprite),sim,app.Area,false);
        using var mono=new Font("Consolas",9);using var brush=new SolidBrush(Theme.Muted);
        g.DrawString(app.Settings.Skin==PetSkin.Cockroach?"02 / PERIPLANETA":"01 / DROSOPHILA",mono,brush,12*scale,10*scale);
    }
    protected override void Dispose(bool disposing){if(disposing)renderer.Dispose();base.Dispose(disposing);}
}

public sealed class Dashboard : Form
{
    readonly PetApplication app;
    readonly Label status,metrics;
    readonly PreviewPanel preview;
    readonly Label heading,subtitle;
    readonly Button flyButton,roachButton;
    bool exiting;
    public Dashboard(PetApplication app)
    {
        this.app=app;Text="FlyPet · 小窝";Theme.Form(this);ClientSize=new(590,310);FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;
        Controls.Add(Theme.Label("FLYPET  /  小窝",22,14,530,22,10,Theme.Accent));
        heading=Theme.Label("",22,38,535,38,20);Controls.Add(heading);
        subtitle=Theme.Label("",23,76,535,23,10,Theme.Muted);Controls.Add(subtitle);
        preview=new(app){Location=new(22,108),Size=new(220,174)};Controls.Add(preview);
        flyButton=Theme.Button("果蝇",262,112,128,()=>app.SelectSkin(PetSkin.Fly));Controls.Add(flyButton);
        roachButton=Theme.Button("广东双马尾",399,112,169,()=>app.SelectSkin(PetSkin.Cockroach));Controls.Add(roachButton);
        status=Theme.Label("",264,165,300,34,11,Theme.Accent);Controls.Add(status);
        metrics=Theme.Label("",264,199,300,40,9,Theme.Muted);Controls.Add(metrics);
        Controls.Add(Theme.Button("投糖",262,244,91,()=>{app.StartPet();app.SetMode(ToolMode.Sugar);Hide();}));
        Controls.Add(Theme.Button("设置",364,244,91,()=>app.ShowSettings()));
        Controls.Add(Theme.Button("大脑图",466,244,102,()=>app.ShowBrainMap()));
        RefreshSkin();
        FormClosing+=(_,e)=>{if(!exiting){e.Cancel=true;Hide();}};ResumeLayout(false);
    }
    public void RefreshSkin()
    {
        bool roach=app.Settings.Skin==PetSkin.Cockroach;
        Text=roach?"FlyPet · 广东双马尾的小窝":"FlyPet · 果蝇的小窝";
        heading.Text=roach?"广东双马尾":"果蝇";
        subtitle.Text=roach?"桌面散步，偶尔展翅":"在桌面自由飞行";
        flyButton.BackColor=roach?Theme.Panel:Theme.Accent;flyButton.ForeColor=roach?Theme.Text:Theme.Bg;
        roachButton.BackColor=roach?Theme.Accent:Theme.Panel;roachButton.ForeColor=roach?Theme.Bg:Theme.Text;
        preview.Invalidate();RefreshStatus();
    }
    public void RefreshStatus()
    {
        var s=app.Sim;status.Text=s.Dead?$"等待复活 · {s.DeathRemaining:0} 秒":app.BehaviorLabel;
        metrics.Text=$"生命 {s.Health/s.MaxHealth*100:0}%   饱腹 {s.Fullness:0}%   {s.DisplaySize} px\n边缘经验 {s.Brain.AvoidanceSkill*100:0}%   成功避开 {s.SuccessfulEdgeAvoidances}   撞击 {s.EdgeCollisions}";preview.Invalidate();
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
        Controls.Add(Theme.Label($"内置回路含 {app.Circuit.Nodes.Length:N0} 个 MaleCNS 神经元和 {app.Circuit.Edges.Length:N0} 条连接。外界刺激写入视觉、嗅觉、味觉与位置稀疏编码；飞行、导航、逃逸和运动从下游群读取。吃糖奖励与边缘撞击负反馈会修改蘑菇体位置记忆。血量与复活仍是工程规则。",28,70,700,72,10,Theme.Muted));
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
        values.Text=$"视觉回路            {b.VisualRate,7:0.0} Hz\n嗅觉回路            {b.OlfactoryRate,7:0.0} Hz\n蘑菇体记忆          {b.MemoryRate,7:0.0} Hz\n奖励 DAN            {b.RewardRate,7:0.0} Hz\n中央复合体导航      {b.NavigationRate,7:0.0} Hz\n下行 / 运动         {b.DescendingRate,7:0.0} / {b.MotorRate:0.0} Hz\n边缘经验            {b.AvoidanceSkill,7:0.00}\n累计放电            {b.TotalSpikes,10:N0}";
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
        var rows=new[]{("视觉输入",b.VisualRate,Color.FromArgb(184,177,102)),("嗅觉输入",b.OlfactoryRate,Color.FromArgb(112,205,126)),("记忆",b.MemoryRate,Color.FromArgb(218,170,72)),("奖励",b.RewardRate,Color.FromArgb(234,104,146)),("导航",b.NavigationRate,Color.FromArgb(91,197,205)),("下行运动",b.DescendingRate,Color.FromArgb(190,82,59))};
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
    readonly CheckBox meters,startup,launch,pauseHidden,invincible,edge,neuralSteering,rest,voice;
    readonly ComboBox monitor,skin;

    public void SyncSkin(PetSkin value){if(!IsDisposed)skin.SelectedIndex=(int)value;}

    public SettingsWindow(PetApplication app)
    {
        this.app=app;Theme.Form(this);Text="FlyPet · 外观与设置";
        ClientSize=new(620,455);MinimumSize=new(520,390);FormBorderStyle=FormBorderStyle.Sizable;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;TopMost=false;

        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(14,12,14,10),BackColor=Theme.Bg};
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,54));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,43));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,51));
        Controls.Add(layout);
        var header=new Label{Text="外观与设置",Dock=DockStyle.Fill,Font=Theme.Font(18,FontStyle.Bold),ForeColor=Theme.Accent,TextAlign=ContentAlignment.MiddleLeft};
        layout.Controls.Add(header,0,0);

        var nav=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.LeftToRight,WrapContents=false,AutoScroll=true,Margin=Padding.Empty};
        layout.Controls.Add(nav,0,1);
        var content=new Panel{Dock=DockStyle.Fill,Margin=new Padding(0,5,0,5),BackColor=Theme.Panel};
        layout.Controls.Add(content,0,2);
        var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false,Margin=Padding.Empty};
        layout.Controls.Add(footer,0,3);

        string[] categories=["外观","生存","行为","神经","系统"];
        var panels=new Dictionary<string,Panel>();
        var tables=new Dictionary<string,TableLayoutPanel>();
        var tabs=new Dictionary<string,Button>();
        foreach(string category in categories)
        {
            var panel=new Panel{Dock=DockStyle.Fill,AutoScroll=true,Visible=false,BackColor=Theme.Panel};
            var table=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,RowCount=0,Padding=new Padding(12,8,12,8),BackColor=Theme.Panel};
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,64));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,36));
            panel.Controls.Add(table);content.Controls.Add(panel);panels[category]=panel;tables[category]=table;
            var tab=Theme.Button(category,0,0,92,()=>SelectCategory(category));
            tab.Height=34;tab.Margin=new Padding(0,0,4,0);tabs[category]=tab;nav.Controls.Add(tab);
        }
        void SelectCategory(string category)
        {
            foreach(var entry in panels)entry.Value.Visible=entry.Key==category;
            panels[category].BringToFront();
            foreach(var entry in tabs)
            {
                bool active=entry.Key==category;
                entry.Value.BackColor=active?Theme.Accent:Theme.Panel;
                entry.Value.ForeColor=active?Theme.Bg:Theme.Text;
                entry.Value.FlatAppearance.BorderColor=active?Theme.Accent:Theme.Line;
            }
        }
        void Row(string category,Control label,Control editor,int height=43)
        {
            var table=tables[category];int index=table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
            label.Dock=DockStyle.Fill;if(label is Label caption)caption.TextAlign=ContentAlignment.MiddleLeft;label.Margin=new Padding(2,0,8,0);
            editor.Dock=DockStyle.Fill;editor.Margin=new Padding(2,6,2,6);
            table.Controls.Add(label,0,index);table.Controls.Add(editor,1,index);
        }
        Label Caption(string value)=>new(){Text=value,ForeColor=Theme.Text,Font=Theme.Font(9.5f),AutoEllipsis=true};
        void Num(string category,string name,string label,decimal min,decimal max,decimal value,int decimals=0)
        {
            decimal increment=decimals switch{0=>1m,1=>.1m,2=>.01m,3=>.001m,_=>.0001m};
            var n=new NumericUpDown{Minimum=min,Maximum=max,DecimalPlaces=decimals,Increment=increment,Value=Math.Clamp(value,min,max),
                BackColor=Theme.Bg,ForeColor=Theme.Text,BorderStyle=BorderStyle.FixedSingle,Font=Theme.Font(10)};
            numbers[name]=n;Row(category,Caption(label),n);
        }
        CheckBox Check(string category,string label,bool value)
        {
            var c=new CheckBox{Text=label,Checked=value,ForeColor=Theme.Text,Font=Theme.Font(9.5f),AutoSize=false,Dock=DockStyle.Fill,Margin=new Padding(4,3,0,3)};
            var table=tables[category];int index=table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.Absolute,38));table.Controls.Add(c,0,index);table.SetColumnSpan(c,2);return c;
        }
        var s=app.Settings;
        skin=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,BackColor=Theme.Bg,ForeColor=Theme.Text};
        skin.Items.AddRange(["果蝇","广东双马尾"]);skin.SelectedIndex=(int)s.Skin;Row("外观",Caption("桌宠外观"),skin);
        Num("外观",nameof(s.PetSize),"果蝇大小 · px",Settings.MinPetSize,Settings.MaxPetSize,s.PetSize);
        Num("外观",nameof(s.CockroachSize),"普通双马尾大小 · px",Settings.MinPetSize,Settings.MaxPetSize,s.CockroachSize);
        Num("外观",nameof(s.GiantCockroachSize),"巨型双马尾大小 · px",Settings.MinPetSize,Settings.MaxPetSize,s.GiantCockroachSize);
        Num("外观",nameof(s.GiantCockroachChance),"巨型双马尾出现概率",0,1,(decimal)s.GiantCockroachChance,2);
        Num("外观",nameof(s.AlbinoChance),"白眼果蝇出现概率",0,1,(decimal)s.AlbinoChance,2);
        Num("外观",nameof(s.AlbinoSpeedMultiplier),"稀有个体速度倍率",1,3,(decimal)s.AlbinoSpeedMultiplier,2);
        meters=Check("外观","显示生命与饱腹条",s.ShowMeters);

        Num("生存",nameof(s.HungerPerMinute),"每分钟饱腹下降",0,60,(decimal)s.HungerPerMinute,2);
        Num("生存",nameof(s.FlightFullnessCostPerSecond),"活动每秒额外消耗",0,10,(decimal)s.FlightFullnessCostPerSecond,3);
        Num("生存",nameof(s.StarvationDamagePerSecond),"饥饿时每秒失血",0,20,(decimal)s.StarvationDamagePerSecond,2);
        Num("生存",nameof(s.SatiatedThreshold),"开始回血的饱腹值",50,100,(decimal)s.SatiatedThreshold);
        Num("生存",nameof(s.SatiatedRegenPerSecond),"饱腹时每秒回血",0,20,(decimal)s.SatiatedRegenPerSecond,1);
        Num("生存",nameof(s.RespawnMinSeconds),"复活最短等待 · 秒",1,3600,(decimal)s.RespawnMinSeconds);
        Num("生存",nameof(s.RespawnMaxSeconds),"复活最长等待 · 秒",1,7200,(decimal)s.RespawnMaxSeconds);
        Num("生存",nameof(s.SwatDamage),"每次拍打伤害",1,100,(decimal)s.SwatDamage);
        Num("生存",nameof(s.EvanescenceChance),"瞬机触发概率",0,1,(decimal)s.EvanescenceChance,2);
        Num("生存",nameof(s.RockSolidChance),"铜头铁臂触发概率",0,1,(decimal)s.RockSolidChance,2);
        invincible=Check("生存","无敌模式",s.Invincible);

        Num("行为",nameof(s.FlightSpeed),"基础移动速度 · px/s",30,900,(decimal)s.FlightSpeed);
        Num("行为",nameof(s.FearRadius),"鼠标威胁范围 · px",80,800,(decimal)s.FearRadius);
        Num("行为",nameof(s.EscapeSafeRadiusMultiplier),"解除逃逸的距离倍率",1,2.5m,(decimal)s.EscapeSafeRadiusMultiplier,2);
        Num("行为",nameof(s.EscapeAccelerationGain),"持续逃逸加速倍率",0,4,(decimal)s.EscapeAccelerationGain,2);
        Num("行为",nameof(s.AlarmSeconds),"受击警觉持续 · 秒",.1m,10,(decimal)s.AlarmSeconds,1);
        Num("行为",nameof(s.SugarAttractionRadius),"糖的感知距离 · px",100,10000,(decimal)s.SugarAttractionRadius);
        Num("行为",nameof(s.SugarNutrition),"每块糖恢复饱腹",1,100,(decimal)s.SugarNutrition);
        Num("行为",nameof(s.SugarEatingSeconds),"吃糖所需 · 秒",.2m,10,(decimal)s.SugarEatingSeconds,1);
        Num("行为",nameof(s.MaxSugar),"最多保留糖粒数",1,30,s.MaxSugar);
        rest=Check("行为","允许停歇",s.RestEnabled);

        Num("神经",nameof(s.NeuralGain),"神经连接增益",0,4,(decimal)s.NeuralGain,1);
        Num("神经",nameof(s.SensoryGain),"感觉输入增益",0,4,(decimal)s.SensoryGain,1);
        Num("神经",nameof(s.LearningRate),"位置学习率",0,2,(decimal)s.LearningRate,2);
        Num("神经",nameof(s.MemoryDecayPerMinute),"每分钟记忆衰减",0,1,(decimal)s.MemoryDecayPerMinute,3);
        Num("神经",nameof(s.SugarReward),"吃糖奖励信号",0,2,(decimal)s.SugarReward,2);
        Num("神经",nameof(s.EdgePunishment),"碰边负面信号",0,2,(decimal)s.EdgePunishment,2);
        Num("神经",nameof(s.CollisionMemoryDiameter),"碰边记忆直径 · px",80,1200,(decimal)s.CollisionMemoryDiameter);
        edge=Check("神经","屏幕边缘视觉输入",s.EdgeSensing);
        neuralSteering=Check("神经","使用神经转向读出",s.NeuralSteering);

        Num("系统",nameof(s.FramesPerSecond),"显示帧率 · FPS",20,120,s.FramesPerSecond);
        monitor=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,BackColor=Theme.Bg,ForeColor=Theme.Text};
        for(int i=0;i<Screen.AllScreens.Length;i++)
        {
            var screen=Screen.AllScreens[i];
            monitor.Items.Add($"{i+1} · {screen.Bounds.Width}×{screen.Bounds.Height}");
        }
        monitor.SelectedIndex=Math.Min(s.MonitorIndex,monitor.Items.Count-1);Row("系统",Caption("所在显示器"),monitor);
        startup=Check("系统","登录 Windows 时自动启动",s.StartWithWindows);
        launch=Check("系统","手动启动时显示小窝",s.ShowLaunchMenu);
        pauseHidden=Check("系统","隐藏时暂停模拟",s.PauseWhenHidden);
        voice=Check("系统","启用本地离线语音控制（需安装中文语音包）",s.VoiceEnabled);

        Button Footer(string text,int width,Action action,bool primary=false)
        {
            var button=Theme.Button(text,0,0,width,action,primary);button.Height=36;button.Margin=new Padding(6,5,0,0);footer.Controls.Add(button);return button;
        }
        Footer("取消",86,Close);
        Footer("配置目录",105,()=>Process.Start(new ProcessStartInfo(Settings.Folder){UseShellExecute=true}));
        Footer("恢复默认",105,ResetDefaults);
        Footer("保存并应用",142,Apply,true);
        SelectCategory("外观");ResumeLayout(false);
    }

    void ResetDefaults()
    {
        var defaults=new Settings();
        foreach(var (name,n) in numbers)
        {
            var value=typeof(Settings).GetProperty(name)!.GetValue(defaults)!;
            n.Value=Math.Clamp(Convert.ToDecimal(value),n.Minimum,n.Maximum);
        }
        monitor.SelectedIndex=Math.Min(defaults.MonitorIndex,monitor.Items.Count-1);
        skin.SelectedIndex=(int)defaults.Skin;
        meters.Checked=defaults.ShowMeters;startup.Checked=defaults.StartWithWindows;launch.Checked=defaults.ShowLaunchMenu;pauseHidden.Checked=defaults.PauseWhenHidden;
        invincible.Checked=defaults.Invincible;edge.Checked=defaults.EdgeSensing;neuralSteering.Checked=defaults.NeuralSteering;rest.Checked=defaults.RestEnabled;voice.Checked=defaults.VoiceEnabled;
        Apply();
    }

    void Apply()
    {
        try
        {
            var s=app.Settings;
            if(numbers[nameof(s.RespawnMaxSeconds)].Value<numbers[nameof(s.RespawnMinSeconds)].Value)
            {MessageBox.Show(this,"最长复活等待不能小于最短等待。","设置");return;}
            if(numbers[nameof(s.EvanescenceChance)].Value+numbers[nameof(s.RockSolidChance)].Value>1)
            {MessageBox.Show(this,"瞬机和铜头铁臂的触发概率之和不能超过 1。","设置");return;}
            foreach(var (name,n) in numbers)
            {
                var p=typeof(Settings).GetProperty(name)!;
                p.SetValue(s,Convert.ChangeType(n.Value,p.PropertyType));
            }
            if(s.Skin!=(PetSkin)skin.SelectedIndex)app.SelectSkin((PetSkin)skin.SelectedIndex);
            s.MonitorIndex=monitor.SelectedIndex;s.ShowMeters=meters.Checked;s.ShowLaunchMenu=launch.Checked;s.PauseWhenHidden=pauseHidden.Checked;
            s.Invincible=invincible.Checked;s.EdgeSensing=edge.Checked;s.NeuralSteering=neuralSteering.Checked;s.RestEnabled=rest.Checked;s.VoiceEnabled=voice.Checked;
            using var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if(startup.Checked)key.SetValue("FlyPet",$"\"{Environment.ProcessPath}\" --quiet");else key.DeleteValue("FlyPet",false);
            s.StartWithWindows=startup.Checked;app.ApplySettings();Close();
        }
        catch(Exception e){MessageBox.Show(this,"无法保存设置："+e.Message,"FlyPet");}
    }
}
