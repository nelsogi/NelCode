using NelCode.Commands;
using NelCode.Storage;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace NelCode.UI;

/// <summary>IOutput dans la console, avec Spectre.Console : le rendu de Nel Code console d'avant IOutput.</summary>
public sealed class ConsoleOutput : IOutput
{
    public void Line(Tone tone, string text)
    {
        if (text.Length == 0)
            AnsiConsole.WriteLine();
        else
            AnsiConsole.MarkupLine(Theme.Markup(new Styled(text, tone)));
    }

    /// <summary>Toutes les colonnes sans retour à la ligne, sauf la dernière (description, forme...).</summary>
    public void Table(IReadOnlyList<Column> columns, IEnumerable<Styled[]> rows)
    {
        var table = new Table().Border(TableBorder.Simple).BorderStyle(Theme.Style(Theme.Border));
        for (int i = 0; i < columns.Count; i++)
        {
            var column = new TableColumn($"[{Theme.Dim}]{Markup.Escape(columns[i].Title)}[/]");
            if (i < columns.Count - 1) column.NoWrap();
            if (columns[i].AlignRight) column.RightAligned();
            table.AddColumn(column);
        }
        foreach (var row in rows)
            table.AddRow(row.Select(Text).ToArray<IRenderable>());
        AnsiConsole.Write(table);
    }

    public void Details(string? title, IEnumerable<(string Label, Styled Value)> rows)
    {
        var grid = new Grid().AddColumn(new GridColumn().PadRight(2)).AddColumn();
        foreach (var (label, value) in rows)
            grid.AddRow(new Markup($"[{Theme.Dim}]{Markup.Escape(label)}[/]"), Text(value));

        if (title is null)
            AnsiConsole.Write(grid);
        else
            AnsiConsole.Write(new Panel(grid)
                .Header($"[{Theme.Accent}] {Markup.Escape(title)} [/]")
                .RoundedBorder()
                .BorderStyle(Theme.Style(Theme.Border)));
    }

    public void Message(ChatMessage message) => ChatView.ShowMessage(message);

    public void Clear(CommandContext context)
    {
        AnsiConsole.Clear();
        WelcomeBanner.Show(context);
    }

    public T Busy<T>(string status, Func<T> work) => Messages.Spin(status, work);

    public bool Confirm(string question) => Prompts.Confirm(question);

    public int Choose(string question, IReadOnlyList<Styled[]> choices) => Prompts.Choose(question, choices);

    public string Ask(string question) => Prompts.Ask(question);

    private static Text Text(Styled styled) => new(styled.Text, Theme.Style(Theme.Color(styled.Tone)));
}
