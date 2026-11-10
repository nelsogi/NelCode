using NelCode.Storage;

namespace NelCode.Commands;

public sealed class TopPCommand : ICommand
{
    public string Name => "topp";
    public string Description => "Tirage parmi les tokens qui font cette part des chances (0.05 à 1, 1 = pas de limite)";
    public string Usage => "/topp [0.9]";

    public void Execute(CommandContext context, string[] args)
    {
        if (args.Length > 0)
        {
            context.Settings.TopP = Settings.ClampTopP(CommandContext.ParseNumber(args[0], Usage));
            context.Settings.Save();
        }
        context.Output.Dim($"(top-p = {(context.Settings.TopP >= 1 ? "désactivé" : context.Settings.TopP)})");
    }
}
