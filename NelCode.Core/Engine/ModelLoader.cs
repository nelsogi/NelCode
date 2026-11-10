using System.Text.Json;
using System.Text.Json.Nodes;
using TorchSharp;
using static TorchSharp.torch;

namespace NelCode.Engine;

/// <summary>
/// Charge un dossier de modèle MiniChat :
///   config.json     la forme du réseau
///   tokenizer.json  { "Alphabet": "...", "Merges": [[g, d], ...], "Special": ["&lt;|doc|&gt;", ...] }
///   weights.dat     les poids (format TorchSharp)
/// </summary>
public static class ModelLoader
{
    public const string ConfigFile = "config.json";
    public const string TokenizerFile = "tokenizer.json";
    public const string WeightsFile = "weights.dat";

    public static readonly string[] RequiredFiles = { ConfigFile, TokenizerFile, WeightsFile };

    private sealed record TokenizerJson(string Alphabet, int[][] Merges, string[]? Special = null);

    public static bool IsModelFolder(string folder) => RequiredFiles.All(f => File.Exists(Path.Combine(folder, f)));

    /// <summary>
    /// La forme du réseau, sans les poids. Un config.json sans "Architecture" date d'avant ce réglage :
    /// ces modèles sont Classic (même règle que MiniChat).
    /// </summary>
    public static ModelConfig ReadConfig(string folder)
    {
        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(folder, ConfigFile)))?.AsObject()
                   ?? throw new InvalidDataException("config.json illisible.");
        if (!json.ContainsKey(nameof(ModelConfig.Architecture)))
            json[nameof(ModelConfig.Architecture)] = nameof(ModelArchitecture.Classic);
        return json.Deserialize<ModelConfig>() ?? throw new InvalidDataException("config.json illisible.");
    }

    public static TextGenerator Load(string folder, Device device)
    {
        if (!IsModelFolder(folder))
            throw new FileNotFoundException(
                $"'{folder}' n'est pas un dossier de modèle : il faut {string.Join(", ", RequiredFiles)}.");

        var config = ReadConfig(folder);
        var file = JsonSerializer.Deserialize<TokenizerJson>(File.ReadAllText(Path.Combine(folder, TokenizerFile)))
                   ?? throw new InvalidDataException("tokenizer.json illisible.");
        if (file.Special is null)
            throw new InvalidDataException(
                "Ce modèle date d'avant les tokens spéciaux (<|user|>, <|assistant|>, <|end|>) : il ne connaît pas le format de conversation.");

        var tokenizer = new BpeTokenizer(file.Alphabet, file.Merges.Select(m => (m[0], m[1])), file.Special);
        if (tokenizer.VocabSize != config.VocabSize)
            throw new InvalidDataException($"tokenizer.json ({tokenizer.VocabSize} tokens) ne correspond pas à config.json ({config.VocabSize}).");

        var model = new MiniGpt(config);
        model.load(Path.Combine(folder, WeightsFile));   // strict : un poids manquant ou en trop est une erreur
        model.eval();
        return new TextGenerator(model.to(device), tokenizer, device);
    }
}
