using NelCode.Storage;

namespace NelCode.Commands;

/// <summary>Le rôle d'un texte. Chaque interface le traduit en couleur (son Theme).</summary>
public enum Tone { Text, Dim, Accent, Command, User, Success, Warning, Error }

/// <summary>Un morceau de texte et son rôle.</summary>
public sealed record Styled(string Text, Tone Tone = Tone.Text);

/// <summary>Une colonne de tableau : son titre, alignée à droite ou non (les nombres).</summary>
public sealed record Column(string Title, bool AlignRight = false);

/// <summary>
/// Tout ce que les commandes affichent ou demandent passe par ici : elles ne savent pas si elles tournent
/// dans la console (Spectre.Console) ou dans la fenêtre (WinForms). Chaque interface l'implémente.
/// Les méthodes peuvent être appelées depuis n'importe quel fil.
/// </summary>
public interface IOutput
{
    /// <summary>Une ligne de texte ("" = ligne vide).</summary>
    void Line(Tone tone, string text);

    /// <summary>Un tableau : une ligne = une cellule par colonne.</summary>
    void Table(IReadOnlyList<Column> columns, IEnumerable<Styled[]> rows);

    /// <summary>Des paires libellé / valeur, avec un titre (encadré) ou sans.</summary>
    void Details(string? title, IEnumerable<(string Label, Styled Value)> rows);

    /// <summary>Un message de conversation (reprise d'une conversation).</summary>
    void Message(ChatMessage message);

    /// <summary>Efface la conversation à l'écran et réaffiche l'accueil.</summary>
    void Clear(CommandContext context);

    /// <summary>Un indicateur d'attente pendant un travail long.</summary>
    T Busy<T>(string status, Func<T> work);

    /// <summary>Oui / non, non par défaut.</summary>
    bool Confirm(string question);

    /// <summary>Le numéro du choix (0 = le premier), -1 si annulé ou invalide.</summary>
    int Choose(string question, IReadOnlyList<Styled[]> choices);

    /// <summary>Un texte, "" si rien n'est tapé.</summary>
    string Ask(string question);
}

/// <summary>Les messages courants, avec leur symbole : le même dans toutes les interfaces.</summary>
public static class OutputExtensions
{
    public static void Dim(this IOutput output, string text) => output.Line(Tone.Dim, text);
    public static void Success(this IOutput output, string text) => output.Line(Tone.Success, $"✓ {text}");
    public static void Warning(this IOutput output, string text) => output.Line(Tone.Warning, $"! {text}");
    public static void Error(this IOutput output, string text) => output.Line(Tone.Error, $"✗ {text}");

    public static void Busy(this IOutput output, string status, Action work) =>
        output.Busy(status, () => { work(); return 0; });

    public static int Choose(this IOutput output, string question, params string[] choices) =>
        output.Choose(question, choices.Select(c => new[] { new Styled(c) }).ToList());
}
