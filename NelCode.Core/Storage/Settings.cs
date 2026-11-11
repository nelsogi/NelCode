using System.Text.Json;
using System.Text.Json.Serialization;
using NelCode.Engine;

namespace NelCode.Storage;

/// <summary>
/// settings.json : réglages de génération et dernier modèle utilisé, partagés par la console et la fenêtre.
///   { "temperature": 0.8, "topK": 20, "topP": 0.9, "maxReplyTokens": 80, "lastModel": "v1", "theme": "dark", "window": {...} }
/// Sauvegardé à chaque /temp, /topk, /topp et /model, et par les réglages de Desktop.
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

    /// <summary>Longueur max d'une réponse, en tokens (~3 caractères chacun).</summary>
    public int MaxReplyTokens { get; set; } = 80;

    public string? LastModel { get; set; }

    /// <summary>Thème de Desktop : "dark" ou "light".</summary>
    public string Theme { get; set; } = "dark";

    /// <summary>La fenêtre de Desktop à sa dernière fermeture.</summary>
    public WindowPlacement? Window { get; set; }

    [JsonIgnore]
    public Sampling Sampling => new(Temperature, TopK, TopP);

    public static double ClampTemperature(double value) => Math.Clamp(value, 0.1, 2.0);
    public static int ClampTopK(int value) => Math.Max(0, value);
    public static double ClampTopP(double value) => Math.Clamp(value, 0.05, 1.0);
    public static int ClampMaxReplyTokens(int value) => Math.Clamp(value, 16, 128);

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
            settings.MaxReplyTokens = ClampMaxReplyTokens(settings.MaxReplyTokens);
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

/// <summary>La fenêtre de Desktop : position et taille (hors plein écran), plein écran ou non, largeur de la colonne de gauche.</summary>
public sealed record WindowPlacement(int X, int Y, int Width, int Height, bool Maximized, int SidebarWidth);
