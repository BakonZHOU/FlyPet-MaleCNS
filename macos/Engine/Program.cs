using System.Drawing;
using System.Numerics;
using System.Text.Json;

namespace FlyPet;

sealed class TickRequest
{
    public float Dt { get; set; } = 1f / 60;
    public int Left { get; set; }
    public int Top { get; set; }
    public int Width { get; set; } = 1440;
    public int Height { get; set; } = 900;
    public float MouseX { get; set; }
    public float MouseY { get; set; }
    public bool Threat { get; set; } = true;
    public string? Action { get; set; }
}

static class Program
{
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    static int Main(string[] args)
    {
        var settings = Settings.Load();
        var circuit = CircuitData.Load();
        var simulation = new Simulation(settings, circuit);
        var area = new Rectangle(0, 0, 1440, 900);
        simulation.Recenter(area);

        if (args.Contains("--probe"))
        {
            for (var i = 0; i < 120; i++) simulation.Update(1f / 120, area, new Vector2(-5000, -5000), false);
            Console.WriteLine(JsonSerializer.Serialize(Reply(simulation, circuit)));
            return 0;
        }

        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        string? line;
        while ((line = Console.ReadLine()) != null)
        {
            TickRequest? request;
            try { request = JsonSerializer.Deserialize<TickRequest>(line, Json); }
            catch { continue; }
            if (request == null) continue;
            area = new Rectangle(request.Left, request.Top, Math.Max(100, request.Width), Math.Max(100, request.Height));
            ApplyAction(request.Action, simulation, area);
            simulation.Update(Math.Clamp(request.Dt, 1f / 240, .1f), area,
                new Vector2(request.MouseX, request.MouseY), request.Threat);
            Console.WriteLine(JsonSerializer.Serialize(Reply(simulation, circuit)));
        }
        return 0;
    }

    static void ApplyAction(string? action, Simulation simulation, Rectangle area)
    {
        if (string.IsNullOrWhiteSpace(action)) return;
        var parts = action.Split(':');
        switch (parts[0])
        {
            case "skin" when parts.Length == 2:
                simulation.ChangeSkin(parts[1] == "cockroach" ? PetSkin.Cockroach : PetSkin.Fly);
                break;
            case "drop" when parts.Length == 3 && float.TryParse(parts[1], out var x) && float.TryParse(parts[2], out var y):
                simulation.AddSugar(new Vector2(x, y));
                break;
            case "hit" when parts.Length == 3 && float.TryParse(parts[1], out var hx) && float.TryParse(parts[2], out var hy):
                simulation.Hit(new Vector2(hx, hy));
                break;
            case "recenter": simulation.Recenter(area); break;
            case "revive": simulation.Revive(area); break;
        }
    }

    static object Reply(Simulation s, CircuitData circuit) => new
    {
        x = s.Position.X, y = s.Position.Y, heading = s.Heading, speed = s.Velocity.Length(),
        health = s.Health, fullness = s.Fullness, size = s.DisplaySize,
        skin = s.Settings.Skin.ToString().ToLowerInvariant(), dead = s.Dead, behavior = s.Behavior,
        spikes = s.Brain.TotalSpikes, neurons = circuit.Nodes.Length, edges = circuit.Edges.Length
    };
}
