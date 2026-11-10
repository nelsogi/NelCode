using System.IO.Compression;
using System.Text.Json;
using NelCode.Engine;

namespace NelCode.Storage;

/// <summary>Le contenu de model-info.json (voir NELMODEL-FORMAT.md). Seul formatVersion est obligatoire.</summary>
public sealed record ModelInfo(
    int? FormatVersion,
    string? Name,
    DateTimeOffset? CreatedAt,
    long? Parameters,
    string? Architecture,
    double? ValLoss,
    int? TrainingSteps,
    string? Description);

/// <summary>
/// Un fichier .nelmodel : un zip avec config.json, tokenizer.json, weights.dat et model-info.json à la racine.
/// Read vérifie tout (zip lisible, fichiers présents, formatVersion) avant qu'on extraie quoi que ce soit.
/// </summary>
public static class NelModelPackage
{
    public const string Extension = ".nelmodel";
    public const string InfoFile = "model-info.json";

    /// <summary>La version la plus récente que ce Nel Code sait lire.</summary>
    public const int SupportedFormatVersion = 1;

    public static readonly string[] RequiredFiles = [.. ModelLoader.RequiredFiles, InfoFile];

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static bool IsPackage(string path) =>
        File.Exists(path) && path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);

    /// <summary>Lit et vérifie model-info.json. InvalidDataException avec un message clair si le paquet est refusé.</summary>
    public static ModelInfo Read(string path)
    {
        using var zip = Open(path);

        var missing = RequiredFiles.Where(f => zip.GetEntry(f) is null).ToList();
        if (missing.Count > 0)
            throw new InvalidDataException($"fichier(s) manquant(s) dans le .nelmodel : {string.Join(", ", missing)}.");

        ModelInfo info;
        try
        {
            using var stream = zip.GetEntry(InfoFile)!.Open();
            info = JsonSerializer.Deserialize<ModelInfo>(stream, JsonOptions)
                   ?? throw new InvalidDataException($"{InfoFile} est vide.");
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"{InfoFile} illisible : {e.Message}");
        }

        return info.FormatVersion switch
        {
            null => throw new InvalidDataException($"{InfoFile} n'indique pas de formatVersion."),
            > SupportedFormatVersion => throw new InvalidDataException(
                $"format trop récent (formatVersion {info.FormatVersion}, ce Nel Code lit jusqu'à la version " +
                $"{SupportedFormatVersion}). Mets Nel Code à jour."),
            < 1 => throw new InvalidDataException($"formatVersion {info.FormatVersion} invalide."),
            _ => info,
        };
    }

    /// <summary>
    /// Extrait les quatre fichiers attendus dans folder (et rien d'autre : un nom de chemin piégé dans le zip
    /// ne peut rien écrire ailleurs).
    /// </summary>
    public static void Extract(string path, string folder)
    {
        using var zip = Open(path);
        foreach (string file in RequiredFiles)
            zip.GetEntry(file)!.ExtractToFile(Path.Combine(folder, file));
    }

    private static ZipArchive Open(string path)
    {
        try
        {
            return ZipFile.OpenRead(path);
        }
        catch (InvalidDataException)
        {
            throw new InvalidDataException($"'{Path.GetFileName(path)}' n'est pas une archive zip valide.");
        }
    }
}
