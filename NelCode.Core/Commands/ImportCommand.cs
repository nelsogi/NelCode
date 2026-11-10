using NelCode.Storage;

namespace NelCode.Commands;

public sealed class ImportCommand : ICommand
{
    private const string ReplaceOption = "--replace";

    public string Name => "import";
    public string Description => "Installe un modèle : fichier .nelmodel, ou dossier de modèle MiniChat";
    public string Usage => $"/import <fichier.nelmodel | dossier> [nom] [{ReplaceOption}]";

    public void Execute(CommandContext context, string[] args)
    {
        bool replace = args.Contains(ReplaceOption, StringComparer.OrdinalIgnoreCase);
        args = args.Where(a => !a.Equals(ReplaceOption, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (args.Length is 0 or > 2)
            throw new UserException($"Utilisation : {Usage}  (chemin avec espaces : entre guillemets)");

        string source = Path.GetFullPath(args[0], context.WorkingDirectory);
        string? name = args.Length > 1 ? args[1] : null;

        try
        {
            ModelInfo? info = null;
            if (NelModelPackage.IsPackage(source))
            {
                info = NelModelPackage.Read(source);
                ShowInfo(context.Output, info);
                name ??= string.IsNullOrWhiteSpace(info.Name) ? Path.GetFileNameWithoutExtension(source) : info.Name.Trim();
            }
            string finalName = name ?? Path.GetFileName(source.TrimEnd('\\', '/'));

            // Nom déjà pris : on demande avant le long travail d'extraction et de chargement.
            if (Directory.Exists(ModelLibrary.Folder(finalName)) && !replace)
            {
                bool? choice = ResolveConflict(context.Output, ref finalName);
                if (choice is null)
                {
                    context.Output.Dim("Import annulé.");
                    return;
                }
                replace = choice.Value;
            }
            name = finalName;

            bool replacesActive = replace && name == context.ModelName;
            var (entry, parameters) = context.Output.Busy($"Installation et test de chargement de « {name} »...",
                () => ModelLibrary.Import(source, finalName, replace));
            context.Output.Success($"Modèle « {entry.Name} » {(replace ? "remplacé" : "installé")} ({entry.Bytes / 1e6:0} Mo), il se charge correctement.");

            if (info?.Parameters is { } announced && announced != parameters)
                context.Output.Warning($"model-info.json annonce {announced:N0} paramètres, mais le modèle en a {parameters:N0}. " +
                                 "Les infos du fichier sont peut-être fausses (le modèle, lui, fonctionne).");

            if (replacesActive || context.Chat is null)
            {
                context.Output.Busy($"Chargement de « {name} »...", () => context.LoadModel(finalName));
                context.Output.Dim(replacesActive ? "Le modèle actif a été rechargé." : "C'est maintenant le modèle actif.");
            }
            else
            {
                context.Output.Dim($"/model {name} pour l'utiliser.");
            }
        }
        catch (Exception e) when (e is IOException or ArgumentException or InvalidDataException or UnauthorizedAccessException)
        {
            throw new UserException($"Import refusé : {e.Message}");
        }
    }

    /// <summary>Le nom existe déjà : remplacer (true), autre nom (false, name changé), annuler (null).</summary>
    private static bool? ResolveConflict(IOutput output, ref string name)
    {
        int choice = output.Choose($"Un modèle « {name} » existe déjà. Que faire ?",
            "Remplacer", "Choisir un autre nom", "Annuler");
        if (choice == 0) return true;
        if (choice != 1) return null;

        while (true)
        {
            string other = output.Ask("Nouveau nom (vide = annuler) :");
            if (other.Length == 0) return null;
            if (!ModelLibrary.IsValidName(other))
                output.Warning($"« {other} » n'est pas un nom valide (pas de \\ / : * ? \" < > |).");
            else if (Directory.Exists(ModelLibrary.Folder(other)))
                output.Warning($"« {other} » existe aussi.");
            else
            {
                name = other;
                return false;
            }
        }
    }

    private static void ShowInfo(IOutput output, ModelInfo info)
    {
        var rows = new List<(string, Styled)>();
        void Row(string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                rows.Add((label, new Styled(value)));
        }

        Row("Nom", info.Name);
        Row("Description", info.Description);
        Row("Créé le", info.CreatedAt?.LocalDateTime.ToString("dd'/'MM'/'yyyy HH:mm"));
        Row("Paramètres", info.Parameters is { } p ? $"{p:N0} ({p / 1e6:0.0} M)" : null);
        Row("Architecture", info.Architecture);
        Row("Entraînement", info.TrainingSteps is { } steps ? $"{steps:N0} pas" : null);
        Row("Val loss", info.ValLoss?.ToString("F3"));
        Row("Format", $"version {info.FormatVersion}");

        output.Details("model-info.json", rows);
    }
}
