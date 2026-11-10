using NelCode.Engine;
using static TorchSharp.torch;

namespace NelCode.Storage;

/// <summary>Un modèle installé : son nom (= nom du dossier), la taille de ses poids, leur date, sa forme.</summary>
public sealed record ModelEntry(string Name, long Bytes, DateTime Date, ModelConfig? Config);

/// <summary>Les modèles installés, un dossier chacun dans %APPDATA%\NelCode\models.</summary>
public static class ModelLibrary
{
    // Fichiers repris d'un dossier MiniChat. optimizer.dat (état d'AdamW) ne sert qu'à l'entraînement.
    private static readonly string[] FolderFiles = [.. ModelLoader.RequiredFiles, "training.json"];

    // Hors de models\ : ce qui est en cours d'import ou de remplacement n'apparaît jamais dans la liste.
    private static string TempFolder() => Path.Combine(AppPaths.Root, "import-tmp", Guid.NewGuid().ToString("N"));

    public static string Folder(string name) => Path.Combine(AppPaths.Models, name);

    /// <summary>Un nom utilisable comme nom de dossier, sans pouvoir sortir de models\ ("..", "a\b").</summary>
    public static bool IsValidName(string name) =>
        name.Trim().Length > 0 && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && name is not ("." or "..");

    public static bool Exists(string name) => IsValidName(name) && ModelLoader.IsModelFolder(Folder(name));

    /// <summary>Du plus récent au plus ancien.</summary>
    public static List<ModelEntry> List()
    {
        if (!Directory.Exists(AppPaths.Models))
            return [];

        return Directory.GetDirectories(AppPaths.Models)
            .Where(ModelLoader.IsModelFolder)
            .Select(Entry)
            .OrderByDescending(m => m.Date)
            .ToList();
    }

    /// <summary>
    /// Installe un fichier .nelmodel, ou un dossier de modèle MiniChat (pour les tests), sous le nom donné.
    /// Les fichiers sont d'abord mis dans un dossier temporaire et chargés (sur CPU) : le modèle n'apparaît
    /// dans la liste que s'il se charge. Sinon le dossier temporaire est supprimé et l'erreur remonte.
    /// replace : un modèle du même nom est remplacé, mais seulement une fois le nouveau vérifié.
    /// </summary>
    /// <returns>Le modèle installé, et son vrai nombre de paramètres (compté au chargement).</returns>
    public static (ModelEntry Entry, long Parameters) Import(string source, string name, bool replace = false)
    {
        if (!IsValidName(name))
            throw new ArgumentException($"« {name} » n'est pas un nom de modèle valide.");

        Action<string> fill;
        if (NelModelPackage.IsPackage(source))
        {
            NelModelPackage.Read(source);   // formatVersion et fichiers vérifiés avant d'extraire
            fill = staging => NelModelPackage.Extract(source, staging);
        }
        else if (ModelLoader.IsModelFolder(source))
        {
            fill = staging =>
            {
                foreach (string file in FolderFiles.Where(f => File.Exists(Path.Combine(source, f))))
                    File.Copy(Path.Combine(source, file), Path.Combine(staging, file));
            };
        }
        else
        {
            throw new FileNotFoundException(
                $"'{source}' n'est ni un fichier {NelModelPackage.Extension} ni un dossier de modèle MiniChat " +
                $"({string.Join(", ", ModelLoader.RequiredFiles)}).");
        }

        string target = Folder(name);
        if (Directory.Exists(target) && !replace)
            throw new IOException($"Un modèle « {name} » existe déjà.");

        string staging = TempFolder();
        Directory.CreateDirectory(staging);
        long parameters;
        try
        {
            fill(staging);
            try
            {
                using var generator = ModelLoader.Load(staging, CPU);
                parameters = generator.Model.ParameterCount;
            }
            catch (Exception e) when (e is not InvalidDataException)
            {
                throw new InvalidDataException($"le modèle ne se charge pas : {e.Message}", e);
            }
            Install(staging, target);
        }
        catch
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
            throw;
        }

        return (Entry(target), parameters);
    }

    /// <summary>Supprime un modèle installé (son dossier et tout ce qu'il contient).</summary>
    public static void Delete(string name)
    {
        if (!Exists(name))
            throw new ArgumentException($"Modèle inconnu : « {name} ».");
        Directory.Delete(Folder(name), recursive: true);
    }

    /// <summary>
    /// Met staging à la place de target. Un ancien modèle est d'abord mis de côté, et remis en place si le
    /// déplacement échoue : on ne perd jamais l'ancien sans avoir le nouveau.
    /// </summary>
    private static void Install(string staging, string target)
    {
        Directory.CreateDirectory(AppPaths.Models);
        if (!Directory.Exists(target))
        {
            Directory.Move(staging, target);
            return;
        }

        string old = TempFolder();
        Directory.Move(target, old);
        try
        {
            Directory.Move(staging, target);
        }
        catch
        {
            Directory.Move(old, target);
            throw;
        }
        Directory.Delete(old, recursive: true);
    }

    private static ModelEntry Entry(string folder)
    {
        var weights = new FileInfo(Path.Combine(folder, ModelLoader.WeightsFile));
        ModelConfig? config;
        try { config = ModelLoader.ReadConfig(folder); }
        catch (Exception) { config = null; }   // config.json abîmé : listé quand même
        return new ModelEntry(Path.GetFileName(folder), weights.Length, weights.LastWriteTime, config);
    }
}
