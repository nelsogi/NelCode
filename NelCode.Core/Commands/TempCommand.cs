using NelCode.Storage;

namespace NelCode.Commands;

public sealed class TempCommand : ICommand
{
    public string Name => "temp";
    public string Description => "Température : plus bas = réponses sages, plus haut = créatives (0.1 à 2)";
    public string Usage => "/temp [0.8]";

    public void Execute(CommandContext context, string[] args)
    {
        if (args.Length > 0)
        {
            context.Settings.Temperature = Settings.ClampTemperature(CommandContext.ParseNumber(args[0], Usage));
            context.Settings.Save();
        }
        context.Output.Dim($"(température = {context.Settings.Temperature})");
    }
}
