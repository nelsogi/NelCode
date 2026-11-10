using System.Text.Json;
using System.Text.Json.Serialization;
using NelCode.Engine;

namespace NelCode.Storage;

/// <summary>
/// settings.json : réglages de génération et dernier modèle utilisé.
///   { "temperature": 0.8, "topK": 20, "topP": 0.9, "lastModel": "v1" }
/// Sauvegardé à chaque /temp, /topk, /topp et /model.
/// </summary>
public sealed class Settings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,   // accents lisibles
    };

    // Valeurs de départ de MiniChat (ChatSession.Default*).
    public double Temperature { get; set; } = 0.8;
    public int TopK { get; set; } = 20;
    public double TopP { get; set; } = 0.9;
    public string? LastModel { get; set; }

    [JsonIgnore]
    public Sampling Sampling => new(Temperature, TopK, TopP);

    public static double ClampTemperature(double value) => Math.Clamp(value, 0.1, 2.0);
    public static int ClampTopK(int value) => Math.Max(0, value);
    public static double ClampTopP(double value) => Math.Clamp(value, 0.05, 1.0);

    /// <summary>Le fichier, ou les réglages par défaut s'il manque. Illisible : défaut, et warning dit pourquoi.</summary>
    public static Settings Load(out string? warning)
    {
        warning = null;
        if (!File.Exists(AppPaths.Settings))
            return new Settings();

        try
        {
            var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(AppPaths.Settings), JsonOptions) ?? new Settings();
            settings.Temperature = ClampTemperature(settings.Temperature);
            settings.TopK = ClampTopK(settings.TopK);
            settings.TopP = ClampTopP(settings.TopP);
            return settings;
        }
        catch (JsonException e)
        {
            warning = $"settings.json illisible (ligne {e.LineNumber + 1}) : réglages par défaut.";
            return new Settings();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(AppPaths.Root);
        File.WriteAllText(AppPaths.Settings, JsonSerializer.Serialize(this, JsonOptions));
    }
}
