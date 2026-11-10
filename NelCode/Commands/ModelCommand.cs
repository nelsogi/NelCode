using NelCode.UI;

namespace NelCode.Commands;

public sealed class ModelCommand : ICommand
{
    public string Name => "model";
    public string Description => "Change de modèle (retenu pour le prochain lancement), nouvelle conversation";
    public string Usage => "/model <nom>";

    public void Execute(CommandContext context, string[] args)
    {
        if (args.Length == 0)
        {
            Messages.Dim(context.ModelName is { } current ? $"(modèle actif : {current})" : "(aucun modèle actif)");
            return;
        }

        string name = string.Join(' ', args);
        Messages.Spin($"Chargement de « {name} »...", () => { context.LoadModel(name); return 0; });
        context.NewSession();
        Messages.Success($"Modèle actif : {name}. Nouvelle conversation.");
    }
}
