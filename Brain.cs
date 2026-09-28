using System.Reflection;
using System.Text.Json;

namespace FlyPet;

public sealed class CircuitData
{
    public Node[] Nodes { get; set; } = [];
    public int[][] Edges { get; set; } = [];
    public sealed class Node
    {
        public string BodyId { get; set; } = "";
        public string Type { get; set; } = "";
        public string Side { get; set; } = "";
        public string Nt { get; set; } = "";
        public long[]? Soma { get; set; }
        public string[] Groups { get; set; } = [];
    }
    public static CircuitData Load()
    {
        // Optional explicitly placed JSON overrides the embedded circuit. Never executes code.
        var custom = Path.Combine(Settings.Folder,"circuit.json");
        using var stream = File.Exists(custom) ? File.OpenRead(custom) : Assembly.GetExecutingAssembly().GetManifestResourceStream("FlyPet.Assets.circuit.json")!;
        var d = JsonSerializer.Deserialize<CircuitData>(stream, Settings.JsonOptions) ?? throw new InvalidDataException("Empty circuit");
        if (d.Nodes.Length is < 1 or > 25000 || d.Edges.Length > 3000000) throw new InvalidDataException("Circuit size out of bounds");
        if (d.Edges.Any(e => e.Length != 3 || e[0] < 0 || e[1] < 0 || e[0] >= d.Nodes.Length || e[1] >= d.Nodes.Length || Math.Abs((long)e[2]) > 1000000)) throw new InvalidDataException("Invalid circuit edge");
        foreach(var g in new[]{"visual","escape","flight","steer","wing","taste","feed","visual_motion","olfactory","memory","reward","navigation","descending","motor"})
            if(!d.Nodes.Any(n=>n.Groups.Contains(g))) throw new InvalidDataException("Missing neural group: " + g);
        return d;
    }
}

