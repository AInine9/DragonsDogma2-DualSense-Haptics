using System.Text.Json;

namespace DragonsDogma2DualSense;

sealed record Configuration(string Game = "", float Gain = 1.25f, bool AutoLaunchGame = true, bool RequireFocus = true, float DamageGain = 1)
{
    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, WriteIndented = true };
    public static Configuration Read()
    {
        var c = File.Exists(Files.At("config.json")) ? JsonSerializer.Deserialize<Configuration>(File.ReadAllText(Files.At("config.json")), Json)! : new();
        if (!float.IsFinite(c.Gain) || c.Gain < 0 || c.Gain > 3) throw new InvalidDataException("gain must be between 0 and 3");
        if (!float.IsFinite(c.DamageGain) || c.DamageGain < 0 || c.DamageGain > 3) throw new InvalidDataException("damage_gain must be between 0 and 3");
        return c;
    }
    public void Save() => File.WriteAllText(Files.At("config.json"), JsonSerializer.Serialize(this, Json).Replace("\"gain\": 1,", "\"gain\": 1.0,"));
}
