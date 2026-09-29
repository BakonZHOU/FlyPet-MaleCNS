using System.Diagnostics;
using System.Drawing.Imaging;
using System.Numerics;
using System.Text.Json;

namespace FlyPet;

public enum ToolMode { Normal, Sugar, Swatter }

public sealed class PetApplication : ApplicationContext
{
    public Settings Settings {get;private set;}
    public CircuitData Circuit {get;}
    public Simulation Sim {get;}
    public Rectangle Area => Screen.AllScreens[Math.Min(Settings.MonitorIndex,Screen.AllScreens.Length-1)].WorkingArea;
    public ToolMode Mode {get;private set;}
    public string ModeLabel=>Mode==ToolMode.Sugar?"投糖模式 · 点击桌面放置一粒糖":"普通模式 · 始终躲避鼠标";
    public string BehaviorLabel=>Settings.Skin==PetSkin.Cockroach&&Sim.Behavior=="自由飞行"&&!Sim.CockroachFlying?"桌面奔跑":Sim.Behavior;
    public bool Paused {get;private set;}
    bool visible,disposing;
    public double ComputeMs {get;private set;}
    public double MeasuredFps {get;private set;}
    public double RealTimeRatio {get;private set;}=1;
    readonly Dashboard dashboard;
    SettingsWindow? settingsWindow;
    NeuralEvidenceWindow? evidenceWindow;
    BrainMapWindow? brainMap;
    DeathMenuWindow? deathMenu;
    readonly LayerWindow pet,cursor,speech;
    readonly List<LayerWindow> sugarWindows=[];
    readonly FlyRenderer renderer=new();
    readonly NotifyIcon tray;
    readonly ToolStripMenuItem flySkinItem,roachSkinItem;
    readonly Icon icon;
    readonly MouseHook mouseHook;
    readonly LocalControl localControl;
    readonly System.Windows.Forms.Timer timer=new();
    readonly Stopwatch clock=Stopwatch.StartNew();
    double previous,accumulator,statsPrevious,simulatedWindow,drawElapsed,nextStatusSave;
    int frameCounter;
    Point? pendingClick;
    ToolMode pendingMode;
    bool escaped;
    public PetApplication(bool quiet)
    {
        Settings=Settings.Load();Directory.CreateDirectory(Settings.Folder);Circuit=CircuitData.Load();
        Sim=new(Settings,Circuit);Sim.Recenter(Area);
        pet=new(Sim.DisplaySize,"FlyPet · 桌宠");cursor=new(84,"FlyPet · 工具指针");speech=new(340,58,"FlyPet · 气泡");
        dashboard=new(this);
        _=dashboard.Handle;
        localControl=new(dashboard,Command);
        using(var b=new Bitmap(32,32,PixelFormat.Format32bppPArgb))
        {
            using(var g=Graphics.FromImage(b)){g.Clear(Color.Transparent);g.FillEllipse(Brushes.DarkOliveGreen,9,7,14,20);g.FillEllipse(Brushes.Silver,1,8,13,8);g.FillEllipse(Brushes.Silver,18,8,13,8);g.FillEllipse(Brushes.IndianRed,11,5,10,8);}
            nint h=b.GetHicon();icon=(Icon)Icon.FromHandle(h).Clone();Native.DestroyIcon(h);
        }
        dashboard.Icon=icon;
        var menu=new ContextMenuStrip();menu.Items.Add("显示 / 隐藏桌宠",null,(_,_)=>ToggleVisible());
        menu.Items.Add("投放糖粒",null,(_,_)=>{StartPet();SetMode(ToolMode.Sugar);});
        menu.Items.Add("打开小窝",null,(_,_)=>Defer(ShowDashboard));
        menu.Items.Add("外观与设置…",null,(_,_)=>Defer(ShowSettings));
        var skins=new ToolStripMenuItem("切换外观");menu.Items.Add(skins);
        flySkinItem=new ToolStripMenuItem("果蝇"){Checked=Settings.Skin==PetSkin.Fly};
        roachSkinItem=new ToolStripMenuItem("广东双马尾"){Checked=Settings.Skin==PetSkin.Cockroach};
        flySkinItem.Click+=(_,_)=>SelectSkin(PetSkin.Fly);roachSkinItem.Click+=(_,_)=>SelectSkin(PetSkin.Cockroach);
        skins.DropDownItems.Add(flySkinItem);skins.DropDownItems.Add(roachSkinItem);
        menu.Items.Add("暂停 / 继续",null,(_,_)=>TogglePause());
        menu.Items.Add(new ToolStripSeparator());
        var more=new ToolStripMenuItem("更多操作");menu.Items.Add(more);
        var meterItem=new ToolStripMenuItem("显示状态条"){Checked=Settings.ShowMeters,CheckOnClick=true};meterItem.CheckedChanged+=(_,_)=>{Settings.ShowMeters=meterItem.Checked;Settings.Save();};menu.Items.Add(meterItem);
        var invincibleItem=new ToolStripMenuItem("无敌模式"){Checked=Settings.Invincible,CheckOnClick=true};invincibleItem.CheckedChanged+=(_,_)=>{Settings.Invincible=invincibleItem.Checked;Settings.Save();};menu.Items.Add(invincibleItem);
        menu.Items.Remove(meterItem);menu.Items.Remove(invincibleItem);more.DropDownItems.Add(meterItem);more.DropDownItems.Add(invincibleItem);
        more.DropDownItems.Add("立即复活",null,(_,_)=>Sim.Revive(Area));
        more.DropDownItems.Add("清除糖粒",null,(_,_)=>Sim.Sugars.Clear());more.DropDownItems.Add("召回桌宠",null,(_,_)=>{Sim.Recenter(Area);StartPet();});
        more.DropDownItems.Add("大脑活动图…",null,(_,_)=>Defer(ShowBrainMap));
        more.DropDownItems.Add("重新加载配置",null,(_,_)=>ReloadSettings());menu.Items.Add("退出 FlyPet",null,(_,_)=>ExitThread());
        tray=new NotifyIcon{Icon=icon,Text="FlyPet · 桌宠",ContextMenuStrip=menu,Visible=true};tray.DoubleClick+=(_,_)=>ShowDashboard();
        mouseHook=new(OnMouseDown);
        Native.timeBeginPeriod(1);timer.Interval=TimerInterval();timer.Tick+=Tick;timer.Start();
        if(quiet||!Settings.ShowLaunchMenu)StartPet();else ShowDashboard();
        if(Settings.LoadWarning!=null)tray.ShowBalloonTip(5000,"FlyPet",Settings.LoadWarning,ToolTipIcon.Warning);
    }
    void Defer(Action action){if(dashboard.IsHandleCreated)dashboard.BeginInvoke(action);else action();}
    public void ShowDashboard(){dashboard.Show();dashboard.WindowState=FormWindowState.Normal;dashboard.BringToFront();dashboard.Activate();}
    public void ShowEvidence(){SetMode(ToolMode.Normal);if(evidenceWindow==null||evidenceWindow.IsDisposed)evidenceWindow=new(this);evidenceWindow.Show();evidenceWindow.Activate();}
    public void ShowBrainMap(){SetMode(ToolMode.Normal);if(brainMap==null||brainMap.IsDisposed)brainMap=new(this);brainMap.Show();brainMap.Activate();}
    public CausalAudit RunCausalAudit()
    {
        Settings Copy(float gain){var s=JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(Settings,Settings.JsonOptions),Settings.JsonOptions)!;s.NeuralGain=gain;s.SensoryGain=1;s.HungerPerMinute=0;return s;}
        (float distance,long spikes,float flight,float fear,float feed) Run(float gain)
        {
            var sim=new Simulation(Copy(gain),Circuit,731);sim.Recenter(Area);sim.Fullness=20;var origin=sim.Position;sim.AddSugar(origin+new Vector2(360,0));
            float flight=0,fear=0,feed=0;
            for(int i=0;i<2400;i++){var mouse=i is >900 and <1100?sim.Position+new Vector2(55,0):new Vector2(-5000,-5000);sim.Update(1f/120,Area,mouse,i is >900 and <1100);flight+=sim.Brain.Flight;fear+=sim.Brain.Fear;feed+=sim.Brain.Feeding;}
            return (Vector2.Distance(origin,sim.Position),sim.Brain.TotalSpikes,flight/2400,fear/2400,feed/2400);
        }
        var connected=Run(1);var severed=Run(0);
        return new(connected.distance,severed.distance,connected.spikes,severed.spikes,connected.flight,severed.flight,connected.fear,severed.fear,connected.feed,severed.feed);
    }
    object Command(string[] args)
    {
        if(args.Length==0)throw new ArgumentException("Missing command");
        Vector2 PointArg()
        {
            if(args.Length!=3||!int.TryParse(args[1],out int x)||!int.TryParse(args[2],out int y)||!Area.Contains(x,y))throw new ArgumentException("Expected x y inside selected monitor working area");
            return new(x,y);
        }
        switch(args[0])
        {
            case "menu":ShowDashboard();break;
            case "settings":ShowSettings();break;
            case "skin":
                if(args.Length!=2||args[1] is not ("fly" or "cockroach"))throw new ArgumentException("Expected skin fly|cockroach");
                SelectSkin(args[1]=="fly"?PetSkin.Fly:PetSkin.Cockroach);break;
            case "evidence":ShowEvidence();break;
            case "brain-map":ShowBrainMap();break;
            case "show":StartPet();break;
            case "hide":if(visible)ToggleVisible();break;
            case "pause":Paused=true;SetMode(ToolMode.Normal);break;
            case "resume":Paused=false;break;
            case "swatter":StartPet();SetMode(ToolMode.Normal);break; // compatibility alias; there is no swatter mode now
            case "sugar-mode":StartPet();SetMode(ToolMode.Sugar);break;
            case "normal":SetMode(ToolMode.Normal);break;
            case "drop":Sim.AddSugar(PointArg());break;
            case "swat":Sim.Hit(PointArg());break;
            case "recall":Sim.Recenter(Area);break;
            case "revive":Sim.Revive(Area);break;
            case "invincible":Settings.Invincible=args.Length<2||args[1]=="on";Settings.Save();break;
            case "clear":Sim.Sugars.Clear();break;
            case "reload":ReloadSettings();break;
            case "audit":return RunCausalAudit();
            case "status":break;
            case "exit":
                var shutdown=new System.Windows.Forms.Timer{Interval=200};
                shutdown.Tick+=(_,_)=>{shutdown.Stop();shutdown.Dispose();ExitThread();};shutdown.Start();return new{ok=true};
            default:throw new ArgumentException("Unknown command");
        }
        return new{ok=true,visible,paused=Paused,mode=Mode.ToString(),skin=Settings.Skin.ToString(),size=Sim.DisplaySize,rare=Sim.Albino,health=Sim.Health,fullness=Sim.Fullness,invincible=Settings.Invincible,grounded=Sim.Grounded,cockroachFlying=Sim.CockroachFlying,dead=Sim.Dead,deathCause=Sim.CauseOfDeath.ToString(),respawn=Sim.DeathRemaining,sugar=Sim.Sugars.Count,x=Sim.Position.X,y=Sim.Position.Y,fps=MeasuredFps,realtime=RealTimeRatio,computeMs=ComputeMs,area=new{Area.X,Area.Y,Area.Width,Area.Height}};
    }
    public void ShowSettings(){SetMode(ToolMode.Normal);if(settingsWindow==null||settingsWindow.IsDisposed)settingsWindow=new(this);settingsWindow.Show();settingsWindow.WindowState=FormWindowState.Normal;settingsWindow.BringToFront();settingsWindow.Activate();}
    public void SelectSkin(PetSkin skin)
    {
        Sim.ChangeSkin(skin);Settings.Save();dashboard.RefreshSkin();
        settingsWindow?.SyncSkin(skin);
        deathMenu?.SetSkin(skin);
        flySkinItem.Checked=skin==PetSkin.Fly;roachSkinItem.Checked=skin==PetSkin.Cockroach;
        tray.Text=skin==PetSkin.Fly?"FlyPet · 果蝇桌宠":"FlyPet · 广东双马尾";
    }
    public void StartPet(){visible=true;Paused=false;pet.Show();}
    public void ToggleVisible()
    {
        visible=!visible;
        if(visible){pet.Show();}else{SetMode(ToolMode.Normal);pet.Hide();speech.Hide();foreach(var w in sugarWindows)w.Hide();}
    }
    public void TogglePause(){Paused=!Paused;if(Paused)SetMode(ToolMode.Normal);}
    public void SetMode(ToolMode mode){Mode=mode;if(mode==ToolMode.Normal)cursor.Hide();}
    int TimerInterval()=>Math.Clamp((int)Math.Round(1000d/Math.Max(1,Settings.FramesPerSecond)),1,50);
    public void ApplySettings(){Settings.Validate();Settings.Save();Sim.Settings=Settings;timer.Interval=TimerInterval();Sim.Recenter(Area);dashboard.RefreshSkin();while(Sim.Sugars.Count>Settings.MaxSugar)Sim.Sugars.RemoveAt(0);}
    void ReloadSettings(){var s=Settings.Load();Sim.ChangeSkin(s.Skin);Settings=s;Sim.Settings=s;timer.Interval=TimerInterval();Sim.Recenter(Area);dashboard.RefreshSkin();flySkinItem.Checked=s.Skin==PetSkin.Fly;roachSkinItem.Checked=s.Skin==PetSkin.Cockroach;if(Settings.LoadWarning!=null)tray.ShowBalloonTip(3000,"设置",Settings.LoadWarning,ToolTipIcon.Warning);}
    bool OnMouseDown(Point p)
    {
        // Hook returns immediately. Work is deferred to the normal event loop; no synchronous rendering or I/O.
        if(!visible||Paused)return false;
        if((dashboard.Visible&&dashboard.Bounds.Contains(p))||(settingsWindow?.Visible==true&&settingsWindow.Bounds.Contains(p))||(evidenceWindow?.Visible==true&&evidenceWindow.Bounds.Contains(p))||(brainMap?.Visible==true&&brainMap.Bounds.Contains(p))||(deathMenu?.Visible==true&&deathMenu.Bounds.Contains(p)))return false;
        if(tray.ContextMenuStrip?.Visible==true||!Area.Contains(p))return false;
        if(Vector2.Distance(new Vector2(p.X,p.Y),Sim.Position)<=Sim.DisplaySize*.55f+18)
        {pendingClick=p;pendingMode=ToolMode.Swatter;return true;}
        if(Mode==ToolMode.Sugar){pendingClick=p;pendingMode=ToolMode.Sugar;return true;}
        return false;
    }
    void Tick(object? sender,EventArgs e)
    {
        double now=clock.Elapsed.TotalSeconds;double elapsed=now-previous;previous=now;
        bool esc=(Native.GetAsyncKeyState(0x1b)&0x8000)!=0;if(esc&&!escaped)SetMode(ToolMode.Normal);escaped=esc;
        if(pendingClick is Point click)
        {
            pendingClick=null;var p=new Vector2(click.X,click.Y);
            if(pendingMode==ToolMode.Sugar){Sim.AddSugar(p);SetMode(ToolMode.Normal);}else if(pendingMode==ToolMode.Swatter)Sim.Hit(p);
        }
        var cost=Stopwatch.StartNew();float advanced=0;
        if(!Paused&&(visible||!Settings.PauseWhenHidden))
        {
            // A short accumulator handles variable UI frame intervals. Resume from sleep never fast-forwards death.
            accumulator+=Math.Min(elapsed,.20);
            var mouse=Control.MousePosition;
            while(accumulator>=1.0/120){Sim.Update(1f/120,Area,new(mouse.X,mouse.Y),visible);accumulator-=1.0/120;advanced+=1f/120;}
        }
        else accumulator=0;
        cost.Stop();ComputeMs=ComputeMs*.94+cost.Elapsed.TotalMilliseconds*.06;simulatedWindow+=advanced;
        drawElapsed+=elapsed;
        if(drawElapsed>=1.0/Settings.FramesPerSecond)
        {
            drawElapsed%=1.0/Settings.FramesPerSecond;
            if(visible){Draw();frameCounter++;}
            if(dashboard.Visible)dashboard.RefreshStatus();
            brainMap?.RefreshActivity();
        }
        if(now-statsPrevious>=1)
        {
            double duration=now-statsPrevious;MeasuredFps=frameCounter/duration;RealTimeRatio=simulatedWindow/duration;frameCounter=0;simulatedWindow=0;statsPrevious=now;
            evidenceWindow?.RefreshEvidence();tray.Text=$"{(Settings.Skin==PetSkin.Cockroach?"双马尾":"果蝇")} 生命{Sim.Health/Sim.MaxHealth*100:0}% 饱腹{Sim.Fullness:0}% · {(Settings.Invincible?"无敌":BehaviorLabel)}";
            if(Sim.ConsumeAlbinoAnnouncement())tray.ShowBalloonTip(6500,"FlyPet",Settings.Skin==PetSkin.Cockroach?"出金了！放大版美洲大蠊登场！":"出金了！是白眼果蝇！",ToolTipIcon.Info);
            if(now>=nextStatusSave){nextStatusSave=now+5;SaveStatus();}
        }
        UpdateDeathMenu(Control.MousePosition);
    }
    void Draw()
    {
        int size=Sim.DisplaySize;
        while(sugarWindows.Count<Sim.Sugars.Count){var w=new LayerWindow(48,"FlyPet · 糖粒");sugarWindows.Add(w);}
        while(sugarWindows.Count>Sim.Sugars.Count){sugarWindows[^1].Dispose();sugarWindows.RemoveAt(sugarWindows.Count-1);}
        for(int i=0;i<Sim.Sugars.Count;i++)
        {
            var s=Sim.Sugars[i];var w=sugarWindows[i];if(!w.Visible)w.Show();
            w.Render((int)s.Position.X-24,(int)s.Position.Y-24,48,g=>FlyRenderer.DrawSugar(g,48,s.Amount));
        }
        if(Sim.Dead&&!Sim.RemainsVisible)pet.Hide();
        else
        {
            if(!pet.Visible)pet.Show();
            pet.Render((int)Sim.Position.X-size/2,(int)Sim.Position.Y-size/2,size,g=>renderer.Draw(g,new(0,0,size,size),Sim,Area,Settings.ShowMeters));
            bool uiOpen=tray.ContextMenuStrip?.Visible==true||dashboard.Visible||settingsWindow?.Visible==true||evidenceWindow?.Visible==true||brainMap?.Visible==true||deathMenu?.Visible==true;
            if(!uiOpen)pet.BringToFront();
        }
        if(Settings.Skin==PetSkin.Cockroach&&!Sim.Dead&&Sim.SpeechRemaining>0)
        {
            if(!speech.Visible)speech.Show();
            int x=Math.Clamp((int)Sim.Position.X+size/4,Area.Left+4,Math.Max(Area.Left+4,Area.Right-344));
            int y=Math.Clamp((int)Sim.Position.Y-size/2-64,Area.Top+4,Math.Max(Area.Top+4,Area.Bottom-62));
            speech.Render(x,y,340,58,FlyRenderer.DrawSpeechBubble);
            speech.BringToFront();
        }
        else speech.Hide();
        if(Mode!=ToolMode.Normal)
        {
            var mouse=Control.MousePosition;if(!cursor.Visible)cursor.Show();
            cursor.Render(mouse.X-32,mouse.Y-32,84,g=>
            {
                if(Mode==ToolMode.Sugar){g.TranslateTransform(30,30);FlyRenderer.DrawSugar(g,48);return;}
                using var shaft=new Pen(Color.FromArgb(216,198,144),5);g.DrawLine(shaft,34,38,68,76);
                using var rim=new Pen(Color.FromArgb(220,181,174,115),3);g.DrawEllipse(rim,8,6,47,43);
                using var mesh=new Pen(Color.FromArgb(190,129,146,91),1);
                for(int k=17;k<50;k+=7){g.DrawLine(mesh,k,13,k,41);g.DrawLine(mesh,15,k,48,k);}
                using var dot=new SolidBrush(Color.FromArgb(220,233,220,165));g.FillRectangle(dot,30,30,4,4);
            });
        }
    }
    void UpdateDeathMenu(Point mouse)
    {
        float remainsRadius=Settings.Skin==PetSkin.Cockroach?Math.Max(36,Sim.DisplaySize*.35f):Sim.DisplaySize*.82f+58;
        bool overCorpse=visible&&Sim.Dead&&Sim.RemainsVisible&&Area.Contains(mouse)&&Vector2.Distance(new(mouse.X,mouse.Y),Sim.Position)<remainsRadius;
        if(deathMenu==null&&overCorpse){deathMenu=new DeathMenuWindow(()=>{Sim.Revive(Area);StartPet();},()=>{Sim.CleanRemains();});deathMenu.SetSkin(Settings.Skin);}
        if(deathMenu==null)return;
        if(overCorpse||deathMenu.Bounds.Contains(mouse))deathMenu.ShowAt(Area,new((int)Sim.Position.X,(int)Sim.Position.Y));else deathMenu.Hide();
    }
    void SaveStatus()
    {
        try{var path=Path.Combine(Settings.Folder,"status.json");File.WriteAllText(path,JsonSerializer.Serialize(new{timestamp=DateTimeOffset.Now,visible,Paused,mode=Mode.ToString(),skin=Settings.Skin.ToString(),size=Sim.DisplaySize,rare=Sim.Albino,behavior=Sim.Behavior,health=Sim.Health,fullness=Sim.Fullness,invincible=Settings.Invincible,grounded=Sim.Grounded,dead=Sim.Dead,deathCause=Sim.CauseOfDeath.ToString(),cockroachFlying=Sim.CockroachFlying,respawn=Sim.DeathRemaining,sugar=Sim.Sugars.Count,position=new{Sim.Position.X,Sim.Position.Y},fps=MeasuredFps,realtime=RealTimeRatio,computeMs=ComputeMs,neurons=Sim.Brain.NeuronCount,edges=Sim.Brain.EdgeCount,spikes=Sim.Brain.TotalSpikes,flight=Sim.Brain.Flight,fear=Sim.Brain.Fear,escapeUrgency=Sim.EscapeUrgency,escapeDistanceRate=Sim.EscapeDistanceRate,rapidEscapeTurn=Sim.RapidEscapeTurn,feeding=Sim.Brain.Feeding,olfactory=Sim.Brain.OlfactoryRate,memory=Sim.Brain.MemoryConfidence,avoidanceMemory=Sim.Brain.AvoidanceConfidence,avoidanceSkill=Sim.Brain.AvoidanceSkill,predictiveEdgeRisk=Sim.PredictiveEdgeRisk,learnedEdgeRisk=Sim.LearnedEdgeRisk,successfulEdgeAvoidances=Sim.SuccessfulEdgeAvoidances,reward=Sim.Brain.RewardSignal,edgeCollisions=Sim.EdgeCollisions},Settings.JsonOptions));}catch(IOException){}
    }
    protected override void ExitThreadCore()
    {
        if(disposing)return;disposing=true;timer.Stop();localControl.Dispose();mouseHook.Dispose();Native.timeEndPeriod(1);tray.Visible=false;tray.ContextMenuStrip?.Dispose();tray.Dispose();icon.Dispose();
        settingsWindow?.Dispose();evidenceWindow?.Dispose();brainMap?.Dispose();deathMenu?.Dispose();pet.Dispose();cursor.Dispose();speech.Dispose();foreach(var w in sugarWindows)w.Dispose();renderer.Dispose();timer.Dispose();dashboard.Shutdown();base.ExitThreadCore();
    }
}

public readonly record struct CausalAudit(float ConnectedDistance,float SeveredDistance,long ConnectedSpikes,long SeveredSpikes,float ConnectedFlight,float SeveredFlight,float ConnectedFear,float SeveredFear,float ConnectedFeed,float SeveredFeed)
{
    public bool Passed=>ConnectedDistance>25&&ConnectedFlight>SeveredFlight+.02f&&ConnectedSpikes!=SeveredSpikes;
}
