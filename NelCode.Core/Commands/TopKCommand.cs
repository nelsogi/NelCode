using NelCode.Storage;

namespace NelCode.Commands;

public sealed class TopKCommand : ICommand
{
    public string Name => "topk";
    public string Description => "Tirage parmi les K tokens les plus probables (0 = pas de limite)";
    public string Usage => "/topk [20]";

    public void Execute(CommandContext context, string[] args)
    {
        if (args.Length > 0)
        {
            context.Settings.TopK = Settings.ClampTopK((int)CommandContext.ParseNumber(args[0], Usage));
            context.Settings.Save();
        }
        context.Output.Dim($"(top-k = {(context.Settings.TopK == 0 ? "désactivé" : context.Settings.TopK)})");
    }
}
