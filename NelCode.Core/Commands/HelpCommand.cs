
namespace NelCode.Commands;

public sealed class HelpCommand : ICommand
{
    public string Name => "help";
    public string Description => "Affiche la liste des commandes";
    public string Usage => "/help";

    public void Execute(CommandContext context, string[] args)
    {
        context.Output.Table([new("Commande"), new("Ce qu'elle fait")],
            context.Registry.Commands.Select(c => new Styled[] { new(c.Usage, Tone.Command), new(c.Description) }));
        context.Output.Dim("Écris un message sans « / » pour discuter. Échap arrête une réponse en cours.");
    }
}
