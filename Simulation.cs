using System.Numerics;

namespace FlyPet;

public sealed class Sugar(Vector2 position)
{
    public Vector2 Position=position;
    public float Amount=100,Age;
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
    public string Behavior="苏醒";
    public readonly List<Sugar> Sugars=[];
    readonly Random random;
    Vector2 wander;
    float wanderRemaining,neuralRemainder,hitCooldown,flightDuration,burstRemaining,burstCooldown;
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
        RollSkin();Health=MaxHealth;Fullness=65;DeathRemaining=0;Alarm=InjuryArousal=RestRemaining=flightDuration=burstRemaining=burstCooldown=0;Brain.Reset();Recenter(area);
        Position+=new Vector2((float)(random.NextDouble()-.5)*area.Width*.4f,(float)(random.NextDouble()-.5)*area.Height*.4f);Behavior="复活";Grounded=false;
    }
    public void Update(float dt,Rectangle area,Vector2 mouse,bool cursorThreat)
    {
        Time+=dt;HitFlash=Math.Max(0,HitFlash-dt);hitCooldown=Math.Max(0,hitCooldown-dt);burstRemaining=Math.Max(0,burstRemaining-dt);burstCooldown-=dt;
        if(Dead){if(!RemainsVisible)return;DeathRemaining-=dt;if(DeathRemaining<=0)Revive(area);return;}
        Alarm=Math.Max(0,Alarm-dt/Settings.AlarmSeconds);
        Fullness=Math.Max(0,Fullness-Settings.HungerPerMinute/60*dt-(Grounded?0:Settings.FlightFullnessCostPerSecond)*Brain.Flight*dt);
        if(Fullness<=.01f&&!Settings.Invincible){Health=Math.Max(0,Health-Settings.StarvationDamagePerSecond*dt);if(Dead){Die("饥饿死亡");return;}}
        foreach(var sugar in Sugars)sugar.Age+=dt;
        Sugars.RemoveAll(s=>s.Amount<=0||s.Age>600);
        Sugar? food=null;float foodDist=float.MaxValue;
        foreach(var s in Sugars){float d=Vector2.Distance(Position,s.Position);if(d<foodDist&&d<Settings.SugarAttractionRadius){foodDist=d;food=s;}}
        float mouseDist=Vector2.Distance(mouse,Position);
        float threat=cursorThreat?Math.Clamp(1-mouseDist/Settings.FearRadius,0,1):0;
        bool eating=food!=null&&foodDist<65&&Fullness<100&&threat<.08f;
        wanderRemaining-=dt;
        if(wanderRemaining<=0||Vector2.Distance(Position,wander)<32)
        {
            float margin=Math.Min(100,Math.Min(area.Width,area.Height)*.25f);
            wander=new(area.Left+margin+(float)random.NextDouble()*Math.Max(1,area.Width-2*margin),area.Top+margin+(float)random.NextDouble()*Math.Max(1,area.Height-2*margin));
            wanderRemaining=2+(float)random.NextDouble()*3;
        }
        Vector2 target=(food!=null&&Fullness<100)?food.Position:wander;
        Vector2 desired=target-Position;
        if(threat>.02f){desired=Position-mouse;if(desired.LengthSquared()<1)desired=new(1,-1);}
        float wall=0;
        if(Settings.EdgeSensing)
        {
            float margin=Math.Min(170,Math.Min(area.Width,area.Height)*.30f);
            float left=Math.Clamp((margin-(Position.X-area.Left))/margin,0,1),right=Math.Clamp((margin-(area.Right-Position.X))/margin,0,1);
            float top=Math.Clamp((margin-(Position.Y-area.Top))/margin,0,1),bottom=Math.Clamp((margin-(area.Bottom-Position.Y))/margin,0,1);
            wall=Math.Max(Math.Max(left,right),Math.Max(top,bottom));
            desired+=new Vector2(left-right,top-bottom)*wall*1200;
        }
        float targetHeading=MathF.Atan2(desired.Y,desired.X)+MathF.PI/2;
        float delta=Wrap(targetHeading-Heading);
        if(Settings.RestEnabled&&Grounded&&RestRemaining>0)RestRemaining-=dt;
        if(Settings.RestEnabled&&!Grounded&&flightDuration>10&&Fullness>27&&food==null&&threat<.02f&&Alarm<.01f)
        {RestRemaining=1+(float)random.NextDouble()*2.5f;flightDuration=0;}
        bool resting=Settings.RestEnabled&&RestRemaining>0&&threat<.02f&&Alarm<.01f;
        float travel=eating||resting?0:.95f;
        Brain.SetInput(Math.Max(threat,Alarm*.95f+InjuryArousal*.13f),eating?1:0,Fullness,Math.Clamp(delta,-1,1),travel,wall,Settings);
        neuralRemainder+=dt;
        while(neuralRemainder>=.001f){Brain.Step(Settings.NeuralGain);neuralRemainder-=.001f;}
        if(burstCooldown<=0&&burstRemaining<=0&&!resting&&!eating&&Brain.Flight>.25f&&random.NextDouble()<dt*.65){burstRemaining=.20f+(float)random.NextDouble()*.22f;burstCooldown=1.5f+(float)random.NextDouble()*2.5f;}
        float turnRate=Settings.NeuralSteering?Brain.OptomotorTurn*6.5f+Brain.Turn*1.2f:Math.Clamp(delta,-1,1)*6.5f;
        if(food!=null&&threat<.02f)turnRate=Math.Clamp(delta,-1,1)*9f+Brain.OptomotorTurn*1.5f;
        if(wall>.08f)turnRate+=Math.Clamp(delta,-1,1)*7.5f;
        Heading=Wrap(Heading+turnRate*dt);
        float neuralSpeed=Math.Clamp(Brain.Flight*(1.25f+Brain.WingRate*.006f),0,1.8f);
        float speed=Settings.FlightSpeed*neuralSpeed*(Albino?Settings.AlbinoSpeedMultiplier:1)*(1+Brain.Fear*1.8f+InjuryArousal*.22f);
        if(burstRemaining>0)speed*=2.2f;
        if(food!=null&&threat<.02f)speed*=Math.Clamp(foodDist/110,.65f,1);
        if(eating||resting)speed=0;
        Grounded=speed<3&&Velocity.Length()<4;
        if(Grounded)flightDuration=0;else flightDuration+=dt;
        var direction=new Vector2(MathF.Sin(Heading),-MathF.Cos(Heading));
        Velocity=Vector2.Lerp(Velocity,direction*speed,1-MathF.Exp(-8*dt));Position+=Velocity*dt;ClampPosition(area);
        if(eating)
        {
            float amount=Math.Min(food!.Amount,Math.Min(100-Fullness,Settings.FeedPerSecond*Brain.Feeding*dt));
            Fullness+=amount;food.Amount-=amount;Health=Math.Min(100,Health+amount*.3f);
        }
        Behavior=threat>.02f||Brain.Fear>.2f?"逃离鼠标":eating?"吃糖":Grounded?"停歇":food!=null&&Fullness<90?"寻找糖粒":wall>.25f?"避开屏幕边缘":"自由飞行";
    }
    void ClampPosition(Rectangle area)
    {
        float margin=Math.Min(Settings.PetSize*.32f,Math.Min(area.Width,area.Height)*.2f);
        var old=Position;Position=new(Math.Clamp(Position.X,area.Left+margin,area.Right-margin),Math.Clamp(Position.Y,area.Top+margin,area.Bottom-margin));
        if(old!=Position){Velocity*=.25f;wanderRemaining=0;}
    }
    static float Wrap(float a){while(a>MathF.PI)a-=MathF.Tau;while(a< -MathF.PI)a+=MathF.Tau;return a;}
}
