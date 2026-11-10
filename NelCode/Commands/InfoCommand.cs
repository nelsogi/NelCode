using NelCode.Engine;
using NelCode.Storage;
using NelCode.UI;
using Spectre.Console;

namespace NelCode.Commands;

public sealed class InfoCommand : ICommand
{
    public string Name => "info";
    public string Description => "Modèle actif, GPU ou CPU, réglages, conversation, dossiers";
    public string Usage => "/info";

    public void Execute(CommandContext context, string[] args)
    {
        var grid = new Grid().AddColumn(new GridColumn().PadRight(2)).AddColumn();

        if (context.Chat is { } chat)
        {
            var model = chat.Generator.Model;
            grid.AddRow(Label("Modèle"), new Text(
                $"{context.ModelName} · {model.ParameterCount:N0} paramètres · vocabulaire {model.Config.VocabSize} tokens"));
            grid.AddRow(Label("Forme"), new Text(ModelsCommand.Describe(model.Config)));
            if (chat.Generator.LastStats is { } stats)
                grid.AddRow(Label("Vitesse"), new Text(
                    $"{stats.TokensPerSecond:0} tokens/s à la dernière réponse ({stats.Tokens} tokens, prompt de {stats.PromptTokens})"));
        }
        else
        {
            grid.AddRow(Label("Modèle"), new Text("aucun (/import <fichier.nelmodel>)", Theme.Style(Theme.Warning)));
        }

        grid.AddRow(Label("Calcul"), new Text(context.OnGpu ? "GPU NVIDIA (CUDA)" : "CPU"));
        var s = context.Settings;
        grid.AddRow(Label("Génération"), new Text(
            $"température {s.Temperature} · top-k {(s.TopK == 0 ? "désactivé" : s.TopK)} · " +
            $"top-p {(s.TopP >= 1 ? "désactivé" : s.TopP)} · réponse max {ChatEngine.MaxReplyTokens} tokens"));
        string inPrompt = context.Chat is { } c && context.Session.Messages.Count > 0
            ? $", dont {c.TurnsInLastPrompt} dans le dernier prompt"
            : "";
        grid.AddRow(Label("Conversation"), new Text($"{context.Session.Turns.Count} échange(s){inPrompt}"));
        grid.AddRow(Label("Dossier"), new Text(context.WorkingDirectory));
        grid.AddRow(Label("Données"), new Text(AppPaths.Root, Theme.Style(Theme.Dim)));

        AnsiConsole.Write(grid);
    }

    private static Markup Label(string text) => new($"[{Theme.Dim}]{text}[/]");
}
