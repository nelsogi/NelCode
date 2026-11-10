using NelCode.UI;
using Spectre.Console;

namespace NelCode.Commands;

public sealed class HelpCommand : ICommand
{
    public string Name => "help";
    public string Description => "Affiche la liste des commandes";
    public string Usage => "/help";

    public void Execute(CommandContext context, string[] args)
    {
        var table = new Table()
            .Border(TableBorder.Simple)
            .BorderStyle(Theme.Style(Theme.Border))
            .AddColumn(new TableColumn($"[{Theme.Dim}]Commande[/]").NoWrap())
            .AddColumn(new TableColumn($"[{Theme.Dim}]Ce qu'elle fait[/]"));

        foreach (var command in context.Registry.Commands)
            table.AddRow(new Text(command.Usage, Theme.Style(Theme.Command)), new Text(command.Description));

        AnsiConsole.Write(table);
        Messages.Dim("Écris un message sans « / » pour discuter. Échap arrête une réponse en cours.");
    }
}
