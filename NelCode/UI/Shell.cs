using NelCode.Commands;
using Spectre.Console;

namespace NelCode.UI;

/// <summary>
/// La boucle de Nel Code : le cadre d'accueil, puis chaque ligne tapée est soit une commande ("/models"),
/// soit un message pour le modèle. Une erreur est affichée et on continue : la console ne se ferme pas.
/// </summary>
public sealed class Shell
{
    private readonly CommandContext _context;
    private readonly InputBox _input;

    public Shell(CommandContext context)
    {
        _context = context;
        _input = new InputBox(context.Registry.Commands);
    }

    public void Run()
    {
        WelcomeBanner.Show(_context);

        while (!_context.QuitRequested)
        {
            string? input = _input.Read()?.Trim();
            if (input is null) break;
            if (input.Length == 0) continue;

            ChatView.ShowUser(input);
            Execute(input);
        }
    }

    private void Execute(string input)
    {
        // Échap pendant la réponse : son token est annulé, elle s'arrête proprement.
        // Pas pendant une commande : /resume lit lui-même les touches (liste de choix).
        try
        {
            if (input.StartsWith('/'))
            {
                _context.Registry.Execute(_context, input);
                AnsiConsole.WriteLine();
            }
            else
            {
                var chat = _context.RequireChat();
                using var escape = EscapeKey.Watch();
                string reply = ChatView.Reply(chat, _context.Session.Turns, input, _context.Settings.Sampling, escape.Token);
                if (reply.Trim().Length > 0)
                    _context.RecordTurn(input, reply);
            }
        }
        catch (UserException e)
        {
            Messages.Error(e.Message);
            AnsiConsole.WriteLine();
        }
        catch (Exception e)
        {
            Messages.Error($"{e.GetType().Name} : {e.Message}");
            AnsiConsole.WriteLine();
        }
    }
}
