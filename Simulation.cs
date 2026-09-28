using System.Numerics;

namespace FlyPet;

public sealed class Sugar(Vector2 position)
{
    public Vector2 Position=position;
    public float Amount=100,Age;
    public bool Consumed {get;private set;}
    public bool TryConsume(){if(Consumed)return false;Consumed=true;Amount=0;return true;}
}

public sealed class Simulation
{
    public readonly Brain Brain;
    public Settings Settings;
    public Vector2 Position,Velocity;
    public float Heading,Health=100,Fullness=65,DeathRemaining,HitFlash,Time,Alarm,InjuryArousal,RestRemaining;
    public bool Dead=>Health<=0;
    public bool Albino { get; private set; }
    public bool AlbinoSpawnedPending { get; private set; }
    public bool RemainsVisible { get; private set; } = true;
    public float MaxHealth => Albino ? 180 : 100;
    public bool Grounded {get;private set;}
    public float EscapeUrgency {get;private set;}
    public float EscapeDistanceRate {get;private set;}
    public bool RapidEscapeTurn=>rapidTurnRemaining>0;
    public string Behavior="苏醒";
    public readonly List<Sugar> Sugars=[];
    readonly Random random;
    Vector2 wander;
    Sugar? feedingSugar;
    float feedingElapsed;
    float wanderRemaining,neuralRemainder,hitCooldown,flightDuration,burstRemaining,burstCooldown,edgeShock,rewardPulse,punishmentPulse;
    float escapeIntegral,previousMouseDistance=float.NaN,escapeSafeTime,rapidTurnRemaining,rapidTurnCooldown;
    int rapidTurnSign;
    bool escapeActive;
    public int EdgeCollisions {get;private set;}
    public Simulation(Settings settings,CircuitData data,int seed=0){Settings=settings;Brain=new(data);random=seed==0?new Random():new Random(seed);RollSkin();Health=MaxHealth;}
    public void Recenter(Rectangle area){Position=new(area.Left+area.Width*.55f,area.Top+area.Height*.45f);wander=Position;Velocity=Vector2.Zero;}
    public void AddSugar(Vector2 p){if(Sugars.Count>=Settings.MaxSugar)Sugars.RemoveAt(0);Sugars.Add(new(p));}
    public bool Hit(Vector2 point)
    {
        if(Dead||hitCooldown>0||Vector2.Distance(point,Position)>Settings.PetSize*.29f+20)return false;
        HitFlash=.28f;hitCooldown=.22f;Alarm=1;InjuryArousal=Math.Min(1,InjuryArousal+.30f);RestRemaining=0;
        if(!Settings.Invincible)Health=Math.Max(0,Health-Settings.SwatDamage*(Albino ? .58f : 1));
        if(Dead)Die("被拍死");
        return true;
    }
    void Die(string reason){RemainsVisible=true;DeathRemaining=Settings.RespawnMinSeconds+(float)random.NextDouble()*(Settings.RespawnMaxSeconds-Settings.RespawnMinSeconds);Behavior=reason;Grounded=true;Velocity=Vector2.Zero;}
    void RollSkin(){Albino=random.NextDouble()<Settings.AlbinoChance;AlbinoSpawnedPending=Albino;RemainsVisible=true;}
    public bool ConsumeAlbinoAnnouncement(){if(!AlbinoSpawnedPending)return false;AlbinoSpawnedPending=false;return true;}
    public void CleanRemains(){if(Dead){RemainsVisible=false;DeathRemaining=0;Behavior="已清理";}}
    public void Revive(Rectangle area)
    {
        RollSkin();Health=MaxHealth;Fullness=65;DeathRemaining=0;Alarm=InjuryArousal=RestRemaining=flightDuration=burstRemaining=burstCooldown=edgeShock=rewardPulse=punishmentPulse=feedingElapsed=escapeIntegral=escapeSafeTime=rapidTurnRemaining=rapidTurnCooldown=EscapeUrgency=EscapeDistanceRate=0;previousMouseDistance=float.NaN;escapeActive=false;rapidTurnSign=0;feedingSugar=null;Brain.Reset();Brain.ClearMemory();Recenter(area);
        Position+=new Vector2((float)(random.NextDouble()-.5)*area.Width*.4f,(float)(random.NextDouble()-.5)*area.Height*.4f);Behavior="复活";Grounded=false;
    }
    public void Update(float dt,Rectangle area,Vector2 mouse,bool cursorThreat)
    {
        Time+=dt;HitFlash=Math.Max(0,HitFlash-dt);hitCooldown=Math.Max(0,hitCooldown-dt);burstRemaining=Math.Max(0,burstRemaining-dt);burstCooldown-=dt;rapidTurnRemaining=Math.Max(0,rapidTurnRemaining-dt);rapidTurnCooldown=Math.Max(0,rapidTurnCooldown-dt);
        edgeShock=Math.Max(0,edgeShock-dt*2.5f);rewardPulse=Math.Max(0,rewardPulse-dt*.7f);punishmentPulse=Math.Max(0,punishmentPulse-dt*1.2f);
        if(Dead){if(!RemainsVisible)return;DeathRemaining-=dt;if(DeathRemaining<=0)Revive(area);return;}
        Alarm=Math.Max(0,Alarm-dt/Settings.AlarmSeconds);
        Fullness=Math.Max(0,Fullness-Settings.HungerPerMinute/60*dt-(Grounded?0:Settings.FlightFullnessCostPerSecond)*Brain.Flight*dt);
        if(Fullness>=Settings.SatiatedThreshold&&Health<MaxHealth)Health=Math.Min(MaxHealth,Health+Settings.SatiatedRegenPerSecond*dt);
        if(Fullness<=.01f&&!Settings.Invincible){Health=Math.Max(0,Health-Settings.StarvationDamagePerSecond*dt);if(Dead){Die("饥饿死亡");return;}}
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
        bool recalling=Fullness<90&&odorStrength<.04f&&Brain.MemoryConfidence>.035f;
        Vector2 remembered=new(area.Left+Brain.RememberedX*area.Width,area.Top+Brain.RememberedY*area.Height);
        Vector2 target=recalling?remembered:wander;
        Vector2 desired=target-Position;
        bool avoidingMemory=Brain.LocalAvoidanceConfidence>.02f;
        if(avoidingMemory)
        {
            float avoidanceRange=Math.Min(area.Width,area.Height)*.38f;
            desired+=new Vector2(Brain.AvoidanceVectorX,Brain.AvoidanceVectorY)*avoidanceRange*Brain.LocalAvoidanceConfidence*2.8f;
        }
        Vector2 away=Position-mouse;if(away.LengthSquared()<1)away=new(1,-1);
        float wall=0,wallTurn=0;
        if(Settings.EdgeSensing)
        {
            float margin=Math.Min(170,Math.Min(area.Width,area.Height)*.30f);
            float left=Math.Clamp((margin-(Position.X-area.Left))/margin,0,1),right=Math.Clamp((margin-(area.Right-Position.X))/margin,0,1);
            float top=Math.Clamp((margin-(Position.Y-area.Top))/margin,0,1),bottom=Math.Clamp((margin-(area.Bottom-Position.Y))/margin,0,1);
            wall=Math.Max(Math.Max(left,right),Math.Max(top,bottom));
            var inward=new Vector2(left-right,top-bottom);if(inward.LengthSquared()>.0001f)wallTurn=Wrap(MathF.Atan2(inward.Y,inward.X)+MathF.PI/2-Heading);
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
        float px=Math.Clamp((Position.X-area.Left)/Math.Max(1,area.Width),0,1),py=Math.Clamp((Position.Y-area.Top)/Math.Max(1,area.Height),0,1);
        float memoryRadius=Settings.CollisionMemoryDiameter*.5f;
        float memoryRadiusX=memoryRadius/Math.Max(1,area.Width),memoryRadiusY=memoryRadius/Math.Max(1,area.Height);
        Brain.SetInput(Math.Max(threat,Alarm*.95f+InjuryArousal*.13f),contact?1:0,Fullness,Math.Clamp(threat>.02f?escapeTurn:delta,-1,1),travel,wall,Settings,
            odorStrength,Math.Clamp(odorTurn,-1,1),Math.Clamp(wallTurn,-1,1),px,py,rewardPulse,punishmentPulse+edgeShock,dt,memoryRadiusX,memoryRadiusY,false);
        neuralRemainder+=dt;
        while(neuralRemainder>=.001f){Brain.Step(Settings.NeuralGain);neuralRemainder-=.001f;}
        if(burstCooldown<=0&&burstRemaining<=0&&!resting&&!contact&&Brain.Flight>.25f&&random.NextDouble()<dt*.65){burstRemaining=.20f+(float)random.NextDouble()*.22f;burstCooldown=1.5f+(float)random.NextDouble()*2.5f;}
        float steeringIntent=threat>.02f?escapeTurn:delta;
        float turnRate=Settings.NeuralSteering
            ? Brain.OptomotorTurn*5.8f+Brain.Turn*2.2f+Brain.OdorTurn*5.6f+Brain.NavigationTurn*3.6f+Brain.DescendingTurn*2.2f+Math.Clamp(steeringIntent,-1,1)*.35f
            : Math.Clamp(threat>.02f?escapeTurn:odorStrength>.02f?odorTurn:delta,-1,1)*6.5f;
        Heading=Wrap(Heading+turnRate*dt);
        float neuralSpeed=Math.Clamp(Brain.Flight*(1.25f+Brain.WingRate*.006f),0,1.8f);
        float speed=Settings.FlightSpeed*neuralSpeed*(Albino?Settings.AlbinoSpeedMultiplier:1)*(1+Brain.Fear*1.8f+EscapeUrgency*Settings.EscapeAccelerationGain+InjuryArousal*.22f);
        if(burstRemaining>0)speed*=2.2f;
        if(contact||resting)speed=0;
        Grounded=speed<3&&Velocity.Length()<4;
        if(Grounded)flightDuration=0;else flightDuration+=dt;
        var direction=new Vector2(MathF.Sin(Heading),-MathF.Cos(Heading));
        Velocity=Vector2.Lerp(Velocity,direction*speed,1-MathF.Exp(-8*dt));Position+=Velocity*dt;
        bool collided=ClampPosition(area);
        if(collided)
        {
            edgeShock=1;punishmentPulse=1;Alarm=Math.Max(Alarm,.45f);RestRemaining=0;EdgeCollisions++;
            float collisionX=Math.Clamp((Position.X-area.Left)/Math.Max(1,area.Width),0,1),collisionY=Math.Clamp((Position.Y-area.Top)/Math.Max(1,area.Height),0,1);
            Brain.LearnCollision(collisionX,collisionY,memoryRadiusX,memoryRadiusY,Math.Max(.14f,Settings.LearningRate*Settings.EdgePunishment*1.5f));
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
        Behavior=collided?"撞击边缘并记住":rapidTurnRemaining>0?"急转逃逸":EscapeUrgency>.35f?"持续加速逃逸":threat>.02f||Brain.Fear>.2f?"逃离鼠标":ate?"吃掉糖粒并记住":feeding?"进食中":Grounded?"停歇":odorStrength>.02f&&Fullness<95?"循着糖味":recalling?"回忆常见糖点":avoidingMemory?"避开负面位置":wall>.25f?"视觉避开边缘":"自由飞行";
    }
    bool ClampPosition(Rectangle area)
    {
        float margin=Math.Min(Settings.PetSize*.32f,Math.Min(area.Width,area.Height)*.2f);
        var old=Position;Position=new(Math.Clamp(Position.X,area.Left+margin,area.Right-margin),Math.Clamp(Position.Y,area.Top+margin,area.Bottom-margin));
        if(old==Position)return false;
        var normal=Position-old;if(normal.LengthSquared()>.001f){normal=Vector2.Normalize(normal);Velocity=Vector2.Reflect(Velocity,normal)*.42f;Heading=MathF.Atan2(normal.Y,normal.X)+MathF.PI/2;}else Velocity*=.25f;
        wanderRemaining=0;return true;
    }
    static float Wrap(float a){while(a>MathF.PI)a-=MathF.Tau;while(a< -MathF.PI)a+=MathF.Tau;return a;}
}
