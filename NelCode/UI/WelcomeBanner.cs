using NelCode.Commands;
using Spectre.Console;

namespace NelCode.UI;

/// <summary>Le cadre d'accueil : le nom, le modèle actif, GPU ou CPU, le dossier de travail, l'astuce /help.</summary>
public static class WelcomeBanner
{
    public static void Show(CommandContext context)
    {
        var grid = new Grid().AddColumn(new GridColumn().PadRight(2)).AddColumn();
        grid.AddRow(Label("Modèle"), new Markup(ModelText(context)));
        grid.AddRow(Label("Calcul"), new Markup(context.OnGpu
            ? $"[{Theme.Success}]GPU NVIDIA (CUDA)[/]"
            : $"CPU [{Theme.Dim}](pas de GPU NVIDIA utilisable)[/]"));
        grid.AddRow(Label("Dossier"), new Text(context.WorkingDirectory, Theme.Style(Theme.Dim)));

        var content = new Rows(
            new Markup($"[{Theme.Accent}]{Theme.Logo}[/] Bienvenue dans [bold {Theme.Accent}]Nel Code[/] !"),
            new Text(""),
            grid,
            new Text(""),
            new Markup($"[{Theme.Dim}]Astuce :[/] [{Theme.Command}]/help[/] [{Theme.Dim}]pour les commandes, " +
                       "ou écris directement pour discuter.[/]"));

        AnsiConsole.Write(new Panel(new Padder(content, new Padding(1, 0)))
            .RoundedBorder()
            .BorderStyle(Theme.Style(Theme.Accent)));
        AnsiConsole.WriteLine();
    }

    private static string ModelText(CommandContext context)
    {
        if (context.Chat is not { } chat)
            return $"[{Theme.Warning}]aucun modèle[/] [{Theme.Dim}]— tape[/] [{Theme.Command}]/import <fichier.nelmodel>[/] " +
                   $"[{Theme.Dim}]pour installer un modèle MiniChat[/]";

        var model = chat.Generator.Model;
        return $"[bold]{Markup.Escape(context.ModelName!)}[/] [{Theme.Dim}]· {model.ParameterCount / 1e6:0.0} M paramètres · " +
               $"{Markup.Escape(ModelsCommand.Describe(model.Config))}[/]";
    }

    private static Markup Label(string text) => new($"[{Theme.Dim}]{text}[/]");
}
