using System.Numerics;

namespace FlyPet;

public sealed class Sugar(Vector2 position)
{
    public Vector2 Position=position;
    public float Amount=100,Age;
    public bool Consumed {get;private set;}
    public bool TryConsume(){if(Consumed)return false;Consumed=true;Amount=0;return true;}
}

public enum DeathCause { None, Swatted, Starved }
public enum DefenseMove { None, Evanescence, RockSolid }

public sealed class Simulation
{
    public readonly Brain Brain;
    public Settings Settings;
    public Vector2 Position,Velocity;
    public float Heading,Health=100,Fullness=100,DeathRemaining,HitFlash,Time,Alarm,InjuryArousal,RestRemaining;
    public bool Dead=>Health<=0;
    public bool Albino { get; private set; }
    public int DisplaySize => Settings.Skin==PetSkin.Cockroach ? (Dead?Math.Min(120,Settings.CockroachSize):Albino?Settings.GiantCockroachSize:Settings.CockroachSize) : Settings.PetSize;
    // Match the projected model footprint; transparent sprite margins are not clickable.
    public float HitRadius=>DisplaySize*.40f;
    public bool AlbinoSpawnedPending { get; private set; }
    public bool RemainsVisible { get; private set; } = true;
    public float MaxHealth => Albino ? 180 : 100;
    public bool Grounded {get;private set;}
    public bool CockroachFlying {get;private set;}
    public DeathCause CauseOfDeath {get;private set;}
    public float SpeechRemaining {get;private set;}
    public float VoiceAttentionRemaining {get;private set;}
    float voiceAttentionHeading;
    float speechDelay=6;
    public float EscapeUrgency {get;private set;}
    public float EscapeDistanceRate {get;private set;}
    public bool RapidEscapeTurn=>rapidTurnRemaining>0;
    public DefenseMove Defense {get;private set;}
    public float DefenseRemaining {get;private set;}
    public float InvulnerabilityRemaining {get;private set;}
    public float DefenseProgress=>Defense==DefenseMove.None?1:1-DefenseRemaining/(Defense==DefenseMove.Evanescence?.6f:.52f);
    public Vector2 DefenseOrigin {get;private set;}
    public Vector2 DefenseDestination {get;private set;}
    public int DefenseSerial {get;private set;}
    public string Behavior="苏醒";
    public readonly List<Sugar> Sugars=[];
    readonly Random random;
    Vector2 wander;
    Sugar? feedingSugar;
    float feedingElapsed;
    float wanderRemaining,neuralRemainder,hitCooldown,flightDuration,burstRemaining,burstCooldown,edgeShock,rewardPulse,punishmentPulse,avoidanceRewardPulse;
    float escapeIntegral,previousMouseDistance=float.NaN,escapeSafeTime,rapidTurnRemaining,rapidTurnCooldown;
    float avoidanceEpisodePeak,avoidanceSuccessRemaining;
    int rapidTurnSign;
    bool escapeActive,avoidanceEpisode;
    Vector2 dodgeDirection;
    public int EdgeCollisions {get;private set;}
    public int SuccessfulEdgeAvoidances {get;private set;}
    public float PredictiveEdgeRisk {get;private set;}
    public float LearnedEdgeRisk {get;private set;}
    public Simulation(Settings settings,CircuitData data,int seed=0){Settings=settings;Brain=new(data);random=seed==0?new Random():new Random(seed);RollSkin();Health=MaxHealth;}
    public void Recenter(Rectangle area){Position=new(area.Left+area.Width*.55f,area.Top+area.Height*.45f);wander=Position;Velocity=Vector2.Zero;Defense=DefenseMove.None;DefenseRemaining=0;InvulnerabilityRemaining=0;}
    public void AddSugar(Vector2 p){if(Sugars.Count>=Settings.MaxSugar)Sugars.RemoveAt(0);Sugars.Add(new(p));}
    public void AttendToVoice(Rectangle area,float seconds)
    {
        if(Dead)return;var center=new Vector2(area.Left+area.Width*.5f,area.Top+area.Height*.5f);var direction=center-Position;
        if(direction.LengthSquared()>.01f)voiceAttentionHeading=MathF.Atan2(direction.Y,direction.X)+MathF.PI/2;
        VoiceAttentionRemaining=Math.Max(VoiceAttentionRemaining,seconds);Velocity=Vector2.Zero;Grounded=true;CockroachFlying=false;Behavior="正在聆听";
    }
    public bool Hit(Vector2 point,bool allowDefense=true)
    {
        if(Dead||Vector2.Distance(point,Position)>HitRadius)return false;
        if(DefenseRemaining>0||InvulnerabilityRemaining>0)return true;
        if(hitCooldown>0)return false;
        if(allowDefense)
        {
            double roll=random.NextDouble();
            if(roll<Settings.EvanescenceChance){StartDefense(DefenseMove.Evanescence,point);return true;}
            if(roll<Settings.EvanescenceChance+Settings.RockSolidChance){StartDefense(DefenseMove.RockSolid,point);return true;}
        }
        HitFlash=.28f;hitCooldown=.22f;Alarm=1;InjuryArousal=Math.Min(1,InjuryArousal+.30f);RestRemaining=0;
        if(!Settings.Invincible)Health=Math.Max(0,Health-Settings.SwatDamage*(Albino ? .58f : 1));
        if(Dead)Die(DeathCause.Swatted);
        return true;
    }
    void StartDefense(DefenseMove move,Vector2 point)
    {
        Defense=move;DefenseRemaining=move==DefenseMove.Evanescence?.6f:.52f;
        // A small recovery tail prevents a click on the exact animation boundary.
        InvulnerabilityRemaining=move==DefenseMove.Evanescence?.72f:.68f;
        DefenseOrigin=Position;DefenseSerial++;hitCooldown=DefenseRemaining;
        HitFlash=0;Alarm=1;RestRemaining=0;InjuryArousal=Math.Min(1,InjuryArousal+.15f);
        if(move==DefenseMove.Evanescence)
        {
            var away=Position-point;
            if(away.LengthSquared()<4)away=Velocity.LengthSquared()>4?Velocity:new Vector2(1,0);
            away=Vector2.Normalize(away);
            dodgeDirection=new Vector2(-away.Y,away.X)*(random.Next(2)==0?-1:1);
            DefenseDestination=DefenseOrigin+dodgeDirection*DisplaySize*1.15f;
            Velocity=Vector2.Zero;Behavior="瞬机 · 翻滚闪避";
        }
        else {Velocity=Vector2.Zero;Behavior="铜头铁臂 · 转震";}
    }
    void Die(DeathCause cause){CauseOfDeath=cause;RemainsVisible=true;DeathRemaining=Settings.RespawnMinSeconds+(float)random.NextDouble()*(Settings.RespawnMaxSeconds-Settings.RespawnMinSeconds);Behavior=cause==DeathCause.Swatted?"被拍死":"饥饿死亡";Grounded=true;CockroachFlying=false;SpeechRemaining=0;Velocity=Vector2.Zero;}
    void RollSkin(){Albino=random.NextDouble()<(Settings.Skin==PetSkin.Cockroach?Settings.GiantCockroachChance:Settings.AlbinoChance);AlbinoSpawnedPending=Albino;RemainsVisible=true;}
    public void ChangeSkin(PetSkin skin)
    {
        if(Settings.Skin==skin)return;
        float healthRatio=Health/MaxHealth;
        Settings.Skin=skin;RollSkin();Health=Dead?0:Math.Max(1,healthRatio*MaxHealth);
        CockroachFlying=false;SpeechRemaining=0;speechDelay=skin==PetSkin.Cockroach?2.5f:6;Defense=DefenseMove.None;DefenseRemaining=0;InvulnerabilityRemaining=0;
    }
    public bool ConsumeAlbinoAnnouncement(){if(!AlbinoSpawnedPending)return false;AlbinoSpawnedPending=false;return true;}
    public void CleanRemains(){if(Dead){RemainsVisible=false;DeathRemaining=0;Behavior="已清理";}}
    public void Revive(Rectangle area)
    {
        RollSkin();Health=MaxHealth;Fullness=100;DeathRemaining=0;CauseOfDeath=DeathCause.None;CockroachFlying=false;SpeechRemaining=0;speechDelay=5;Alarm=InjuryArousal=RestRemaining=flightDuration=burstRemaining=burstCooldown=edgeShock=rewardPulse=punishmentPulse=avoidanceRewardPulse=avoidanceEpisodePeak=avoidanceSuccessRemaining=PredictiveEdgeRisk=LearnedEdgeRisk=feedingElapsed=escapeIntegral=escapeSafeTime=rapidTurnRemaining=rapidTurnCooldown=EscapeUrgency=EscapeDistanceRate=DefenseRemaining=InvulnerabilityRemaining=0;Defense=DefenseMove.None;previousMouseDistance=float.NaN;escapeActive=avoidanceEpisode=false;rapidTurnSign=0;SuccessfulEdgeAvoidances=0;feedingSugar=null;Brain.Reset();Brain.ClearMemory();Recenter(area);
        Position+=new Vector2((float)(random.NextDouble()-.5)*area.Width*.4f,(float)(random.NextDouble()-.5)*area.Height*.4f);Behavior="复活";Grounded=false;
    }
    public void Update(float dt,Rectangle area,Vector2 mouse,bool cursorThreat)
    {
        if(Settings.Invincible)Fullness=100;
        Time+=dt;InvulnerabilityRemaining=Math.Max(0,InvulnerabilityRemaining-dt);HitFlash=Math.Max(0,HitFlash-dt);hitCooldown=Math.Max(0,hitCooldown-dt);burstRemaining=Math.Max(0,burstRemaining-dt);burstCooldown-=dt;rapidTurnRemaining=Math.Max(0,rapidTurnRemaining-dt);rapidTurnCooldown=Math.Max(0,rapidTurnCooldown-dt);
        edgeShock=Math.Max(0,edgeShock-dt*2.5f);rewardPulse=Math.Max(0,rewardPulse-dt*.7f);punishmentPulse=Math.Max(0,punishmentPulse-dt*1.2f);avoidanceRewardPulse=Math.Max(0,avoidanceRewardPulse-dt*2.2f);avoidanceSuccessRemaining=Math.Max(0,avoidanceSuccessRemaining-dt);
        if(Dead){if(!RemainsVisible)return;DeathRemaining-=dt;if(DeathRemaining<=0)Revive(area);return;}
        if(VoiceAttentionRemaining>0)
        {
            VoiceAttentionRemaining=Math.Max(0,VoiceAttentionRemaining-dt);float attentionDelta=Wrap(voiceAttentionHeading-Heading);Heading=Wrap(Heading+Math.Clamp(attentionDelta,-1.35f*dt,1.35f*dt));Velocity=Vector2.Zero;Grounded=true;CockroachFlying=false;Behavior="正在聆听";return;
        }
        if(DefenseRemaining>0)
        {
            DefenseRemaining=Math.Max(0,DefenseRemaining-dt);
            if(Defense==DefenseMove.Evanescence)
            {
                float progress=DefenseProgress;
                float displacement=DisplaySize*1.15f*(progress*progress*(3-2*progress));
                Position=DefenseOrigin+dodgeDirection*displacement;
                ClampPosition(area);Velocity=Vector2.Zero;
                Behavior="瞬机 · 翻滚闪避";
            }
            else {Position=DefenseOrigin;Velocity=Vector2.Zero;Behavior="铜头铁臂 · 转震";}
            if(DefenseRemaining==0)Defense=DefenseMove.None;
            return;
        }
        if(Settings.Skin==PetSkin.Cockroach)
        {
            SpeechRemaining=Math.Max(0,SpeechRemaining-dt);
            speechDelay-=dt;
            if(speechDelay<=0){SpeechRemaining=3.2f;speechDelay=45+(float)random.NextDouble()*35;}
        }
        else SpeechRemaining=0;
        Alarm=Math.Max(0,Alarm-dt/Settings.AlarmSeconds);
        Fullness=Math.Max(0,Fullness-Settings.HungerPerMinute/60*dt-(Grounded?0:Settings.FlightFullnessCostPerSecond)*Brain.Flight*dt);
        if(Settings.Invincible)Fullness=100;
        if(Fullness>=Settings.SatiatedThreshold&&Health<MaxHealth)Health=Math.Min(MaxHealth,Health+Settings.SatiatedRegenPerSecond*dt);
        if(Fullness<=.01f&&!Settings.Invincible){Health=Math.Max(0,Health-Settings.StarvationDamagePerSecond*dt);if(Dead){Die(DeathCause.Starved);return;}}
        foreach(var sugar in Sugars)sugar.Age+=dt;
        Sugars.RemoveAll(s=>s.Consumed||s.Amount<=0||s.Age>600);
        Sugar? food=null;float foodDist=float.MaxValue;
        Vector2 odorVector=Vector2.Zero;float odorStrength=0;
        foreach(var s in Sugars)
        {
            var offset=s.Position-Position;float d=Math.Max(1,offset.Length());
            if(d<foodDist){foodDist=d;food=s;}
            if(d<Settings.SugarAttractionRadius){float signal=1-d/Settings.SugarAttractionRadius;signal*=signal;odorVector+=offset/d*signal;odorStrength+=signal;}
        }
        odorStrength=Math.Clamp(odorStrength,0,1);if(odorVector.LengthSquared()>1)odorVector=Vector2.Normalize(odorVector);
        float mouseDist=Vector2.Distance(mouse,Position);
        float rawThreat=cursorThreat?Math.Clamp(1-mouseDist/Settings.FearRadius,0,1):0;
        float safeRadius=Settings.FearRadius*Settings.EscapeSafeRadiusMultiplier;
        EscapeDistanceRate=float.IsFinite(previousMouseDistance)?(mouseDist-previousMouseDistance)/Math.Max(dt,.0001f):0;previousMouseDistance=mouseDist;
        if(!cursorThreat){escapeActive=false;escapeIntegral=escapeSafeTime=EscapeUrgency=0;previousMouseDistance=float.NaN;}
        else
        {
            if(rawThreat>.015f)escapeActive=true;
            if(escapeActive)
            {
                float error=Math.Clamp((safeRadius-mouseDist)/Math.Max(1,safeRadius),0,1);
                float targetRate=Settings.FlightSpeed*.42f;
                float rateError=Math.Clamp((targetRate-EscapeDistanceRate)/Math.Max(1,targetRate),0,1);
                escapeIntegral=Math.Clamp(escapeIntegral+error*dt*(.65f+.75f*rateError)-Math.Max(0,EscapeDistanceRate-targetRate)*dt/Math.Max(1,targetRate)*.18f,0,1.5f);
                EscapeUrgency=Math.Clamp(error*.55f+escapeIntegral*.52f+rateError*.30f,0,1);
                if(mouseDist>=safeRadius){escapeSafeTime+=dt;if(escapeSafeTime>=.35f){escapeActive=false;escapeIntegral=EscapeUrgency=0;}}
                else escapeSafeTime=0;
            }
            else {escapeIntegral=Math.Max(0,escapeIntegral-dt*2);EscapeUrgency=Math.Max(0,EscapeUrgency-dt*3);}
        }
        float threat=Math.Max(rawThreat,EscapeUrgency*.92f);
        bool contact=food!=null&&foodDist<65&&Fullness<100&&threat<.08f;
        wanderRemaining-=dt;
        if(wanderRemaining<=0||Vector2.Distance(Position,wander)<32)
        {
            float margin=Math.Min(100,Math.Min(area.Width,area.Height)*.25f);
            wander=new(area.Left+margin+(float)random.NextDouble()*Math.Max(1,area.Width-2*margin),area.Top+margin+(float)random.NextDouble()*Math.Max(1,area.Height-2*margin));
            wanderRemaining=2+(float)random.NextDouble()*3;
        }
        float px=Math.Clamp((Position.X-area.Left)/Math.Max(1,area.Width),0,1),py=Math.Clamp((Position.Y-area.Top)/Math.Max(1,area.Height),0,1);
        float memoryRadius=Settings.CollisionMemoryDiameter*.5f;
        float memoryRadiusX=memoryRadius/Math.Max(1,area.Width),memoryRadiusY=memoryRadius/Math.Max(1,area.Height);
        Brain.SenseLocation(px,py,memoryRadiusX,memoryRadiusY);
        bool recalling=Fullness<90&&odorStrength<.04f&&Brain.MemoryConfidence>.035f;
        Vector2 remembered=new(area.Left+Brain.RememberedX*area.Width,area.Top+Brain.RememberedY*area.Height);
        Vector2 target=recalling?remembered:wander;
        Vector2 desired=target-Position;
        bool avoidingMemory=Brain.LocalAvoidanceConfidence>.02f;
        if(avoidingMemory)
        {
            float avoidanceRange=Math.Min(area.Width,area.Height)*.38f;
            // Negative place memory is predictive: one impact already supplies a clear
            // inward steering bias on the next approach, and repeats reinforce it.
            desired+=new Vector2(Brain.AvoidanceVectorX,Brain.AvoidanceVectorY)*avoidanceRange*(.8f+Brain.LocalAvoidanceConfidence*6.4f);
        }
        Vector2 away=Position-mouse;if(away.LengthSquared()<1)away=new(1,-1);
        float wall=0,wallTurn=0;
        Vector2 visualInward=Vector2.Zero;
        if(Settings.EdgeSensing)
        {
            float margin=Math.Min(170,Math.Min(area.Width,area.Height)*.30f);
            float left=Math.Clamp((margin-(Position.X-area.Left))/margin,0,1),right=Math.Clamp((margin-(area.Right-Position.X))/margin,0,1);
            float top=Math.Clamp((margin-(Position.Y-area.Top))/margin,0,1),bottom=Math.Clamp((margin-(area.Bottom-Position.Y))/margin,0,1);
            wall=Math.Max(Math.Max(left,right),Math.Max(top,bottom));
            visualInward=new Vector2(left-right,top-bottom);if(visualInward.LengthSquared()>.0001f)wallTurn=Wrap(MathF.Atan2(visualInward.Y,visualInward.X)+MathF.PI/2-Heading);
        }
        var prediction=PredictBoundary(area);
        PredictiveEdgeRisk=prediction.Risk;
        var memoryInward=new Vector2(Brain.AvoidanceVectorX,Brain.AvoidanceVectorY);
        float agreement=memoryInward.LengthSquared()>.001f&&prediction.Inward.LengthSquared()>.001f?Math.Max(0,Vector2.Dot(Vector2.Normalize(memoryInward),prediction.Inward)):0;
        LearnedEdgeRisk=Math.Clamp(prediction.Risk*Brain.LocalAvoidanceConfidence*(.6f+.4f*agreement),0,1);
        if(LearnedEdgeRisk>.01f)
        {
            var learnedInward=prediction.Inward*(.8f+Brain.AvoidanceSkill*.8f)+memoryInward*.35f;
            if(learnedInward.LengthSquared()>.001f)learnedInward=Vector2.Normalize(learnedInward);
            float learnedTurn=Wrap(MathF.Atan2(learnedInward.Y,learnedInward.X)+MathF.PI/2-Heading);
            float learnedWall=Math.Clamp(LearnedEdgeRisk*(.9f+Brain.AvoidanceSkill*.8f),0,1);
            if(learnedWall>=wall*.75f)wallTurn=learnedTurn;
            wall=Math.Max(wall,learnedWall);
        }
        if(LearnedEdgeRisk>.12f){avoidanceEpisode=true;avoidanceEpisodePeak=Math.Max(avoidanceEpisodePeak,LearnedEdgeRisk);}
        else if(avoidanceEpisode&&LearnedEdgeRisk<.035f)
        {
            if(avoidanceEpisodePeak>.18f){Brain.ReinforceSuccessfulAvoidance(avoidanceEpisodePeak);avoidanceRewardPulse=1;avoidanceSuccessRemaining=1.35f;SuccessfulEdgeAvoidances++;}
            avoidanceEpisode=false;avoidanceEpisodePeak=0;
        }
        float targetHeading=MathF.Atan2(desired.Y,desired.X)+MathF.PI/2;
        float delta=Wrap(targetHeading-Heading);
        float threatHeading=MathF.Atan2(away.Y,away.X)+MathF.PI/2;
        float threatTurn=Wrap(threatHeading-Heading);
        bool escapeStalled=escapeActive&&escapeIntegral>.34f&&EscapeDistanceRate<Settings.FlightSpeed*.12f;
        if(escapeStalled&&rapidTurnCooldown<=0)
        {
            rapidTurnSign=Math.Abs(threatTurn)>.18f?Math.Sign(threatTurn):(random.Next(2)==0?-1:1);
            rapidTurnRemaining=.32f;rapidTurnCooldown=.85f;
        }
        float escapeTurn=rapidTurnRemaining>0?Wrap(threatTurn+rapidTurnSign*.72f):threatTurn;
        float odorTurn=0;if(odorVector.LengthSquared()>.0001f)odorTurn=Wrap(MathF.Atan2(odorVector.Y,odorVector.X)+MathF.PI/2-Heading);
        if(Settings.RestEnabled&&Grounded&&RestRemaining>0)RestRemaining-=dt;
        if(Settings.RestEnabled&&!Grounded&&flightDuration>10&&Fullness>27&&odorStrength<.02f&&threat<.02f&&Alarm<.01f)
        {RestRemaining=1+(float)random.NextDouble()*2.5f;flightDuration=0;}
        bool resting=Settings.RestEnabled&&RestRemaining>0&&threat<.02f&&Alarm<.01f;
        float travel=contact||resting?0:.95f;
        Brain.SetInput(Math.Max(threat,Alarm*.95f+InjuryArousal*.13f),contact?1:0,Fullness,Math.Clamp(threat>.02f?escapeTurn:delta,-1,1),travel,wall,Settings,
            odorStrength,Math.Clamp(odorTurn,-1,1),Math.Clamp(wallTurn,-1,1),px,py,rewardPulse,punishmentPulse+edgeShock,dt,memoryRadiusX,memoryRadiusY,false,avoidanceRewardPulse);
        neuralRemainder+=dt;
        while(neuralRemainder>=.001f){Brain.Step(Settings.NeuralGain,false);neuralRemainder-=.001f;}
        Brain.SampleOutputs(Settings.NeuralGain);
        if(burstCooldown<=0&&burstRemaining<=0&&!resting&&!contact&&Brain.Flight>.25f&&random.NextDouble()<dt*.65){burstRemaining=.20f+(float)random.NextDouble()*.22f;burstCooldown=1.5f+(float)random.NextDouble()*2.5f;}
        float steeringIntent=threat>.02f?escapeTurn:delta;
        float learnedTurnGain=3.6f+LearnedEdgeRisk*(5.5f+Brain.AvoidanceSkill*5.5f);
        float turnRate=Settings.NeuralSteering
            ? Brain.OptomotorTurn*5.8f+Brain.Turn*2.2f+Brain.OdorTurn*5.6f+Brain.NavigationTurn*learnedTurnGain+Brain.DescendingTurn*2.2f+Math.Clamp(steeringIntent,-1,1)*.35f
            : Math.Clamp(threat>.02f?escapeTurn:odorStrength>.02f?odorTurn:delta,-1,1)*6.5f;
        float avoidanceActivity=Math.Clamp(Brain.NavigationRate/4+Brain.VisualMotionRate/8,0,1);
        float predictiveNeuralGate=Math.Clamp(LearnedEdgeRisk*avoidanceActivity,0,1);
        if(Settings.NeuralSteering&&predictiveNeuralGate>.01f)
        {
            float learnedNeuralTurn=Brain.NavigationTurn*(8+Brain.AvoidanceSkill*8);
            turnRate=turnRate*(1-predictiveNeuralGate*.82f)+learnedNeuralTurn*(predictiveNeuralGate*.82f);
        }
        Heading=Wrap(Heading+turnRate*dt);
        float neuralSpeed=Math.Clamp(Brain.Flight*(1.25f+Brain.WingRate*.006f),0,1.8f);
        float speed=Settings.FlightSpeed*neuralSpeed*(Albino?Settings.AlbinoSpeedMultiplier:1)*(1+Brain.Fear*1.8f+EscapeUrgency*Settings.EscapeAccelerationGain+InjuryArousal*.22f);
        float learnedBrake=Math.Clamp(LearnedEdgeRisk*avoidanceActivity*(.65f+Brain.AvoidanceSkill*.35f),0,.90f);
        speed*=1-learnedBrake;
        if(burstRemaining>0)speed*=2.2f;
        if(contact||resting)speed=0;
        Grounded=speed<3&&Velocity.Length()<4;
        if(Grounded)flightDuration=0;else flightDuration+=dt;
        var direction=new Vector2(MathF.Sin(Heading),-MathF.Cos(Heading));
        Velocity*=MathF.Exp(-LearnedEdgeRisk*avoidanceActivity*(2+Brain.AvoidanceSkill*6)*dt);
        Velocity=Vector2.Lerp(Velocity,direction*speed,1-MathF.Exp(-8*dt));Position+=Velocity*dt;
        if(Settings.Skin==PetSkin.Cockroach)
        {
            float relativeSpeed=Velocity.Length()/Math.Max(1,Settings.FlightSpeed*(Albino?Settings.AlbinoSpeedMultiplier:1));
            CockroachFlying=contact||resting?false:CockroachFlying?relativeSpeed>.85f:relativeSpeed>1.25f;
        }
        else CockroachFlying=false;
        bool collided=ClampPosition(area);
        if(collided)
        {
            edgeShock=1;punishmentPulse=1;Alarm=Math.Max(Alarm,.45f);RestRemaining=0;EdgeCollisions++;
            float collisionX=Math.Clamp((Position.X-area.Left)/Math.Max(1,area.Width),0,1),collisionY=Math.Clamp((Position.Y-area.Top)/Math.Max(1,area.Height),0,1);
            Brain.LearnCollision(collisionX,collisionY,memoryRadiusX,memoryRadiusY,Math.Max(.36f,Settings.LearningRate*Settings.EdgePunishment*2.4f));
            avoidanceEpisode=false;avoidanceEpisodePeak=0;avoidanceSuccessRemaining=0;
        }
        bool ate=false,feeding=false;
        if(contact&&food!=null)
        {
            if(!ReferenceEquals(feedingSugar,food)){feedingSugar=food;feedingElapsed=0;}
            if(Brain.Feeding>.02f){feedingElapsed+=dt;feeding=true;}
        }
        else {feedingSugar=null;feedingElapsed=0;}
        if(feeding&&feedingElapsed>=Settings.SugarEatingSeconds&&feedingSugar!.TryConsume())
        {
            float amount=Math.Min(100-Fullness,Settings.SugarNutrition);Fullness+=amount;Health=Math.Min(MaxHealth,Health+amount*.3f);rewardPulse=1;ate=true;Sugars.Remove(feedingSugar);feedingSugar=null;feedingElapsed=0;
        }
        Behavior=collided?"撞到边缘，强化危险记忆":avoidanceSuccessRemaining>0?"成功避开记忆边缘":LearnedEdgeRisk>.10f?"回忆危险，提前转向":rapidTurnRemaining>0?"急转逃逸":EscapeUrgency>.35f?"持续加速逃逸":threat>.02f||Brain.Fear>.2f?"逃离鼠标":ate?"吃掉糖粒并记住":feeding?"进食中":Grounded?"停歇":odorStrength>.02f&&Fullness<95?"循着糖味":recalling?"回忆常见糖点":avoidingMemory?"避开负面位置":wall>.25f?"视觉避开边缘":"自由飞行";
    }
    BoundaryPrediction PredictBoundary(Rectangle area)
    {
        float bodyMargin=Math.Min(DisplaySize*.32f,Math.Min(area.Width,area.Height)*.2f);
        var headingDirection=new Vector2(MathF.Sin(Heading),-MathF.Cos(Heading));
        float intentSpeed=Math.Max(Velocity.Length(),Settings.FlightSpeed*(.30f+Brain.Flight*.18f));
        var projectedVelocity=Velocity.LengthSquared()>4?Vector2.Lerp(headingDirection*intentSpeed,Velocity,.62f):headingDirection*intentSpeed;
        float horizon=1.20f+Brain.AvoidanceSkill*.80f;
        Vector2 inward=Vector2.Zero;float risk=0;
        void Consider(float distance,float closing,Vector2 normal)
        {
            if(closing<=1)return;
            float time=Math.Max(0,distance)/closing;
            float candidate=Math.Clamp(1-time/horizon,0,1);
            if(candidate<=0)return;
            inward+=normal*candidate*candidate;risk=Math.Max(risk,candidate);
        }
        Consider(Position.X-(area.Left+bodyMargin),-projectedVelocity.X,Vector2.UnitX);
        Consider((area.Right-bodyMargin)-Position.X,projectedVelocity.X,-Vector2.UnitX);
        Consider(Position.Y-(area.Top+bodyMargin),-projectedVelocity.Y,Vector2.UnitY);
        Consider((area.Bottom-bodyMargin)-Position.Y,projectedVelocity.Y,-Vector2.UnitY);
        if(inward.LengthSquared()>.001f)inward=Vector2.Normalize(inward);
        return new(risk,inward);
    }
    readonly record struct BoundaryPrediction(float Risk,Vector2 Inward);
    bool ClampPosition(Rectangle area)
    {
        float margin=Math.Min(DisplaySize*.32f,Math.Min(area.Width,area.Height)*.2f);
        var old=Position;Position=new(Math.Clamp(Position.X,area.Left+margin,area.Right-margin),Math.Clamp(Position.Y,area.Top+margin,area.Bottom-margin));
        if(old==Position)return false;
        var normal=Position-old;if(normal.LengthSquared()>.001f){normal=Vector2.Normalize(normal);Velocity=Vector2.Reflect(Velocity,normal)*.42f;Heading=MathF.Atan2(normal.Y,normal.X)+MathF.PI/2;}else Velocity*=.25f;
        wanderRemaining=0;return true;
    }
    static float Wrap(float a){while(a>MathF.PI)a-=MathF.Tau;while(a< -MathF.PI)a+=MathF.Tau;return a;}
}
