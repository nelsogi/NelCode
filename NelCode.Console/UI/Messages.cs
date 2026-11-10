using Spectre.Console;

namespace NelCode.UI;

/// <summary>
/// Les messages de Nel Code, chacun dans sa couleur du Theme. Le texte est affiché tel quel :
/// les "[" et "]" d'un chemin ou d'une réponse du modèle ne sont jamais lus comme du markup Spectre.
/// </summary>
public static class Messages
{
    public static void Dim(string text) => Line(Theme.Dim, text);

    public static void Success(string text) => Line(Theme.Success, $"✓ {text}");

    public static void Warning(string text) => Line(Theme.Warning, $"! {text}");

    public static void Error(string text) => Line(Theme.Error, $"✗ {text}");

    /// <summary>Un spinner pendant un travail long.</summary>
    public static T Spin<T>(string status, Func<T> work) =>
        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Theme.Style(Theme.Accent))
            .Start(Markup.Escape(status), _ => work());

    private static void Line(string color, string text) =>
        AnsiConsole.MarkupLine($"[{color}]{Markup.Escape(text)}[/]");
}
