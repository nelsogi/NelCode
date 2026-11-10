namespace NelCode.Commands;

/// <summary>
/// Une commande (/help, /model...). Chaque classe qui implémente ICommand est trouvée toute seule par
/// CommandRegistry : pour ajouter une commande, il suffit d'ajouter sa classe.
/// </summary>
public interface ICommand
{
    /// <summary>Le nom tapé après "/", ex. "model".</summary>
    string Name { get; }

    /// <summary>Une ligne pour /help et les suggestions.</summary>
    string Description { get; }

    /// <summary>Comment l'écrire, ex. "/import &lt;dossier&gt; [nom]".</summary>
    string Usage { get; }

    /// <param name="args">Les mots tapés après le nom ("entre guillemets" = un seul mot).</param>
    void Execute(CommandContext context, string[] args);
}
