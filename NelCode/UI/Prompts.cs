using Spectre.Console;

namespace NelCode.UI;

/// <summary>
/// Les questions posées pendant une commande (confirmer, choisir, taper un nom).
/// Console interactive : les prompts de Spectre. Entrée redirigée (script) : la réponse est lue sur la
/// ligne suivante, comme les messages (InputBox).
/// </summary>
public static class Prompts
{
    /// <summary>Oui / non, non par défaut.</summary>
    public static bool Confirm(string question)
    {
        if (Console.IsInputRedirected)
        {
            string answer = ReadPlain($"{question} (o/N)").ToLowerInvariant();
            return answer is "o" or "oui" or "y" or "yes";
        }

        return AnsiConsole.Prompt(new ConfirmationPrompt(Markup.Escape(question))
        {
            DefaultValue = false,
            Yes = 'o',
            No = 'n',
            InvalidChoiceMessage = $"[{Theme.Error}]Réponds o ou n.[/]",
        });
    }

    /// <summary>Le numéro du choix (0 = le premier). Entrée redirigée : le numéro tapé, -1 s'il n'est pas valide.</summary>
    public static int Choose(string question, params string[] choices)
    {
        if (Console.IsInputRedirected)
        {
            AnsiConsole.MarkupLine(Markup.Escape(question));
            for (int i = 0; i < choices.Length; i++)
                AnsiConsole.MarkupLine($"  [{Theme.Command}]{i + 1}[/]  {Markup.Escape(choices[i])}");
            return int.TryParse(ReadPlain("Choix"), out int n) && n >= 1 && n <= choices.Length ? n - 1 : -1;
        }

        return AnsiConsole.Prompt(new SelectionPrompt<int>()
            .Title(Markup.Escape(question))
            .HighlightStyle(Theme.Style(Theme.Accent))
            .UseConverter(i => Markup.Escape(choices[i]))
            .AddChoices(Enumerable.Range(0, choices.Length)));
    }

    /// <summary>Un texte, "" si rien n'est tapé.</summary>
    public static string Ask(string question) =>
        Console.IsInputRedirected
            ? ReadPlain(question)
            : AnsiConsole.Prompt(new TextPrompt<string>(Markup.Escape(question)).AllowEmpty()).Trim();

    private static string ReadPlain(string question)
    {
        AnsiConsole.MarkupLine($"[{Theme.Dim}]{Markup.Escape(question)}[/]");
        string answer = (Console.ReadLine() ?? "").Trim();
        AnsiConsole.MarkupLine($"[{Theme.Dim}]{Theme.Prompt}[/] {Markup.Escape(answer)}");
        return answer;
    }
}
