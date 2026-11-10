using System.Text.Json.Serialization;

namespace NelCode.Engine;

/// <summary>
/// Les deux architectures de MiniChat, écrites en toutes lettres dans config.json.
/// Classic : comme GPT-2 (embedding de position, LayerNorm, GELU).
/// Modern : comme LLaMA (RoPE, RMSNorm, SwiGLU, couches sans biais).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ModelArchitecture
{
    Classic,
    Modern,
}

/// <summary>Forme du réseau, lue dans config.json (copie de MiniChat.Config.ModelConfig).</summary>
public sealed class ModelConfig
{
    public int VocabSize { get; init; }
    public int BlockSize { get; init; } = 256;
    public int EmbedDim { get; init; } = 512;
    public int Heads { get; init; } = 8;
    public int Layers { get; init; } = 8;

    /// <summary>Sans effet ici (le modèle est toujours en mode eval), gardé pour relire config.json tel quel.</summary>
    public double Dropout { get; init; } = 0.1;

    public ModelArchitecture Architecture { get; init; } = ModelArchitecture.Modern;

    [JsonIgnore]
    public int HeadDim => EmbedDim / Heads;

    /// <summary>Largeur cachée de SwiGLU : 2/3 de 4 x EmbedDim, arrondie au multiple de 64 (comme MiniChat).</summary>
    [JsonIgnore]
    public int SwiGluHidden => (8 * EmbedDim / 3 + 63) / 64 * 64;
}
