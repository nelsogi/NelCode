using NelCode.Storage;
using NelCode.UI;

namespace NelCode.Commands;

public sealed class DeleteCommand : ICommand
{
    public string Name => "delete";
    public string Description => "Supprime un modèle installé (après confirmation)";
    public string Usage => "/delete <nom>";

    public void Execute(CommandContext context, string[] args)
    {
        if (args.Length == 0)
            throw new UserException($"Utilisation : {Usage}");

        string name = string.Join(' ', args);
        var entry = ModelLibrary.List().FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    ?? throw new UserException($"Modèle inconnu : « {name} ». /models pour la liste.");

        bool active = entry.Name == context.ModelName;
        string question = $"Supprimer définitivement « {entry.Name} » ({entry.Bytes / 1e6:0} Mo) ?" +
                          (active ? " C'est le modèle actif." : "");
        if (!Prompts.Confirm(question))
        {
            Messages.Dim("Suppression annulée.");
            return;
        }

        if (active)
            context.UnloadModel();   // libère les poids avant d'effacer leurs fichiers
        ModelLibrary.Delete(entry.Name);

        Messages.Success($"Modèle « {entry.Name} » supprimé.");
        if (active)
            Messages.Dim("Plus de modèle actif : /model <nom> pour en choisir un, /import pour en installer un.");
    }
}
