using NelCode.Engine;
using NelCode.Storage;

namespace NelCode.Commands;

public sealed class InfoCommand : ICommand
{
    public string Name => "info";
    public string Description => "Modèle actif, GPU ou CPU, réglages, conversation, dossiers";
    public string Usage => "/info";

    public void Execute(CommandContext context, string[] args)
    {
        var grid = new List<(string, Styled)>();

        if (context.Chat is { } chat)
        {
            var model = chat.Generator.Model;
            grid.Add(("Modèle", new Styled(
                $"{context.ModelName} · {model.ParameterCount:N0} paramètres · vocabulaire {model.Config.VocabSize} tokens")));
            grid.Add(("Forme", new Styled(ModelsCommand.Describe(model.Config))));
            if (chat.Generator.LastStats is { } stats)
                grid.Add(("Vitesse", new Styled(
                    $"{stats.TokensPerSecond:0} tokens/s à la dernière réponse ({stats.Tokens} tokens, prompt de {stats.PromptTokens})")));
        }
        else
        {
            grid.Add(("Modèle", new Styled("aucun (/import <fichier.nelmodel>)", Tone.Warning)));
        }

        grid.Add(("Calcul", new Styled(context.OnGpu ? "GPU NVIDIA (CUDA)" : "CPU")));
        var s = context.Settings;
        grid.Add(("Génération", new Styled(
            $"température {s.Temperature} · top-k {(s.TopK == 0 ? "désactivé" : s.TopK)} · " +
            $"top-p {(s.TopP >= 1 ? "désactivé" : s.TopP)} · réponse max {ChatEngine.MaxReplyTokens} tokens")));
        string inPrompt = context.Chat is { } c && context.Session.Messages.Count > 0
            ? $", dont {c.TurnsInLastPrompt} dans le dernier prompt"
            : "";
        grid.Add(("Conversation", new Styled($"{context.Session.Turns.Count} échange(s){inPrompt}")));
        grid.Add(("Dossier", new Styled(context.WorkingDirectory)));
        grid.Add(("Données", new Styled(AppPaths.Root, Tone.Dim)));

        context.Output.Details(null, grid);
    }
}
