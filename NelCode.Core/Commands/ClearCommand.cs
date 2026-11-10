
namespace NelCode.Commands;

public sealed class ClearCommand : ICommand
{
    public string Name => "clear";
    public string Description => "Efface l'écran (la conversation continue ; /new pour en commencer une autre)";
    public string Usage => "/clear";

    public void Execute(CommandContext context, string[] args)
    {
        context.Output.Clear(context);
    }
}
