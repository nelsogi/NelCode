using NelCode.Commands;
using Spectre.Console;

namespace NelCode.UI;

/// <summary>
/// Les couleurs et les symboles de Nel Code, à un seul endroit. Les constantes sont des noms de couleur
/// Spectre ("grey50", "#D97757"...) : utilisables dans le markup ("[grey50]texte[/]") ou via Style().
/// </summary>
public static class Theme
{
    public const string Accent = "#D97757";        // le nom, le cadre d'accueil, le "●" des réponses
    public const string Text = "default";
    public const string User = "grey70";           // les messages de l'utilisateur dans la conversation
    public const string Dim = "grey50";            // astuces, détails, messages secondaires
    public const string Border = "grey35";         // cadres et lignes
    public const string Command = "#E0A87E";       // les noms de commandes (/help, /model...)
    public const string Success = "green";
    public const string Warning = "yellow";
    public const string Error = "red";

    public const string Logo = "✻";
    public const string Prompt = ">";
    public const string ReplyBullet = "●";

    public static Style Style(string color) => Spectre.Console.Style.Parse(color);

    /// <summary>La couleur d'un rôle de texte des commandes (IOutput).</summary>
    public static string Color(Tone tone) => tone switch
    {
        Tone.Dim => Dim,
        Tone.Accent => Accent,
        Tone.Command => Command,
        Tone.User => User,
        Tone.Success => Success,
        Tone.Warning => Warning,
        Tone.Error => Error,
        _ => Text,
    };

    /// <summary>Le texte en markup Spectre, échappé : ses "[" et "]" restent du texte.</summary>
    public static string Markup(Styled styled) =>
        styled.Tone == Tone.Text
            ? Spectre.Console.Markup.Escape(styled.Text)
            : $"[{Color(styled.Tone)}]{Spectre.Console.Markup.Escape(styled.Text)}[/]";
}
