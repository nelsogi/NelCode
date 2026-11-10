using NelCode.Storage;
using NelCode.UI;
using Spectre.Console;

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
                ShowInfo(info);
                name ??= string.IsNullOrWhiteSpace(info.Name) ? Path.GetFileNameWithoutExtension(source) : info.Name.Trim();
            }
            string finalName = name ?? Path.GetFileName(source.TrimEnd('\\', '/'));

            // Nom déjà pris : on demande avant le long travail d'extraction et de chargement.
            if (Directory.Exists(ModelLibrary.Folder(finalName)) && !replace)
            {
                bool? choice = ResolveConflict(ref finalName);
                if (choice is null)
                {
                    Messages.Dim("Import annulé.");
                    return;
                }
                replace = choice.Value;
            }
            name = finalName;

            bool replacesActive = replace && name == context.ModelName;
            var (entry, parameters) = Messages.Spin($"Installation et test de chargement de « {name} »...",
                () => ModelLibrary.Import(source, finalName, replace));
            Messages.Success($"Modèle « {entry.Name} » {(replace ? "remplacé" : "installé")} ({entry.Bytes / 1e6:0} Mo), il se charge correctement.");

            if (info?.Parameters is { } announced && announced != parameters)
                Messages.Warning($"model-info.json annonce {announced:N0} paramètres, mais le modèle en a {parameters:N0}. " +
                                 "Les infos du fichier sont peut-être fausses (le modèle, lui, fonctionne).");

            if (replacesActive || context.Chat is null)
            {
                Messages.Spin($"Chargement de « {name} »...", () => { context.LoadModel(finalName); return 0; });
                Messages.Dim(replacesActive ? "Le modèle actif a été rechargé." : "C'est maintenant le modèle actif.");
            }
            else
            {
                Messages.Dim($"/model {name} pour l'utiliser.");
            }
        }
        catch (Exception e) when (e is IOException or ArgumentException or InvalidDataException or UnauthorizedAccessException)
        {
            throw new UserException($"Import refusé : {e.Message}");
        }
    }

    /// <summary>Le nom existe déjà : remplacer (true), autre nom (false, name changé), annuler (null).</summary>
    private static bool? ResolveConflict(ref string name)
    {
        int choice = Prompts.Choose($"Un modèle « {name} » existe déjà. Que faire ?",
            "Remplacer", "Choisir un autre nom", "Annuler");
        if (choice == 0) return true;
        if (choice != 1) return null;

        while (true)
        {
            string other = Prompts.Ask("Nouveau nom (vide = annuler) :");
            if (other.Length == 0) return null;
            if (!ModelLibrary.IsValidName(other))
                Messages.Warning($"« {other} » n'est pas un nom valide (pas de \\ / : * ? \" < > |).");
            else if (Directory.Exists(ModelLibrary.Folder(other)))
                Messages.Warning($"« {other} » existe aussi.");
            else
            {
                name = other;
                return false;
            }
        }
    }

    private static void ShowInfo(ModelInfo info)
    {
        var grid = new Grid().AddColumn(new GridColumn().PadRight(2)).AddColumn();
        void Row(string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                grid.AddRow(new Markup($"[{Theme.Dim}]{label}[/]"), new Text(value));
        }

        Row("Nom", info.Name);
        Row("Description", info.Description);
        Row("Créé le", info.CreatedAt?.LocalDateTime.ToString("dd'/'MM'/'yyyy HH:mm"));
        Row("Paramètres", info.Parameters is { } p ? $"{p:N0} ({p / 1e6:0.0} M)" : null);
        Row("Architecture", info.Architecture);
        Row("Entraînement", info.TrainingSteps is { } steps ? $"{steps:N0} pas" : null);
        Row("Val loss", info.ValLoss?.ToString("F3"));
        Row("Format", $"version {info.FormatVersion}");

        AnsiConsole.Write(new Panel(grid)
            .Header($"[{Theme.Accent}] model-info.json [/]")
            .RoundedBorder()
            .BorderStyle(Theme.Style(Theme.Border)));
    }
}
