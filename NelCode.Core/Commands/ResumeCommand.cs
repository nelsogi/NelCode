using NelCode.Storage;

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
            context.Output.Dim("Aucune autre conversation sauvegardée.");
            return;
        }
        int index = context.Output.Choose("Quelle conversation reprendre ? (↑↓ puis Entrée)", sessions.Select(Label).ToList());
        if (index < 0)
        {
            context.Output.Dim("Reprise annulée.");
            return;
        }
        Resume(context, sessions[index]);
    }

    /// <summary>Reprend la conversation et la réaffiche (aussi utilisé par la liste des conversations de Desktop).</summary>
    public static void Resume(CommandContext context, Session chosen)
    {
        string wantedModel = chosen.Model;
        context.Output.Busy("Reprise...", () => context.ResumeSession(chosen));
        if (wantedModel != context.ModelName)
            context.Output.Warning($"Le modèle « {wantedModel} » n'est plus installé : la conversation continue avec « {context.ModelName} ».");

        context.Output.Line(Tone.Text, "");
        foreach (var message in chosen.Messages)
            context.Output.Message(message);
        context.Output.Success($"Conversation reprise ({chosen.Turns.Count} échange(s)).");
    }

    // "10/11 14:03  v1  Bonjour, qui es-tu ?  (3 échange(s) · C:\Projets)"
    private static Styled[] Label(Session s)
    {
        string first = s.Messages[0].Content.ReplaceLineEndings(" ");
        if (first.Length > 50) first = first[..49] + "…";
        return
        [
            new($"{s.Date:dd'/'MM HH:mm}  "),
            new(s.Model, Tone.Command),
            new($"  {first}  "),
            new($"({s.Messages.Count / 2} échange(s) · {s.WorkingDirectory})", Tone.Dim),
        ];
    }
}
