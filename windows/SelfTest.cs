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
        var defaults=new Settings();Check("default_parameters",defaults.PetSize==90&&Math.Abs(defaults.AlbinoChance-.1f)<.0001f&&Math.Abs(defaults.GiantCockroachChance-.1f)<.0001f&&defaults.RespawnMinSeconds==45&&defaults.RespawnMaxSeconds==90&&100/defaults.StarvationDamagePerSecond>=1200,new{petSize=defaults.PetSize,albinoChance=defaults.AlbinoChance,giantChance=defaults.GiantCockroachChance,respawnMin=defaults.RespawnMinSeconds,respawnMax=defaults.RespawnMaxSeconds,starvationMinutes=100/defaults.StarvationDamagePerSecond/60});
        var enlarged=new Settings{PetSize=400};enlarged.Validate();Check("pet_size_400_is_preserved",enlarged.PetSize==400,new{size=enlarged.PetSize,max=Settings.MaxPetSize});
        var giantSettings=new Settings{Skin=PetSkin.Cockroach,CockroachSize=120,GiantCockroachSize=700};giantSettings.Validate();
        Check("giant_size_700_is_preserved",giantSettings.GiantCockroachSize==700,new{size=giantSettings.GiantCockroachSize,max=Settings.MaxPetSize});
        var normalProbe=Make(new(){Skin=PetSkin.Cockroach,CockroachSize=120,GiantCockroachSize=700,GiantCockroachChance=0});
        var giantProbe=Make(new(){Skin=PetSkin.Cockroach,CockroachSize=120,GiantCockroachSize=700,GiantCockroachChance=1});
        Check("cockroach_rare_roll_and_sizes",!normalProbe.Albino&&normalProbe.DisplaySize==120&&giantProbe.Albino&&giantProbe.DisplaySize==700,new{normal=normalProbe.DisplaySize,giant=giantProbe.DisplaySize});
        giantProbe.Velocity=new(700,0);giantProbe.Update(1f/120,area,mouse,false);
        bool giantWalksAtOrdinarySpeed=!giantProbe.CockroachFlying;
        giantProbe.Velocity=new(1100,0);giantProbe.Update(1f/120,area,mouse,false);
        bool giantOpensWingsAtHighSpeed=giantProbe.CockroachFlying;
        giantProbe.Velocity=new(120,0);giantProbe.Update(1f/120,area,mouse,false);
        Check("giant_can_walk_fly_and_land",giantWalksAtOrdinarySpeed&&giantOpensWingsAtHighSpeed&&!giantProbe.CockroachFlying,new{giantWalksAtOrdinarySpeed,giantOpensWingsAtHighSpeed,landed=!giantProbe.CockroachFlying});
        var roamingGiant=Make(new(){Skin=PetSkin.Cockroach,GiantCockroachChance=1});bool walkedNaturally=false,flewNaturally=false;
        for(int i=0;i<2400;i++){roamingGiant.Update(1f/120,area,mouse,false);walkedNaturally|=!roamingGiant.CockroachFlying&&roamingGiant.Velocity.Length()>10;flewNaturally|=roamingGiant.CockroachFlying;}
        Check("giant_roams_on_legs_and_flies",walkedNaturally&&flewNaturally,new{walkedNaturally,flewNaturally});
        float minimumDefaultLifetimeSeconds=65/(defaults.HungerPerMinute/60+defaults.FlightFullnessCostPerSecond)+100/defaults.StarvationDamagePerSecond;
        Check("default_hunger_survives_over_twenty_minutes",minimumDefaultLifetimeSeconds>=1200,new{seconds=minimumDefaultLifetimeSeconds,minutes=minimumDefaultLifetimeSeconds/60});
        Check("expanded_connectome_loaded",data.Nodes.Length==16711&&data.Edges.Length>2000000,new{neurons=data.Nodes.Length,edges=data.Edges.Length});
        Check("expanded_functional_groups",new[]{"visual_motion","olfactory","memory","reward","navigation","descending","motor"}.All(g=>data.Nodes.Any(n=>n.Groups.Contains(g))),new{olfactory=data.Nodes.Count(n=>n.Groups.Contains("olfactory")),memory=data.Nodes.Count(n=>n.Groups.Contains("memory")),navigation=data.Nodes.Count(n=>n.Groups.Contains("navigation")),descending=data.Nodes.Count(n=>n.Groups.Contains("descending"))});
        (float turn,float opto) ProbeTurn(float turn){var b=new Brain(data);var s=new Settings();b.SetInput(0,0,20,turn,.95f,0,s);for(int i=0;i<2000;i++)b.Step(1);return (b.Turn,b.OptomotorTurn);}
        var rightProbe=ProbeTurn(1);var leftProbe=ProbeTurn(-1);var centerProbe=ProbeTurn(0);
        Check("sided_neural_steering",rightProbe.opto>leftProbe.opto+.2f,new{rightDN=rightProbe.turn,leftDN=leftProbe.turn,centerDN=centerProbe.turn,rightOpto=rightProbe.opto,leftOpto=leftProbe.opto,centerOpto=centerProbe.opto});
        var baselineEscape=new Brain(data);baselineEscape.SetInput(0,0,65,0,.95f,0,new Settings());for(int i=0;i<2000;i++)baselineEscape.Step(1);
        Check("escape_output_requires_threat_context",baselineEscape.Fear<.001f,new{rawEscapeRate=baselineEscape.EscapeRate,effectiveFear=baselineEscape.Fear});
        var sim=Make();var start=sim.Position;Advance(sim,8);
        Check("neural_flight_and_movement",Vector2.Distance(start,sim.Position)>80&&sim.Brain.Flight>.03f&&sim.Brain.TotalSpikes>100,new{distance=Vector2.Distance(start,sim.Position),flight=sim.Brain.Flight,fear=sim.Brain.Fear,spikes=sim.Brain.TotalSpikes,visual=sim.Brain.VisualRate,navigation=sim.Brain.NavigationRate,descending=sim.Brain.DescendingRate,motor=sim.Brain.MotorRate,flightRate=sim.Brain.FlightRate,wing=sim.Brain.WingRate,escapeRaw=sim.Brain.EscapeRate,turn=sim.Brain.Turn,optomotor=sim.Brain.OptomotorRate});
        var noInput=Make(new(){SensoryGain=0});var start2=noInput.Position;Advance(noInput,8);
        Check("sensory_ablation_stops_flight",Vector2.Distance(start2,noInput.Position)<1&&noInput.Brain.TotalSpikes==0,new{distance=Vector2.Distance(start2,noInput.Position),spikes=noInput.Brain.TotalSpikes});
        var noSynapse=Make(new(){NeuralGain=0});Advance(noSynapse,8);
        Check("connectome_is_required_for_motor_output",Vector2.Distance(start2,noSynapse.Position)<1&&noSynapse.Brain.Flight<.001f&&Math.Abs(noSynapse.Brain.TotalSpikes-sim.Brain.TotalSpikes)>100,new{connectedSpikes=sim.Brain.TotalSpikes,severedSpikes=noSynapse.Brain.TotalSpikes,severedDistance=Vector2.Distance(start2,noSynapse.Position),severedFlight=noSynapse.Brain.Flight});
        var feed=Make(new(){SugarEatingSeconds=1.8f});feed.Fullness=25;feed.AddSugar(feed.Position);Advance(feed,.6f);
        Check("sugar_requires_feeding_time",feed.Sugars.Count==1&&feed.Fullness<30,new{fullness=feed.Fullness,sugarCount=feed.Sugars.Count,behavior=feed.Behavior,feeding=feed.Brain.Feeding});
        Advance(feed,3.4f);
        float afterSugar=feed.Fullness;Advance(feed,2);
        Check("sugar_is_consumed_once",afterSugar>45&&feed.Sugars.Count==0&&Math.Abs(feed.Fullness-afterSugar)<1,new{fullness=feed.Fullness,sugarCount=feed.Sugars.Count,feeding=feed.Brain.Feeding});
        var seek=Make();seek.Fullness=20;seek.AddSugar(seek.Position+new Vector2(350,0));Advance(seek,24);
        Check("remote_sugar_attraction",seek.Fullness>30,new{fullness=seek.Fullness,distance=Vector2.Distance(seek.Position,seek.Sugars.FirstOrDefault()?.Position??seek.Position),x=seek.Position.X,y=seek.Position.Y,heading=seek.Heading,turn=seek.Brain.Turn,opto=seek.Brain.OptomotorRate,flight=seek.Brain.Flight});
        var smell=Make(new(){SugarAttractionRadius=2200,HungerPerMinute=0,RestEnabled=false});var noSmell=Make(new(){SugarAttractionRadius=100,HungerPerMinute=0,RestEnabled=false});
        var smellSugar=smell.Position+new Vector2(420,0);smell.Fullness=noSmell.Fullness=20;smell.AddSugar(smellSugar);noSmell.AddSugar(smellSugar);
        int smellFrames=960,noSmellFrames=960;for(int i=0;i<960;i++){smell.Update(1f/120,area,mouse,false);if(smell.Sugars.Count==0){smellFrames=i;break;}}
        for(int i=0;i<960;i++){noSmell.Update(1f/120,area,mouse,false);if(noSmell.Sugars.Count==0){noSmellFrames=i;break;}}
        float smellDistance=smell.Sugars.Count==0?0:Vector2.Distance(smell.Position,smellSugar),noSmellDistance=noSmell.Sugars.Count==0?0:Vector2.Distance(noSmell.Position,smellSugar);
        Check("olfactory_circuit_improves_sugar_approach",smellFrames+60<noSmellFrames||smellDistance+40<noSmellDistance,new{smellFrames,noSmellFrames,smellDistance,noSmellDistance,olfactory=smell.Brain.OlfactoryRate});
        var scared=Make();var threat=scared.Position+new Vector2(60,0);float d0=Vector2.Distance(scared.Position,threat);Advance(scared,.7f,true,threat);
        Check("cursor_avoidance",Vector2.Distance(scared.Position,threat)>d0+20,new{before=d0,after=Vector2.Distance(scared.Position,threat),fear=scared.Brain.Fear});
        var turning=Make(new(){RestEnabled=false});turning.Heading=0;var turnThreat=turning.Position+new Vector2(60,0);Advance(turning,.45f,true,turnThreat);
        Check("cursor_escape_uses_turning_circuit",Math.Abs(turning.Heading)>.30f,new{heading=turning.Heading,optomotor=turning.Brain.OptomotorTurn,descending=turning.Brain.DescendingTurn,turn=turning.Brain.Turn});
        var adaptiveEscape=Make(new(){RestEnabled=false});bool usedRapidTurn=false;
        for(int i=0;i<180;i++){adaptiveEscape.Update(1f/120,area,adaptiveEscape.Position+new Vector2(80,0),true);usedRapidTurn|=adaptiveEscape.RapidEscapeTurn;}
        float stalledUrgency=adaptiveEscape.EscapeUrgency;
        Check("failed_escape_accumulates_acceleration",stalledUrgency>.75f&&usedRapidTurn,new{urgency=stalledUrgency,distanceRate=adaptiveEscape.EscapeDistanceRate,rapidTurn=usedRapidTurn,speed=adaptiveEscape.Velocity.Length()});
        for(int i=0;i<24;i++)adaptiveEscape.Update(1f/120,area,adaptiveEscape.Position+new Vector2(adaptiveEscape.Settings.FearRadius*1.1f,0),true);
        Check("escape_persists_past_detection_radius",adaptiveEscape.Brain.ThreatDrive>.1f&&adaptiveEscape.EscapeUrgency>.1f,new{threatDrive=adaptiveEscape.Brain.ThreatDrive,urgency=adaptiveEscape.EscapeUrgency});
        for(int i=0;i<90;i++)adaptiveEscape.Update(1f/120,area,adaptiveEscape.Position+new Vector2(adaptiveEscape.Settings.FearRadius*adaptiveEscape.Settings.EscapeSafeRadiusMultiplier*1.2f,0),true);
        Check("escape_releases_after_safe_distance",adaptiveEscape.EscapeUrgency<.01f,new{urgency=adaptiveEscape.EscapeUrgency,distanceRate=adaptiveEscape.EscapeDistanceRate});
        var dying=Make(new(){RespawnMinSeconds=1,RespawnMaxSeconds=2,SwatDamage=100});Check("swat_kills",dying.Hit(dying.Position)&&dying.Dead&&dying.CauseOfDeath==DeathCause.Swatted&&dying.DeathRemaining>=1&&dying.DeathRemaining<=2);
        Advance(dying,2.1f);Check("random_respawn",!dying.Dead&&dying.Health==100);
        var starving=Make(new(){StarvationDamagePerSecond=10});starving.Fullness=0;starving.Health=1;Advance(starving,.2f);Check("starvation_kills",starving.Dead&&starving.CauseOfDeath==DeathCause.Starved);
        var capped=Make(new(){MaxSugar=3});for(int i=0;i<20;i++)capped.AddSugar(new(i,i));Check("bounded_food",capped.Sugars.Count==3);
        var bounds=Make();bounds.Position=new(-99999,99999);Advance(bounds,.02f);Check("display_bounds",area.Contains((int)bounds.Position.X,(int)bounds.Position.Y));
        var edgeOn=Make();edgeOn.Position=new(area.Left+65,area.Top+area.Height*.5f);Advance(edgeOn,.02f);
        var edgeOff=Make(new(){EdgeSensing=false});edgeOff.Position=edgeOn.Position;Advance(edgeOff,.02f);
        Check("edge_sensory_stimulus",edgeOn.Brain.WallDrive>.4f&&edgeOff.Brain.WallDrive==0,new{enabled=edgeOn.Brain.WallDrive,disabled=edgeOff.Brain.WallDrive});
        var navigationProbe=new Brain(data);for(int frame=0;frame<240;frame++){navigationProbe.SetInput(0,0,60,1,.95f,.9f,new Settings(),wallTurn:1,positionX:.05f,positionY:.5f,frameDt:1f/120);for(int ms=0;ms<8;ms++)navigationProbe.Step(1);}
        Check("edge_input_activates_navigation_circuit",navigationProbe.NavigationRate>.2f&&navigationProbe.NavigationTurn>.02f,new{rate=navigationProbe.NavigationRate,turn=navigationProbe.NavigationTurn,visualMotion=navigationProbe.VisualMotionRate});
        var protectedFly=Make(new(){Invincible=true,SwatDamage=100});Advance(protectedFly,.2f);protectedFly.Hit(protectedFly.Position);
        Check("invincible_hit_still_alarms",protectedFly.Health==100&&protectedFly.Alarm>0&&protectedFly.InjuryArousal>0,new{health=protectedFly.Health,alarm=protectedFly.Alarm,injury=protectedFly.InjuryArousal});
        Advance(protectedFly,3);Check("injury_arousal_persists",protectedFly.Alarm<1&&protectedFly.InjuryArousal>0);
        protectedFly.Revive(area);Check("revival_clears_injury",protectedFly.Health==100&&protectedFly.InjuryArousal==0&&protectedFly.Alarm==0);
        var collision=Make(new(){Invincible=true});collision.Position=new(area.Left-200,area.Top+area.Height*.5f);float collisionHealth=collision.Health;Advance(collision,.05f);
        Check("edge_collision_is_nonlethal_negative_input",collision.Health==collisionHealth&&collision.EdgeCollisions>0&&collision.Brain.RewardSignal<0,new{health=collision.Health,collisions=collision.EdgeCollisions,reward=collision.Brain.RewardSignal,wall=collision.Brain.WallDrive});
        var learner=new Brain(data);var learningSettings=new Settings();
        for(int frame=0;frame<240;frame++){learner.SetInput(0,0,25,0,.95f,0,learningSettings,odor:.7f,positionX:.78f,positionY:.22f,reward:1,frameDt:1f/120);for(int ms=0;ms<8;ms++)learner.Step(1);}
        var learned=learner.ExportMemory();var restored=new Brain(data);restored.ImportMemory(learned);
        Check("reward_memory_learns_sugar_place",learner.MemoryConfidence>.1f&&Math.Abs(learner.RememberedX-.78f)<.12f&&Math.Abs(learner.RememberedY-.22f)<.12f&&Math.Abs(restored.MemoryConfidence-learner.MemoryConfidence)<.001f,new{confidence=learner.MemoryConfidence,x=learner.RememberedX,y=learner.RememberedY});
        var avoidLearner=new Brain(data);for(int frame=0;frame<240;frame++){avoidLearner.SetInput(0,0,25,0,.95f,.9f,learningSettings,wallTurn:1,positionX:.04f,positionY:.52f,punishment:1,frameDt:1f/120);for(int ms=0;ms<8;ms++)avoidLearner.Step(1);}
        avoidLearner.SetInput(0,0,25,0,.95f,0,learningSettings,positionX:.13f,positionY:.52f,frameDt:1f/120);
        Check("negative_memory_learns_edge_place",avoidLearner.AvoidanceConfidence>.1f&&avoidLearner.AvoidedX<.15f&&Math.Abs(avoidLearner.AvoidedY-.52f)<.15f&&avoidLearner.LocalAvoidanceConfidence>.1f&&avoidLearner.AvoidanceVectorX>0,new{confidence=avoidLearner.AvoidanceConfidence,local=avoidLearner.LocalAvoidanceConfidence,x=avoidLearner.AvoidedX,y=avoidLearner.AvoidedY,awayX=avoidLearner.AvoidanceVectorX});
        float collisionRadiusX=150f/area.Width,collisionRadiusY=150f/area.Height;
        var collisionRange=new Brain(data);collisionRange.LearnCollision(.5f,.027f,collisionRadiusX,collisionRadiusY,.22f);
        collisionRange.SetInput(0,0,60,0,.95f,0,learningSettings,positionX:.56f,positionY:.08f,frameDt:1f/120,avoidanceRadiusX:collisionRadiusX,avoidanceRadiusY:collisionRadiusY);float nearbyCollisionMemory=collisionRange.LocalAvoidanceConfidence;
        collisionRange.SetInput(0,0,60,0,.95f,0,learningSettings,positionX:.72f,positionY:.08f,frameDt:1f/120,avoidanceRadiusX:collisionRadiusX,avoidanceRadiusY:collisionRadiusY);float distantCollisionMemory=collisionRange.LocalAvoidanceConfidence;
        Check("collision_memory_has_300px_diameter",nearbyCollisionMemory>.02f&&nearbyCollisionMemory>distantCollisionMemory*3,new{nearbyCollisionMemory,distantCollisionMemory,diameter=300});
        var predictiveAvoidance=new Brain(data);predictiveAvoidance.LearnCollision(.5f,.027f,collisionRadiusX,collisionRadiusY,.36f);
        predictiveAvoidance.SetInput(0,0,60,0,.95f,0,learningSettings,positionX:.50f,positionY:.13f,frameDt:1f/120,avoidanceRadiusX:collisionRadiusX,avoidanceRadiusY:collisionRadiusY);
        Check("one_collision_creates_inward_preemptive_turn",predictiveAvoidance.LocalAvoidanceConfidence>.18f&&predictiveAvoidance.AvoidanceVectorY>.7f,new{local=predictiveAvoidance.LocalAvoidanceConfidence,awayY=predictiveAvoidance.AvoidanceVectorY});
        var topCoverage=new Brain(data);for(int i=0;i<16;i++)topCoverage.LearnCollision((i+.5f)/16,.027f,collisionRadiusX,collisionRadiusY,.22f);
        float weakestTopMemory=1;for(int i=0;i<32;i++){topCoverage.SetInput(0,0,60,0,.95f,0,learningSettings,positionX:(i+.5f)/32,positionY:.08f,frameDt:1f/120,avoidanceRadiusX:collisionRadiusX,avoidanceRadiusY:collisionRadiusY);weakestTopMemory=Math.Min(weakestTopMemory,topCoverage.LocalAvoidanceConfidence);}
        Check("sixteen_impacts_cover_top_edge",weakestTopMemory>.02f,new{impacts=16,weakestTopMemory,diameter=300});
        var blankApproach=Make(new(){EdgeSensing=true,RestEnabled=false,HungerPerMinute=0,FlightFullnessCostPerSecond=0,MemoryDecayPerMinute=0});blankApproach.Position=new(600,115);blankApproach.Heading=0;blankApproach.Velocity=new(0,-120);Advance(blankApproach,1);
        var learnedApproach=Make(new(){EdgeSensing=true,RestEnabled=false,HungerPerMinute=0,FlightFullnessCostPerSecond=0,MemoryDecayPerMinute=0});learnedApproach.Brain.ImportMemory(topCoverage.ExportMemory());learnedApproach.Position=new(600,115);learnedApproach.Heading=0;learnedApproach.Velocity=new(0,-120);Advance(learnedApproach,1);
        Check("learned_edge_memory_prevents_repeat_approach",blankApproach.EdgeCollisions>0&&learnedApproach.EdgeCollisions==0&&learnedApproach.SuccessfulEdgeAvoidances>0,new{blank=blankApproach.EdgeCollisions,learned=learnedApproach.EdgeCollisions,successes=learnedApproach.SuccessfulEdgeAvoidances,skill=learnedApproach.Brain.AvoidanceSkill,risk=learnedApproach.LearnedEdgeRisk,navigation=learnedApproach.Brain.NavigationRate});
        var connectedAvoidance=new Brain(data);connectedAvoidance.ImportMemory(topCoverage.ExportMemory());var severedAvoidance=new Brain(data);severedAvoidance.ImportMemory(topCoverage.ExportMemory());
        for(int frame=0;frame<120;frame++){connectedAvoidance.SetInput(0,0,60,1,.95f,.8f,learningSettings,wallTurn:1,positionX:.31f,positionY:.1f,frameDt:1f/120,avoidanceRadiusX:collisionRadiusX,avoidanceRadiusY:collisionRadiusY);severedAvoidance.SetInput(0,0,60,1,.95f,.8f,learningSettings,wallTurn:1,positionX:.31f,positionY:.1f,frameDt:1f/120,avoidanceRadiusX:collisionRadiusX,avoidanceRadiusY:collisionRadiusY);for(int ms=0;ms<8;ms++){connectedAvoidance.Step(1);severedAvoidance.Step(0);}}
        Check("learned_avoidance_requires_neural_firing",connectedAvoidance.NavigationTurn>.25f&&Math.Abs(severedAvoidance.NavigationTurn)<.01f,new{connected=connectedAvoidance.NavigationTurn,severed=severedAvoidance.NavigationTurn,connectedRate=connectedAvoidance.NavigationRate,severedRate=severedAvoidance.NavigationRate});
        var freshLife=Make();freshLife.Brain.ImportMemory(avoidLearner.ExportMemory());freshLife.Revive(area);
        Check("new_fly_starts_with_blank_memory",freshLife.Brain.AvoidanceConfidence==0&&freshLife.Brain.MemoryConfidence==0,new{positive=freshLife.Brain.MemoryConfidence,negative=freshLife.Brain.AvoidanceConfidence});
        var albino=Make(new(){AlbinoChance=1,AlbinoSpeedMultiplier=1.5f});Check("albino_hidden_skin",albino.Albino&&albino.Health==albino.MaxHealth&&albino.MaxHealth==180);
        var corpse=Make(new(){SwatDamage=100,RespawnMinSeconds=1,RespawnMaxSeconds=1});corpse.Hit(corpse.Position);corpse.CleanRemains();Advance(corpse,2);Check("cleaned_corpse_stays_clean",corpse.Dead&&!corpse.RemainsVisible);
        Check("cleaned_corpse_cannot_reopen_death_menu",!PetApplication.ShouldKeepDeathMenu(true,corpse.Dead,corpse.RemainsVisible,false,true),new{corpse.Dead,corpse.RemainsVisible,mouseOverOldMenu=true});
        Check("living_pet_cannot_open_death_menu",!PetApplication.ShouldKeepDeathMenu(true,false,true,false,true),new{dead=false,remainsVisible=true,mouseOverOldMenu=true});
        var flying=Make(new(){RestEnabled=false,HungerPerMinute=0});var resting=Make(new(){RestEnabled=false,HungerPerMinute=0,FlightFullnessCostPerSecond=0});
        flying.Fullness=resting.Fullness=20;
        Advance(flying,8);Advance(resting,8);
        Check("flight_consumes_fullness",flying.Fullness<resting.Fullness-.03f,new{flying=flying.Fullness,zeroFlightCost=resting.Fullness});
        var healing=Make(new(){HungerPerMinute=0,FlightFullnessCostPerSecond=0,SatiatedThreshold=80,SatiatedRegenPerSecond=2});healing.Fullness=90;healing.Health=50;Advance(healing,2);
        Check("satiated_fly_regenerates_health",healing.Health>=53.8f,new{health=healing.Health,fullness=healing.Fullness});
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
        using(var deathImage=new Bitmap(900,350))using(var dg=Graphics.FromImage(deathImage))
        {
            dg.Clear(Theme.Bg);
            var flyStarved=Make(new(){StarvationDamagePerSecond=10});flyStarved.Fullness=0;flyStarved.Health=1;Advance(flyStarved,.2f);
            var flySwatted=Make(new(){SwatDamage=100});flySwatted.Hit(flySwatted.Position);
            var roachDead=Make(new(){Skin=PetSkin.Cockroach,GiantCockroachChance=1,SwatDamage=100});roachDead.Health=1;roachDead.Hit(roachDead.Position);
            Check("roach_death_leaves_small_remains",roachDead.Dead&&roachDead.DisplaySize==120,new{roachDead.CauseOfDeath,size=roachDead.DisplaySize});
            var samples=new[]{("果蝇 · 饿死",flyStarved),("果蝇 · 拍死",flySwatted),("双马尾 · 白卵",roachDead)};
            for(int i=0;i<samples.Length;i++)
            {
                int x=i*300;using var tile=new SolidBrush(Theme.Panel);dg.FillRectangle(tile,x+5,6,290,338);
                renderer.Draw(dg,new(x+34,22,230,230),samples[i].Item2,area,false);
                using var caption=Theme.Font(9);dg.DrawString(samples[i].Item1,caption,muted,x+22,276);
            }
            deathImage.Save(Path.Combine(output,"death-appearance.png"),ImageFormat.Png);
        }
        using(var bubbleImage=new Bitmap(340,58))using(var bg=Graphics.FromImage(bubbleImage))
        {bg.Clear(Color.Transparent);FlyRenderer.DrawSpeechBubble(bg);bubbleImage.Save(Path.Combine(output,"speech-bubble.png"),ImageFormat.Png);}
        using(var roachImage=new Bitmap(1000,800))using(var rg=Graphics.FromImage(roachImage))
        {
            using var roachTitle=Theme.Font(17);rg.Clear(Theme.Bg);rg.DrawString("FLYPET / 广东双马尾 · 奔跑与飞行",roachTitle,titleBrush,24,18);
            for(int i=0;i<4;i++)
            {
                int x=24+i%2*488,y=74+i/2*350;
                using var tile=new SolidBrush(Theme.Panel);rg.FillRectangle(tile,x,y,464,330);
                var rs=Make(new(){Skin=PetSkin.Cockroach,CockroachSize=120,GiantCockroachSize=500,GiantCockroachChance=i>=2?1:0});
                rs.Time=.15f+i*.12f;rs.Heading=i%2==0?0:.38f;
                rs.Velocity=i%2==0?new Vector2(130,0):new Vector2(1100,0);
                rs.Update(1f/120,area,mouse,false);
                int sprite=i<2?200:290;renderer.Draw(rg,new(x+(464-sprite)/2,y+8,sprite,sprite),rs,area,false);
                rg.DrawString(i<2?(i==0?"普通 · 奔跑":"普通 · 飞行"):(i==2?"巨型 · 奔跑":"巨型 · 飞行"),small,muted,x+20,y+294);
            }
            roachImage.Save(Path.Combine(output,"cockroach-appearance.png"),ImageFormat.Png);
        }
        using(var layer=new LayerWindow(120,"size-check"))
        {
            layer.Render(100,100,700,g=>g.Clear(Color.Transparent));
            Check("layer_window_reaches_700",layer.Width==700&&layer.Height==700,new{layer.Width,layer.Height});
        }
        var renderWatch=Stopwatch.StartNew();for(int i=0;i<300;i++){sim.Time+=.016f;renderer.Draw(g,new(0,0,180,180),sim,area);}renderWatch.Stop();
        Check("render_budget_90fps",renderWatch.Elapsed.TotalMilliseconds/300<11.12,new{averageMs=renderWatch.Elapsed.TotalMilliseconds/300});
        File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(new{passed=failures==0,failures,tests=results},Settings.JsonOptions));
        return failures==0?0:1;
    }
}
