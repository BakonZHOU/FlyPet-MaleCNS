using System.Text.Json;

namespace FlyPet;

public enum PetSkin { Fly, Cockroach }

public sealed class Settings
{
    public const int MinPetSize=60,MaxPetSize=800;
    public int SchemaVersion { get; set; } = 14;
    public PetSkin Skin { get; set; } = PetSkin.Fly;
    public int FramesPerSecond { get; set; } = 90;
    public int PetSize { get; set; } = 90;
    public int CockroachSize { get; set; } = 120;
    public int GiantCockroachSize { get; set; } = 500;
    public float GiantCockroachChance { get; set; } = .1f;
    public float FlightSpeed { get; set; } = 400;
    public float FearRadius { get; set; } = 250;
    public float EscapeSafeRadiusMultiplier { get; set; } = 1.35f;
    public float EscapeAccelerationGain { get; set; } = 1.4f;
    public float SwatDamage { get; set; } = 20;
    public float EvanescenceChance { get; set; } = .30f;
    public float RockSolidChance { get; set; } = .30f;
    public float HungerPerMinute { get; set; } = .25f;
    public float FlightFullnessCostPerSecond { get; set; } = .003f;
    public float AlarmSeconds { get; set; } = 1.4f;
    public float FeedPerSecond { get; set; } = 15;
    public float StarvationDamagePerSecond { get; set; } = .08f;
    public float AlbinoChance { get; set; } = .1f;
    public float AlbinoSpeedMultiplier { get; set; } = 1.55f;
    public float RespawnMinSeconds { get; set; } = 45;
    public float RespawnMaxSeconds { get; set; } = 90;
    public float NeuralGain { get; set; } = 1;
    public float SensoryGain { get; set; } = 1;
    public float SugarAttractionRadius { get; set; } = 2200;
    public float SugarNutrition { get; set; } = 32;
    public float SugarEatingSeconds { get; set; } = 1.8f;
    public float SatiatedThreshold { get; set; } = 85;
    public float SatiatedRegenPerSecond { get; set; } = 1.5f;
    public float LearningRate { get; set; } = .18f;
    public float MemoryDecayPerMinute { get; set; } = .002f;
    public float SugarReward { get; set; } = 1;
    public float EdgePunishment { get; set; } = .75f;
    public float CollisionMemoryDiameter { get; set; } = 300;
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
    public bool VoiceEnabled { get; set; }
    public int VoiceCommandTimeoutSeconds { get; set; } = 3;
    public List<string> VoiceSearchFolders { get; set; } = DefaultVoiceSearchFolders();
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
            // v5 slows the untouched v4 hunger defaults to roughly one hour from the
            // normal 65% spawn state. Values the user customized are preserved.
            if(exists&&storedSchema<5)
            {
                if(Math.Abs(s.HungerPerMinute-1.6f)<.0001f)s.HungerPerMinute=.8f;
                if(Math.Abs(s.FlightFullnessCostPerSecond-.02f)<.0001f)s.FlightFullnessCostPerSecond=.008f;
                if(Math.Abs(s.StarvationDamagePerSecond-.35f)<.0001f)s.StarvationDamagePerSecond=.2f;
                s.SchemaVersion=5;changed=true;
            }
            if(exists&&storedSchema<6){s.SchemaVersion=6;changed=true;}
            if(exists&&storedSchema<7)
            {
                if(Math.Abs(s.RespawnMinSeconds-12)<.0001f&&Math.Abs(s.RespawnMaxSeconds-40)<.0001f)
                {s.RespawnMinSeconds=45;s.RespawnMaxSeconds=90;}
                s.SchemaVersion=7;changed=true;
            }
            if(exists&&storedSchema<8)
            {
                if(Math.Abs(s.HungerPerMinute-.8f)<.0001f)s.HungerPerMinute=.6f;
                if(Math.Abs(s.FlightFullnessCostPerSecond-.008f)<.0001f)s.FlightFullnessCostPerSecond=.006f;
                if(Math.Abs(s.StarvationDamagePerSecond-.2f)<.0001f)s.StarvationDamagePerSecond=.08f;
                s.SchemaVersion=8;changed=true;
            }
            if(exists&&storedSchema<9)
            {
                // Preserve customized combat values while upgrading the old defaults.
                if(Math.Abs(s.SwatDamage-40)<.0001f)s.SwatDamage=20;
                if(Math.Abs(s.EvanescenceChance-.16f)<.0001f)s.EvanescenceChance=.30f;
                if(Math.Abs(s.RockSolidChance-.16f)<.0001f)s.RockSolidChance=.30f;
                s.SchemaVersion=9;changed=true;
            }
            if(exists&&storedSchema<10){s.SchemaVersion=10;changed=true;}
            if(exists&&storedSchema<11)
            {
                if(Math.Abs(s.HungerPerMinute-.6f)<.0001f)s.HungerPerMinute=.25f;
                if(Math.Abs(s.FlightFullnessCostPerSecond-.006f)<.0001f)s.FlightFullnessCostPerSecond=.003f;
                s.SchemaVersion=11;changed=true;
            }
            if(exists&&storedSchema<12){s.SchemaVersion=12;changed=true;}
            if(exists&&storedSchema<13){s.VoiceSearchFolders=DefaultVoiceSearchFolders();s.SchemaVersion=13;changed=true;}
            if(exists&&storedSchema<14){s.VoiceCommandTimeoutSeconds=3;s.SchemaVersion=14;changed=true;}
            if(changed)s.Save();
            s.Validate(); return s;
        }
        catch (Exception e) { LoadWarning = "设置文件无法读取，已使用默认值：" + e.Message; return new(); }
    }
    static float Safe(float value, float min, float max, float fallback) => float.IsFinite(value) ? Math.Clamp(value,min,max) : fallback;
    public void Validate()
    {
        FramesPerSecond = Math.Clamp(FramesPerSecond, 20, 120); PetSize = Math.Clamp(PetSize, MinPetSize, MaxPetSize);
        CockroachSize=Math.Clamp(CockroachSize,MinPetSize,MaxPetSize);
        GiantCockroachSize=Math.Clamp(GiantCockroachSize,MinPetSize,MaxPetSize);
        if(!Enum.IsDefined(Skin))Skin=PetSkin.Fly;
        FlightSpeed = Safe(FlightSpeed, 30, 900,400); FearRadius = Safe(FearRadius,80,800,250);
        EscapeSafeRadiusMultiplier=Safe(EscapeSafeRadiusMultiplier,1,2.5f,1.35f);EscapeAccelerationGain=Safe(EscapeAccelerationGain,0,4,1.4f);
        FlightFullnessCostPerSecond=Safe(FlightFullnessCostPerSecond,0,10,.003f);
        AlarmSeconds=Safe(AlarmSeconds,.1f,10,1.4f);
        SwatDamage = Safe(SwatDamage,1,100,20); HungerPerMinute = Safe(HungerPerMinute,0,60,.25f);
        EvanescenceChance=Safe(EvanescenceChance,0,1,.30f);
        RockSolidChance=Math.Clamp(float.IsFinite(RockSolidChance)?RockSolidChance:.30f,0,1-EvanescenceChance);
        FeedPerSecond = Safe(FeedPerSecond,1,100,15); StarvationDamagePerSecond = Safe(StarvationDamagePerSecond,0,20,.08f);
        AlbinoChance=Safe(AlbinoChance,0,1,.1f);GiantCockroachChance=Safe(GiantCockroachChance,0,1,.1f);AlbinoSpeedMultiplier=Safe(AlbinoSpeedMultiplier,1,3,1.55f);
        RespawnMinSeconds = Safe(RespawnMinSeconds,1,3600,45); RespawnMaxSeconds = Safe(RespawnMaxSeconds,RespawnMinSeconds,7200,90);
        NeuralGain = Safe(NeuralGain,0,4,1); SensoryGain = Safe(SensoryGain,0,4,1);
        SugarAttractionRadius = Safe(SugarAttractionRadius,100,10000,2200); MaxSugar = Math.Clamp(MaxSugar,1,30);
        SugarNutrition=Safe(SugarNutrition,1,100,32);SugarEatingSeconds=Safe(SugarEatingSeconds,.2f,10,1.8f);
        SatiatedThreshold=Safe(SatiatedThreshold,50,100,85);SatiatedRegenPerSecond=Safe(SatiatedRegenPerSecond,0,20,1.5f);
        LearningRate=Safe(LearningRate,0,2,.18f);MemoryDecayPerMinute=Safe(MemoryDecayPerMinute,0,1,.002f);
        SugarReward=Safe(SugarReward,0,2,1);EdgePunishment=Safe(EdgePunishment,0,2,.75f);
        CollisionMemoryDiameter=Safe(CollisionMemoryDiameter,80,1200,300);
        MonitorIndex = Math.Max(0, MonitorIndex);
        VoiceSearchFolders=VoiceSearchFolders?.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList()??DefaultVoiceSearchFolders();
        VoiceCommandTimeoutSeconds=Math.Clamp(VoiceCommandTimeoutSeconds,2,10);
    }
    public void Save()
    {
        Validate(); Directory.CreateDirectory(Folder);
        var temp = FileName + ".tmp"; File.WriteAllText(temp, JsonSerializer.Serialize(this,JsonOptions)); File.Move(temp,FileName,true);
    }
    public static string VoiceModelFolder => Path.Combine(Folder,"models","vosk-model-small-cn-0.22");
    static List<string> DefaultVoiceSearchFolders()=>new[]{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads")}.Where(Directory.Exists).ToList();
}
