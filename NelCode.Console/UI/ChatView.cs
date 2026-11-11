using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using NelCode.Commands;
using NelCode.Storage;
using Spectre.Console;

namespace NelCode.UI;

/// <summary>
/// La conversation à l'écran :
///   > Bonjour !                    <- le message de l'utilisateur
///   ● Bonjour, comment vas-tu ?    <- la réponse, écrite au fur et à mesure
/// </summary>
public static class ChatView
{
    public static void ShowUser(string message) =>
        AnsiConsole.MarkupLine($"[{Theme.Dim}]{Theme.Prompt}[/] [{Theme.User}]{Markup.Escape(message)}[/]");

    public static void ShowReply(string reply)
    {
        AnsiConsole.Markup($"[{Theme.Accent}]{Theme.ReplyBullet}[/] ");
        AnsiConsole.Write(new Text(reply, Theme.Style(Theme.Text)));
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
    }

    /// <summary>Un message d'une conversation reprise (/resume).</summary>
    public static void ShowMessage(ChatMessage message)
    {
        if (message.Role == ChatMessage.UserRole)
            ShowUser(message.Content);
        else
            ShowReply(message.Content);
    }

    /// <summary>
    /// Demande la réponse au modèle et l'affiche caractère par caractère. Un spinner tourne jusqu'au premier
    /// caractère. cancellation (Échap) : la réponse s'arrête là, "(interrompu)" est ajouté.
    /// </summary>
    /// <returns>La réponse écrite (éventuellement coupée), déjà ajoutée à la conversation si elle n'est pas vide.</returns>
    public static string Reply(CommandContext context, string message, CancellationToken cancellation)
    {
        // La génération tourne à part et pousse ses caractères ici ; l'affichage les reprend dans l'ordre.
        using var characters = new BlockingCollection<char>();
        // L'erreur est attrapée dans la tâche même : si elle en sortait, le débogueur de Visual Studio
        // s'arrêterait dessus ("non gérée par le code utilisateur"). Elle est relancée plus bas.
        var generation = Task.Run(() =>
        {
            try { return (Reply: context.SendMessage(message, characters.Add, cancellation), Error: (Exception?)null); }
            catch (Exception e) { return (Reply: "", Error: e); }
            finally { characters.CompleteAdding(); }
        });

        char first = default;
        bool any = false;
        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Theme.Style(Theme.Accent))
            .Start($"[{Theme.Dim}]Réflexion... (Échap pour arrêter)[/]",
                _ => any = characters.TryTake(out first, Timeout.Infinite));

        AnsiConsole.Markup($"[{Theme.Accent}]{Theme.ReplyBullet}[/] ");
        if (any)
        {
            Write(first);
            foreach (char c in characters.GetConsumingEnumerable())
                Write(c);
        }
        else if (!cancellation.IsCancellationRequested)
        {
            AnsiConsole.Markup($"[{Theme.Dim}](pas de réponse)[/]");
        }
        if (cancellation.IsCancellationRequested)
            AnsiConsole.Markup($"[{Theme.Dim}] (interrompu)[/]");
        AnsiConsole.WriteLine();

        var (reply, error) = generation.GetAwaiter().GetResult();
        if (error is not null)
            ExceptionDispatchInfo.Throw(error);   // une erreur pendant la génération remonte ici, vers Shell

        if (context.Chat?.Generator.LastStats is { Tokens: > 0 } stats)
            AnsiConsole.MarkupLine($"[{Theme.Dim}]  {stats.Tokens} token{(stats.Tokens > 1 ? "s" : "")} · {stats.TokensPerSecond:0} tokens/s[/]");
        AnsiConsole.WriteLine();
        return reply;
    }

    private static void Write(char c) => AnsiConsole.Write(new Text(c.ToString(), Theme.Style(Theme.Text)));
}
