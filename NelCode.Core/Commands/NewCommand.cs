
namespace NelCode.Commands;

public sealed class NewCommand : ICommand
{
    public string Name => "new";
    public string Description => "Commence une nouvelle conversation (l'ancienne reste dans /resume)";
    public string Usage => "/new";

    public void Execute(CommandContext context, string[] args)
    {
        context.NewSession();
        context.Output.Success("Nouvelle conversation.");
    }
}
