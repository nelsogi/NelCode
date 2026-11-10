using NelCode.Storage;
using NelCode.UI;
using Spectre.Console;

namespace NelCode.Commands;

public sealed class ResumeCommand : ICommand
{
    public string Name => "resume";
    public string Description => "Reprend une conversation sauvegardée";
    public string Usage => "/resume";

    private const int MaxListed = 20;

    public void Execute(CommandContext context, string[] args)
    {
        var sessions = SessionStore.List()
            .Where(s => s.Messages.Count > 0 && s.Id != context.Session.Id)
            .Take(MaxListed)
            .ToList();
        if (sessions.Count == 0)
        {
            Messages.Dim("Aucune autre conversation sauvegardée.");
            return;
        }
        if (Console.IsInputRedirected)
            throw new UserException("/resume a besoin d'une console interactive pour choisir.");

        var chosen = AnsiConsole.Prompt(new SelectionPrompt<Session>()
            .Title($"[{Theme.Dim}]Quelle conversation reprendre ? (↑↓ puis Entrée)[/]")
            .PageSize(10)
            .HighlightStyle(Theme.Style(Theme.Accent))
            .UseConverter(Label)
            .AddChoices(sessions));

        string wantedModel = chosen.Model;
        Messages.Spin("Reprise...", () => { context.ResumeSession(chosen); return 0; });
        if (wantedModel != context.ModelName)
            Messages.Warning($"Le modèle « {wantedModel} » n'est plus installé : la conversation continue avec « {context.ModelName} ».");

        AnsiConsole.WriteLine();
        foreach (var message in chosen.Messages)
            ChatView.ShowMessage(message);
        Messages.Success($"Conversation reprise ({chosen.Turns.Count} échange(s)).");
    }

    // "10/11 14:03  v1  Bonjour, qui es-tu ?  (3 échange(s) · C:\Projets)"
    private static string Label(Session s)
    {
        string first = s.Messages[0].Content.ReplaceLineEndings(" ");
        if (first.Length > 50) first = first[..49] + "…";
        return $"{s.Date:dd'/'MM HH:mm}  [{Theme.Command}]{Markup.Escape(s.Model)}[/]  {Markup.Escape(first)}  " +
               $"[{Theme.Dim}]({s.Messages.Count / 2} échange(s) · {Markup.Escape(s.WorkingDirectory)})[/]";
    }
}
