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
        if (d.Nodes.Length is < 1 or > 20000 || d.Edges.Length > 2000000) throw new InvalidDataException("Circuit size out of bounds");
        if (d.Edges.Any(e => e.Length != 3 || e[0] < 0 || e[1] < 0 || e[0] >= d.Nodes.Length || e[1] >= d.Nodes.Length || Math.Abs((long)e[2]) > 1000000)) throw new InvalidDataException("Invalid circuit edge");
        foreach(var g in new[]{"visual","escape","flight","steer","wing","taste","feed"})
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
        groups = new[]{"visual","escape","flight","steer","wing","taste","feed","optomotor","reverse"}.ToDictionary(g=>g,g=>Enumerable.Range(0,n).Where(i=>data.Nodes[i].Groups.Contains(g)).ToArray());
        steerLeft=groups["steer"].Where(i=>data.Nodes[i].Side=="L").ToArray();steerRight=groups["steer"].Where(i=>data.Nodes[i].Side=="R").ToArray();
        visualLeft=groups["visual"].Where(i=>data.Nodes[i].Side=="L").ToArray();visualRight=groups["visual"].Where(i=>data.Nodes[i].Side=="R").ToArray();
        optomotor=groups["optomotor"];reverse=groups["reverse"];
        optoLeft=optomotor.Where(i=>data.Nodes[i].Side=="L").ToArray();optoRight=optomotor.Where(i=>data.Nodes[i].Side=="R").ToArray();
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
    public void Reset(){Array.Fill(voltage,-52);Array.Clear(current);Array.Clear(rates);Array.Clear(drive);Array.Clear(adaptation);foreach(var d in delayed)Array.Clear(d);Array.Clear(refractory);delaySlot=0;Flight=Fear=Feeding=Turn=0;}
    public void SetInput(float threat,float contact,float fullness,float turn,float travel,float wall,Settings s)
    {
        Array.Clear(drive);
        void Add(int[] ids,float v){foreach(int i in ids)drive[i]+=v*s.SensoryGain;}
        ThreatDrive=threat;WallDrive=wall;FoodDrive=travel;
        // External drives replace missing retina/olfaction/proprioception. They enter sensory populations only.
        float motivation=Math.Clamp((100-fullness)/100,0,1);
        Add(groups["visual"],Math.Clamp(travel*(8+4*motivation)+threat*10+wall*8,0,23));
        Add(visualLeft,Math.Max(0,-turn)*4.5f);Add(visualRight,Math.Max(0,turn)*4.5f);
        Add(groups["taste"],contact*14);
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
        OptomotorTurn=Math.Clamp((Mean(optoRight)-Mean(optoLeft))/55,-1,1);
        Flight=Math.Clamp(FlightRate/55+WingRate/14,0,1);
        Fear=Math.Clamp(EscapeRate/50,0,1);
        // MN9 does not reliably fire in this reduced induced subgraph. Feeding therefore uses the sensory
        // sweet-GRN rate as a documented fallback; the evidence window exposes MN9 separately instead of hiding it.
        Feeding=Math.Clamp(TasteRate/50,0,1);
        Turn=Math.Clamp((Mean(steerLeft)-Mean(steerRight))/55,-1,1);
    }
    float Mean(int[] ids){float sum=0;foreach(int i in ids)sum+=rates[i];return ids.Length==0?0:sum/ids.Length;}
}
