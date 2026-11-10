
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
            context.Output.Dim(context.ModelName is { } current ? $"(modèle actif : {current})" : "(aucun modèle actif)");
            return;
        }

        string name = string.Join(' ', args);
        context.Output.Busy($"Chargement de « {name} »...", () => context.LoadModel(name));
        context.NewSession();
        context.Output.Success($"Modèle actif : {name}. Nouvelle conversation.");
    }
}