public sealed class Brain
{
    readonly float[] voltage, current, rates, drive, adaptation;
    readonly float[][] delayed;
    int delaySlot;
    readonly byte[] refractory;
    readonly int[] offsets, targets, spikes;
    readonly float[] weights;
    readonly Dictionary<string,int[]> groups;
    readonly int[] steerLeft, steerRight, visualLeft, visualRight;
    readonly int[] optomotor, reverse;
    readonly int[] optoLeft,optoRight;
    readonly int[] visualMotionStim,olfactoryStim,rewardStim,placeCells;
    readonly int[] visualMotionLeft,visualMotionRight,olfactoryLeft,olfactoryRight,navigationLeft,navigationRight,descendingLeft,descendingRight;
    readonly float[] placeX,placeY,placeActivity,placeValue;
    float odorDirectionInput,wallDirectionInput;
    public int NeuronCount => voltage.Length;
    public int EdgeCount => targets.Length;
    public long TotalSpikes { get; private set; }
    public float Flight { get; private set; }
    public float Fear { get; private set; }
    public float Feeding { get; private set; }
    public float Turn { get; private set; }
    public float VisualRate { get; private set; }
    public float TasteRate { get; private set; }
    public float EscapeRate { get; private set; }
    public float FlightRate { get; private set; }
    public float WingRate { get; private set; }
    public float FeedRate { get; private set; }
    public float OptomotorRate { get; private set; }
    public float OptomotorTurn { get; private set; }
    public float ReverseRate { get; private set; }
    public float OlfactoryRate { get; private set; }
    public float VisualMotionRate { get; private set; }
    public float MemoryRate { get; private set; }
    public float RewardRate { get; private set; }
    public float NavigationRate { get; private set; }
    public float DescendingRate { get; private set; }
    public float MotorRate { get; private set; }
    public float OdorTurn { get; private set; }
    public float NavigationTurn { get; private set; }
    public float DescendingTurn { get; private set; }
    public float RememberedX { get; private set; }=.5f;
    public float RememberedY { get; private set; }=.5f;
    public float MemoryConfidence { get; private set; }
    public float AvoidedX { get; private set; }=.5f;
    public float AvoidedY { get; private set; }=.5f;
    public float AvoidanceConfidence { get; private set; }
    public float RewardSignal { get; private set; }
    public float WallDrive { get; private set; }
    public float ThreatDrive { get; private set; }
    public float FoodDrive { get; private set; }
    public float GetRate(int index)=>rates[index];
    public float GetVoltage(int index)=>voltage[index];
    public IReadOnlyList<int> Group(string name)=>groups.TryGetValue(name,out var ids)?ids:[];
    public Brain(CircuitData data)
    {
        int n=data.Nodes.Length;
        voltage=new float[n];current=new float[n];rates=new float[n];drive=new float[n];adaptation=new float[n];delayed=[new float[n],new float[n],new float[n]];refractory=new byte[n];spikes=new int[n];
        groups = new[]{"visual","escape","flight","steer","wing","taste","feed","optomotor","reverse","visual_motion","olfactory","memory","reward","navigation","descending","motor","gustatory"}.ToDictionary(g=>g,g=>Enumerable.Range(0,n).Where(i=>data.Nodes[i].Groups.Contains(g)).ToArray());
        steerLeft=groups["steer"].Where(i=>data.Nodes[i].Side=="L").ToArray();steerRight=groups["steer"].Where(i=>data.Nodes[i].Side=="R").ToArray();
        visualLeft=groups["visual"].Where(i=>data.Nodes[i].Side=="L").ToArray();visualRight=groups["visual"].Where(i=>data.Nodes[i].Side=="R").ToArray();
        optomotor=groups["optomotor"];reverse=groups["reverse"];
        optoLeft=optomotor.Where(i=>data.Nodes[i].Side=="L").ToArray();optoRight=optomotor.Where(i=>data.Nodes[i].Side=="R").ToArray();
        visualMotionStim=Sample(groups["visual_motion"],256);olfactoryStim=Sample(groups["olfactory"],256);rewardStim=Sample(groups["reward"],128);
        visualMotionLeft=visualMotionStim.Where(i=>data.Nodes[i].Side=="L").ToArray();visualMotionRight=visualMotionStim.Where(i=>data.Nodes[i].Side=="R").ToArray();
        olfactoryLeft=olfactoryStim.Where(i=>data.Nodes[i].Side=="L").ToArray();olfactoryRight=olfactoryStim.Where(i=>data.Nodes[i].Side=="R").ToArray();
        navigationLeft=groups["navigation"].Where(i=>data.Nodes[i].Side=="L").ToArray();navigationRight=groups["navigation"].Where(i=>data.Nodes[i].Side=="R").ToArray();
        descendingLeft=groups["descending"].Where(i=>data.Nodes[i].Side=="L").ToArray();descendingRight=groups["descending"].Where(i=>data.Nodes[i].Side=="R").ToArray();
        placeCells=Sample(groups["memory"],192);placeX=new float[placeCells.Length];placeY=new float[placeCells.Length];placeActivity=new float[placeCells.Length];placeValue=new float[placeCells.Length];
        int columns=16,rows=Math.Max(1,(int)Math.Ceiling(placeCells.Length/(double)columns));
        for(int i=0;i<placeCells.Length;i++){placeX[i]=((i%columns)+.5f)/columns;placeY[i]=((i/columns)+.5f)/rows;}
        offsets=new int[n+1]; targets=new int[data.Edges.Length];weights=new float[data.Edges.Length];
        var fanin=new float[n];
        foreach(var e in data.Edges){offsets[e[0]+1]++;fanin[e[1]]+=Math.Min(Math.Abs(e[2]),60);}
        for(int i=1;i<=n;i++)offsets[i]+=offsets[i-1];
        var cursors=(int[])offsets.Clone();
        foreach(var e in data.Edges)
        {
            int j=cursors[e[0]]++;targets[j]=e[1];
            var sameType=data.Nodes[e[0]].Type==data.Nodes[e[1]].Type;
            // Shiu/FlyBrain: 0.275 mV per signed synapse; cap and fan-in damping are explicit model assumptions.
            weights[j]=Math.Clamp(e[2],-60,60)*.275f*(sameType?.1f:1f)*Math.Min(1,5000f/Math.Max(1,fanin[e[1]]));
        }
        Reset();
    }
    static int[] Sample(int[] ids,int max)
    {
        if(ids.Length<=max)return ids;
        var result=new int[max];for(int i=0;i<max;i++)result[i]=ids[(int)((long)i*ids.Length/max)];return result;
    }
    public void Reset(){Array.Fill(voltage,-52);Array.Clear(current);Array.Clear(rates);Array.Clear(drive);Array.Clear(adaptation);foreach(var d in delayed)Array.Clear(d);Array.Clear(refractory);delaySlot=0;Flight=Fear=Feeding=Turn=OdorTurn=NavigationTurn=DescendingTurn=RewardSignal=0;}
    public void SetInput(float threat,float contact,float fullness,float turn,float travel,float wall,Settings s,
        float odor=0,float odorTurn=0,float wallTurn=0,float positionX=.5f,float positionY=.5f,float reward=0,float punishment=0,float frameDt=1f/120)
    {
        Array.Clear(drive);
        void Add(int[] ids,float v){foreach(int i in ids)drive[i]+=v*s.SensoryGain;}
        ThreatDrive=threat;WallDrive=wall;FoodDrive=travel;
        odorDirectionInput=Math.Clamp(odorTurn,-1,1);wallDirectionInput=Math.Clamp(wallTurn,-1,1);
        // External drives replace missing retina/olfaction/proprioception. They enter sensory populations only.
        float motivation=Math.Clamp((100-fullness)/100,0,1);
        Add(groups["visual"],Math.Clamp(travel*(8+4*motivation)+threat*10+wall*8,0,23));
        Add(visualLeft,Math.Max(0,-turn)*4.5f);Add(visualRight,Math.Max(0,turn)*4.5f);
        Add(visualMotionStim,Math.Clamp(wall*9+punishment*14+threat*5,0,24));
        Add(visualMotionLeft,Math.Max(0,-wallTurn)*10);Add(visualMotionRight,Math.Max(0,wallTurn)*10);
        Add(olfactoryStim,Math.Clamp(odor*(7+13*motivation),0,22));
        Add(olfactoryLeft,Math.Max(0,-odorTurn)*8);Add(olfactoryRight,Math.Max(0,odorTurn)*8);
        Add(groups["taste"],contact*14);
        Add(rewardStim,Math.Clamp(Math.Abs(reward-punishment)*20,0,24));
        float decay=MathF.Exp(-Math.Max(0,s.MemoryDecayPerMinute)/60*frameDt);
        float valence=Math.Clamp(reward*s.SugarReward-punishment*s.EdgePunishment,-1,1);RewardSignal=valence;
        for(int k=0;k<placeCells.Length;k++)
        {
            float dx=positionX-placeX[k],dy=positionY-placeY[k];float activation=MathF.Exp(-(dx*dx+dy*dy)/.018f);placeActivity[k]=activation;
            drive[placeCells[k]]+=activation*(2.5f+odor*4)*s.SensoryGain;
            // One sugar produces a modest trace; repeated rewards at the same place
            // strengthen it, so frequency matters instead of one event saturating memory.
            placeValue[k]=Math.Clamp(placeValue[k]*decay+activation*valence*s.LearningRate*frameDt*1.2f,-1,1);
        }
        UpdateRememberedPlace();
    }
    public void Step(float gain)
    {
        // 1 ms exponential Euler LIF: Vrest/reset -52 mV, threshold -45 mV,
        // tau_m 20 ms, tau_syn 5 ms, adaptation 1.5 mV/spike, 2 ms synaptic delay, 3 ms refractory.
        int count=0;
        var arriving=delayed[delaySlot];
        for(int i=0;i<voltage.Length;i++)
        {
            current[i]=Math.Clamp(current[i]*.81873075f+arriving[i],-40,40);arriving[i]=0;
            adaptation[i]*=.9950125f;rates[i]*=.9900498f;
            if(refractory[i]>0){refractory[i]--;continue;}
            voltage[i]=-52+(voltage[i]+52)*.95122945f+(current[i]+drive[i]-adaptation[i])*.04877055f;
            if(voltage[i]>=-45){voltage[i]=-52;refractory[i]=3;adaptation[i]+=1.5f;rates[i]+=10;spikes[count++]=i;}
        }
        var future=delayed[(delaySlot+2)%3];
        for(int k=0;k<count;k++)for(int j=offsets[spikes[k]];j<offsets[spikes[k]+1];j++)future[targets[j]]+=weights[j]*gain;
        delaySlot=(delaySlot+1)%3;
        TotalSpikes+=count;
        VisualRate=Mean(groups["visual"]);TasteRate=Mean(groups["taste"]);EscapeRate=Mean(groups["escape"]);
        FlightRate=Mean(groups["flight"]);WingRate=Mean(groups["wing"]);FeedRate=Mean(groups["feed"]);
        OptomotorRate=Mean(optomotor);ReverseRate=Mean(reverse);
        VisualMotionRate=Mean(groups["visual_motion"]);OlfactoryRate=Mean(groups["olfactory"]);MemoryRate=Mean(groups["memory"]);RewardRate=Mean(groups["reward"]);
        NavigationRate=Mean(groups["navigation"]);DescendingRate=Mean(groups["descending"]);MotorRate=Mean(groups["motor"]);
        OptomotorTurn=Math.Clamp((Mean(optoRight)-Mean(optoLeft))/55,-1,1);
        float connected=Math.Clamp(gain,0,1),odorGate=Math.Clamp(OlfactoryRate/8,0,1)*connected,wallGate=Math.Clamp(VisualMotionRate/8,0,1)*connected;
        // Direction is encoded by the bilateral receptor input. A turn is emitted only
        // while the matching sensory population is firing and graph propagation is on.
        OdorTurn=Math.Clamp(odorDirectionInput*odorGate,-1,1);
        float navigationLateral=(Mean(navigationRight)-Mean(navigationLeft))/50;
        NavigationTurn=Math.Clamp(navigationLateral*.35f+wallDirectionInput*wallGate*.9f,-1,1);
        DescendingTurn=Math.Clamp((Mean(descendingRight)-Mean(descendingLeft))/50,-1,1);
        Flight=Math.Clamp(FlightRate/55+WingRate/14+MotorRate/80+DescendingRate/180,0,1);
        Fear=Math.Clamp(EscapeRate/50+DescendingRate/160,0,1);
        // MN9 does not reliably fire in this reduced induced subgraph. Feeding therefore uses the sensory
        // sweet-GRN rate as a documented fallback; the evidence window exposes MN9 separately instead of hiding it.
        Feeding=Math.Clamp(TasteRate/50,0,1);
        Turn=Math.Clamp((Mean(steerLeft)-Mean(steerRight))/55,-1,1);
    }
    void UpdateRememberedPlace()
    {
        float best=0,worst=0;int bestIndex=-1,worstIndex=-1;
        for(int i=0;i<placeValue.Length;i++)
        {
            if(placeValue[i]>best){best=placeValue[i];bestIndex=i;}
            if(placeValue[i]<worst){worst=placeValue[i];worstIndex=i;}
        }
        if(bestIndex>=0){RememberedX=placeX[bestIndex];RememberedY=placeY[bestIndex];}
        if(worstIndex>=0){AvoidedX=placeX[worstIndex];AvoidedY=placeY[worstIndex];}
        MemoryConfidence=Math.Clamp(best,0,1);AvoidanceConfidence=Math.Clamp(-worst,0,1);
    }
    public float[] ExportMemory()=>(float[])placeValue.Clone();
    public void ImportMemory(float[] values)
    {
        int count=Math.Min(values.Length,placeValue.Length);for(int i=0;i<count;i++)placeValue[i]=float.IsFinite(values[i])?Math.Clamp(values[i],-1,1):0;UpdateRememberedPlace();
    }
    float Mean(int[] ids){float sum=0;foreach(int i in ids)sum+=rates[i];return ids.Length==0?0:sum/ids.Length;}
}
