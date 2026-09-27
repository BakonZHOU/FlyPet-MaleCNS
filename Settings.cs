using System.Text.Json;

namespace FlyPet;

public sealed class Settings
{
    public int SchemaVersion { get; set; } = 4;
    public int FramesPerSecond { get; set; } = 90;
    public int PetSize { get; set; } = 90;
    public float FlightSpeed { get; set; } = 400;
    public float FearRadius { get; set; } = 250;
    public float SwatDamage { get; set; } = 40;
    public float HungerPerMinute { get; set; } = 1.6f;
    public float FlightFullnessCostPerSecond { get; set; } = .02f;
    public float AlarmSeconds { get; set; } = 1.4f;
    public float FeedPerSecond { get; set; } = 15;
    public float StarvationDamagePerSecond { get; set; } = .35f;
    public float AlbinoChance { get; set; } = .1f;
    public float AlbinoSpeedMultiplier { get; set; } = 1.55f;
    public float RespawnMinSeconds { get; set; } = 12;
    public float RespawnMaxSeconds { get; set; } = 40;
    public float NeuralGain { get; set; } = 1;
    public float SensoryGain { get; set; } = 1;
    public float SugarAttractionRadius { get; set; } = 2200;
    public int MaxSugar { get; set; } = 12;
    public int MonitorIndex { get; set; } = 0;
    public bool ShowMeters { get; set; } = false;
    public bool Invincible { get; set; }
    public bool EdgeSensing { get; set; } = true;
    public bool NeuralSteering { get; set; } = true;
    public bool RestEnabled { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool ShowLaunchMenu { get; set; }
    public bool PauseWhenHidden { get; set; } = true;
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlyPet");
    public static string FileName => Path.Combine(Folder, "settings.json");
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public static string? LoadWarning;
    public static Settings Load()
    {
        LoadWarning=null;
        try
        {
            bool exists=File.Exists(FileName);string raw=exists?File.ReadAllText(FileName):"";using var doc=exists?JsonDocument.Parse(raw):null;
            var s=exists?JsonSerializer.Deserialize<Settings>(raw, JsonOptions) ?? new():new();
            int storedSchema=exists&&doc!.RootElement.TryGetProperty(nameof(SchemaVersion),out var schemaNode)&&schemaNode.TryGetInt32(out var parsedSchema)?parsedSchema:0;
            bool changed=false;
            if(exists&&!doc!.RootElement.TryGetProperty(nameof(FlightFullnessCostPerSecond),out _))
            {s.PetSize=90;s.FlightSpeed=400;s.HungerPerMinute=1.6f;s.FlightFullnessCostPerSecond=.02f;s.StarvationDamagePerSecond=.35f;s.FramesPerSecond=90;s.ShowMeters=false;changed=true;}
            // v4 changes the default hidden-skin chance from 4% to 10%. Preserve an
            // explicitly customized value while upgrading the old untouched default.
            if(exists&&(!doc!.RootElement.TryGetProperty(nameof(AlbinoChance),out _) || (storedSchema<4&&Math.Abs(s.AlbinoChance-.04f)<.0001f)))
            {s.AlbinoChance=.1f;s.AlbinoSpeedMultiplier=1.55f;changed=true;}
            if(exists&&storedSchema<4){s.SchemaVersion=4;s.ShowLaunchMenu=false;changed=true;}
            if(changed)s.Save();
            s.Validate(); return s;
        }
        catch (Exception e) { LoadWarning = "设置文件无法读取，已使用默认值：" + e.Message; return new(); }
    }
    static float Safe(float value, float min, float max, float fallback) => float.IsFinite(value) ? Math.Clamp(value,min,max) : fallback;
    public void Validate()
    {
        FramesPerSecond = Math.Clamp(FramesPerSecond, 20, 120); PetSize = Math.Clamp(PetSize, 60, 320);
        FlightSpeed = Safe(FlightSpeed, 30, 900,400); FearRadius = Safe(FearRadius,80,800,250);
        FlightFullnessCostPerSecond=Safe(FlightFullnessCostPerSecond,0,10,.02f);
        AlarmSeconds=Safe(AlarmSeconds,.1f,10,1.4f);
        SwatDamage = Safe(SwatDamage,1,100,40); HungerPerMinute = Safe(HungerPerMinute,0,60,1.6f);
        FeedPerSecond = Safe(FeedPerSecond,1,100,15); StarvationDamagePerSecond = Safe(StarvationDamagePerSecond,0,20,.35f);
        AlbinoChance=Safe(AlbinoChance,0,1,.1f);AlbinoSpeedMultiplier=Safe(AlbinoSpeedMultiplier,1,3,1.55f);
        RespawnMinSeconds = Safe(RespawnMinSeconds,1,3600,12); RespawnMaxSeconds = Safe(RespawnMaxSeconds,RespawnMinSeconds,7200,40);
        NeuralGain = Safe(NeuralGain,0,4,1); SensoryGain = Safe(SensoryGain,0,4,1);
        SugarAttractionRadius = Safe(SugarAttractionRadius,100,10000,2200); MaxSugar = Math.Clamp(MaxSugar,1,30);
        MonitorIndex = Math.Max(0, MonitorIndex);
    }
    public void Save()
    {
        Validate(); Directory.CreateDirectory(Folder);
        var temp = FileName + ".tmp"; File.WriteAllText(temp, JsonSerializer.Serialize(this,JsonOptions)); File.Move(temp,FileName,true);
    }
}
