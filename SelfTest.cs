using System.Diagnostics;
using System.Drawing.Imaging;
using System.Numerics;
using System.Text.Json;

namespace FlyPet;

static class SelfTest
{
    public static int Run(string[] args)
    {
        string output=args.Length>1?args[^1]:Path.Combine(AppContext.BaseDirectory,"test-results");Directory.CreateDirectory(output);
        var results=new List<object>();int failures=0;
        void Check(string name,bool ok,object? detail=null){results.Add(new{name,passed=ok,detail});if(!ok)failures++;}
        var data=CircuitData.Load();var area=new Rectangle(0,0,1920,1080);var mouse=new Vector2(-5000,-5000);
        Simulation Make(Settings? settings=null){var s=new Simulation(settings??new Settings(),data,42);s.Recenter(area);return s;}
        void Advance(Simulation s,float seconds,bool swat=false,Vector2? cursor=null){for(int i=0;i<(int)(seconds*120);i++)s.Update(1f/120,area,cursor??mouse,swat);}
        Check("real_connectome_loaded",data.Nodes.Length==1800&&data.Edges.Length>100000,new{neurons=data.Nodes.Length,edges=data.Edges.Length});
        (float turn,float opto) ProbeTurn(float turn){var b=new Brain(data);var s=new Settings();b.SetInput(0,0,20,turn,.95f,0,s);for(int i=0;i<2000;i++)b.Step(1);return (b.Turn,b.OptomotorTurn);}
        var rightProbe=ProbeTurn(1);var leftProbe=ProbeTurn(-1);var centerProbe=ProbeTurn(0);
        Check("sided_neural_steering",rightProbe.turn>leftProbe.turn,new{rightDN=rightProbe.turn,leftDN=leftProbe.turn,centerDN=centerProbe.turn,rightOpto=rightProbe.opto,leftOpto=leftProbe.opto,centerOpto=centerProbe.opto});
        var sim=Make();var start=sim.Position;Advance(sim,8);
        Check("neural_flight_and_movement",Vector2.Distance(start,sim.Position)>80&&sim.Brain.Flight>.1f&&sim.Brain.TotalSpikes>100,new{distance=Vector2.Distance(start,sim.Position),flight=sim.Brain.Flight,spikes=sim.Brain.TotalSpikes,visual=sim.Brain.VisualRate,flightRate=sim.Brain.FlightRate,wing=sim.Brain.WingRate,escape=sim.Brain.EscapeRate,turn=sim.Brain.Turn,optomotor=sim.Brain.OptomotorRate});
        var noInput=Make(new(){SensoryGain=0});var start2=noInput.Position;Advance(noInput,8);
        Check("sensory_ablation_stops_flight",Vector2.Distance(start2,noInput.Position)<1&&noInput.Brain.TotalSpikes==0,new{distance=Vector2.Distance(start2,noInput.Position),spikes=noInput.Brain.TotalSpikes});
        var noSynapse=Make(new(){NeuralGain=0});Advance(noSynapse,8);
        Check("connectome_is_required_for_motor_output",Vector2.Distance(start2,noSynapse.Position)<1&&noSynapse.Brain.Flight<.001f&&Math.Abs(noSynapse.Brain.TotalSpikes-sim.Brain.TotalSpikes)>100,new{connectedSpikes=sim.Brain.TotalSpikes,severedSpikes=noSynapse.Brain.TotalSpikes,severedDistance=Vector2.Distance(start2,noSynapse.Position),severedFlight=noSynapse.Brain.Flight});
        var feed=Make();feed.Fullness=25;feed.AddSugar(feed.Position);Advance(feed,4);
        Check("sugar_contact_feeds_and_consumes",feed.Fullness>45&&feed.Sugars.Count==1&&feed.Sugars[0].Amount<85,new{fullness=feed.Fullness,sugar=feed.Sugars.FirstOrDefault()?.Amount,feeding=feed.Brain.Feeding});
        var seek=Make();seek.Fullness=20;seek.AddSugar(seek.Position+new Vector2(350,0));Advance(seek,20);
        Check("remote_sugar_attraction",seek.Fullness>30,new{fullness=seek.Fullness,distance=Vector2.Distance(seek.Position,seek.Sugars.FirstOrDefault()?.Position??seek.Position),x=seek.Position.X,y=seek.Position.Y,heading=seek.Heading,turn=seek.Brain.Turn,opto=seek.Brain.OptomotorRate,flight=seek.Brain.Flight});
        var scared=Make();var threat=scared.Position+new Vector2(60,0);float d0=Vector2.Distance(scared.Position,threat);Advance(scared,.7f,true,threat);
        Check("cursor_avoidance",Vector2.Distance(scared.Position,threat)>d0+20,new{before=d0,after=Vector2.Distance(scared.Position,threat),fear=scared.Brain.Fear});
        var dying=Make(new(){RespawnMinSeconds=1,RespawnMaxSeconds=2,SwatDamage=100});Check("swat_kills",dying.Hit(dying.Position)&&dying.Dead&&dying.DeathRemaining>=1&&dying.DeathRemaining<=2);
        Advance(dying,2.1f);Check("random_respawn",!dying.Dead&&dying.Health==100);
        var starving=Make(new(){StarvationDamagePerSecond=10});starving.Fullness=0;starving.Health=1;Advance(starving,.2f);Check("starvation_kills",starving.Dead);
        var capped=Make(new(){MaxSugar=3});for(int i=0;i<20;i++)capped.AddSugar(new(i,i));Check("bounded_food",capped.Sugars.Count==3);
        var bounds=Make();bounds.Position=new(-99999,99999);Advance(bounds,.02f);Check("display_bounds",area.Contains((int)bounds.Position.X,(int)bounds.Position.Y));
        var edgeOn=Make();edgeOn.Position=new(area.Left+65,area.Top+area.Height*.5f);Advance(edgeOn,.02f);
        var edgeOff=Make(new(){EdgeSensing=false});edgeOff.Position=edgeOn.Position;Advance(edgeOff,.02f);
        Check("edge_sensory_stimulus",edgeOn.Brain.WallDrive>.4f&&edgeOff.Brain.WallDrive==0,new{enabled=edgeOn.Brain.WallDrive,disabled=edgeOff.Brain.WallDrive});
        var protectedFly=Make(new(){Invincible=true,SwatDamage=100});Advance(protectedFly,.2f);protectedFly.Hit(protectedFly.Position);
        Check("invincible_hit_still_alarms",protectedFly.Health==100&&protectedFly.Alarm>0&&protectedFly.InjuryArousal>0,new{health=protectedFly.Health,alarm=protectedFly.Alarm,injury=protectedFly.InjuryArousal});
        Advance(protectedFly,3);Check("injury_arousal_persists",protectedFly.Alarm==0&&protectedFly.InjuryArousal>0);
        protectedFly.Revive(area);Check("revival_clears_injury",protectedFly.Health==100&&protectedFly.InjuryArousal==0&&protectedFly.Alarm==0);
        var flying=Make(new(){RestEnabled=false,HungerPerMinute=0});var resting=Make(new(){RestEnabled=false,HungerPerMinute=0,FlightFullnessCostPerSecond=0});
        flying.Fullness=resting.Fullness=20;
        Advance(flying,8);Advance(resting,8);
        Check("flight_consumes_fullness",flying.Fullness<resting.Fullness-.1f,new{flying=flying.Fullness,zeroFlightCost=resting.Fullness});
        var invalid=new Settings{FramesPerSecond=999,PetSize=-2,NeuralGain=float.NaN,RespawnMinSeconds=5,RespawnMaxSeconds=-9};invalid.Validate();Check("configuration_validation",invalid.FramesPerSecond==120&&invalid.PetSize==60&&invalid.NeuralGain==1&&invalid.RespawnMaxSeconds==5);
        var bench=Make();Advance(bench,2);var watch=Stopwatch.StartNew();Advance(bench,30);watch.Stop();
        Check("neural_simulation_faster_than_realtime",watch.Elapsed.TotalSeconds<30,new{simulatedSeconds=30,wallSeconds=watch.Elapsed.TotalSeconds,realtimeFactor=30/watch.Elapsed.TotalSeconds,processor=Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER")});
        using var renderer=new FlyRenderer();using var image=new Bitmap(1200,780);image.SetResolution(96,96);using var g=Graphics.FromImage(image);
        g.Clear(Theme.Bg);using var title=Theme.Font(24);using var titleBrush=new SolidBrush(Theme.Text);using var small=Theme.Font(11);using var muted=new SolidBrush(Theme.Muted);
        g.DrawString("FLYPET / 低多边形视角与动作",title,titleBrush,30,24);
        var variants=new[]{("左上 · 侧面",new Vector2(100,100),false,false), ("中央 · 飞行",new Vector2(960,540),false,false),("右下 · 反侧",new Vector2(1800,980),false,false),("低头进食",new Vector2(960,540),true,false),("死亡 · 翻转",new Vector2(960,540),false,true),("拍打 · 受伤",new Vector2(960,540),false,false)};
        for(int i=0;i<variants.Length;i++)
        {
            int x=30+i%3*390,y=92+i/3*330;using var tile=new SolidBrush(Theme.Panel);g.FillRectangle(tile,x,y,360,310);
            var v=variants[i];var s=Make();s.Position=v.Item2;s.Heading=i%3==1?.35f:0;s.Time=.31f;s.Behavior=v.Item3?"吃糖":"自由飞行";s.Health=v.Item4?0:100;s.HitFlash=i==5?.2f:0;
            renderer.Draw(g,new(x+40,y+4,280,280),s,area,true);g.DrawString(v.Item1,small,muted,x+18,y+277);
        }
        image.Save(Path.Combine(output,"appearance.png"),ImageFormat.Png);
        var renderWatch=Stopwatch.StartNew();for(int i=0;i<300;i++){sim.Time+=.016f;renderer.Draw(g,new(0,0,180,180),sim,area);}renderWatch.Stop();
        Check("render_budget_90fps",renderWatch.Elapsed.TotalMilliseconds/300<11.12,new{averageMs=renderWatch.Elapsed.TotalMilliseconds/300});
        File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(new{passed=failures==0,failures,tests=results},Settings.JsonOptions));
        return failures==0?0:1;
    }
}
