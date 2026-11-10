using NelCode.Engine;
using NelCode.Storage;
using NelCode.UI;
using Spectre.Console;

namespace NelCode.Commands;

public sealed class ModelsCommand : ICommand
{
    public string Name => "models";
    public string Description => "Liste les modèles installés : nom, taille, date";
    public string Usage => "/models";

    public void Execute(CommandContext context, string[] args)
    {
        var models = ModelLibrary.List();
        if (models.Count == 0)
        {
            Messages.Warning($"Aucun modèle installé dans {AppPaths.Models}. /import <fichier.nelmodel> pour en ajouter un.");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Simple)
            .BorderStyle(Theme.Style(Theme.Border))
            .AddColumn(new TableColumn($"[{Theme.Dim}]Nom[/]").NoWrap())
            .AddColumn(new TableColumn($"[{Theme.Dim}]Taille[/]").RightAligned().NoWrap())
            .AddColumn(new TableColumn($"[{Theme.Dim}]Date[/]").NoWrap())
            .AddColumn(new TableColumn($"[{Theme.Dim}]Forme[/]"));

        foreach (var model in models)
        {
            bool active = model.Name == context.ModelName;
            table.AddRow(
                new Markup(active
                    ? $"[{Theme.Accent}]{Theme.ReplyBullet} {Markup.Escape(model.Name)}[/]"
                    : $"  [{Theme.Command}]{Markup.Escape(model.Name)}[/]"),
                new Text($"{model.Bytes / 1e6:0} Mo"),
                new Text(model.Date.ToString("dd'/'MM'/'yy HH:mm")),
                new Text(model.Config is { } c ? Describe(c) : "config.json illisible", Theme.Style(Theme.Dim)));
        }

        AnsiConsole.Write(table);
        Messages.Dim($"{Theme.ReplyBullet} = modèle actif. /model <nom> pour changer.");
    }

    public static string Describe(ModelConfig config) =>
        $"{config.Layers} couches · {config.Heads} têtes · {config.EmbedDim} dim · contexte {config.BlockSize} · " +
        (config.Architecture == ModelArchitecture.Modern ? "RoPE/SwiGLU" : "classique");
}
