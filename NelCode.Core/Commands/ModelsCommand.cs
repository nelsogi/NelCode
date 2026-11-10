using NelCode.Engine;
using NelCode.Storage;

namespace NelCode.Commands;

public sealed class ModelsCommand : ICommand
{
    public string Name => "models";
    public string Description => "Liste les modèles installés : nom, taille, date";
    public string Usage => "/models";

    public void Execute(CommandContext context, string[] args)
    {
        var models = ModelLibrary.List();
        if (models.Count == 0)
        {
            context.Output.Warning($"Aucun modèle installé dans {AppPaths.Models}. /import <fichier.nelmodel> pour en ajouter un.");
            return;
        }

        context.Output.Table([new("Nom"), new("Taille", AlignRight: true), new("Date"), new("Forme")],
            models.Select(model => new Styled[]
            {
                model.Name == context.ModelName ? new($"● {model.Name}", Tone.Accent) : new($"  {model.Name}", Tone.Command),
                new($"{model.Bytes / 1e6:0} Mo"),
                new(model.Date.ToString("dd'/'MM'/'yy HH:mm")),
                new(model.Config is { } c ? Describe(c) : "config.json illisible", Tone.Dim),
            }));
        context.Output.Dim("● = modèle actif. /model <nom> pour changer.");
    }

    public static string Describe(ModelConfig config) =>
        $"{config.Layers} couches · {config.Heads} têtes · {config.EmbedDim} dim · contexte {config.BlockSize} · " +
        (config.Architecture == ModelArchitecture.Modern ? "RoPE/SwiGLU" : "classique");
}
