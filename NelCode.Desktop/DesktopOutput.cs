using NelCode.Commands;
using NelCode.Desktop.Controls;
using StatusBar = NelCode.Desktop.Controls.StatusBar;   // pas celle, obsolète, de WinForms
using NelCode.Storage;

namespace NelCode.Desktop;

/// <summary>
/// IOutput dans la fenêtre. Les commandes tournent en tâche de fond : chaque appel est passé au fil de
/// l'interface (Invoke), et attend sa fin, comme une écriture dans la console.
/// currentSession : la conversation en cours, pour savoir où sont les messages réaffichés (Modifier, Régénérer).
/// </summary>
internal sealed class DesktopOutput(Form owner, ChatView chat, StatusBar statusBar, Func<Session?> currentSession) : IOutput
{
    public void Line(Tone tone, string text) => Ui(() => chat.WriteLine(text, Theme.Of(tone)));

    /// <summary>Colonnes alignées en police fixe, titres en gris, comme le tableau de la console.</summary>
    public void Table(IReadOnlyList<Column> columns, IEnumerable<Styled[]> rows)
    {
        var all = rows.ToList();
        int[] widths = columns.Select((c, i) => all.Select(r => r[i].Text.Length).Append(c.Title.Length).Max()).ToArray();
        string Cell(string text, int i) =>
            i == columns.Count - 1 ? text : columns[i].AlignRight ? text.PadLeft(widths[i]) : text.PadRight(widths[i]);

        Ui(() =>
        {
            chat.WriteLine(string.Join("  ", columns.Select((c, i) => Cell(c.Title, i))), Theme.Dim);
            chat.WriteLine(string.Join("  ", widths.Select(w => new string('─', w))), Theme.Border);
            foreach (var row in all)
            {
                for (int i = 0; i < row.Length; i++)
                    chat.Write((i > 0 ? "  " : "") + Cell(row[i].Text, i), Theme.Of(row[i].Tone));
                chat.Write("\n", Theme.Text);
            }
        });
    }

    public void Details(string? title, IEnumerable<(string Label, Styled Value)> rows)
    {
        var all = rows.ToList();
        int width = all.Select(r => r.Label.Length).DefaultIfEmpty(0).Max() + 2;
        Ui(() =>
        {
            if (title is not null)
                chat.WriteLine($"── {title} ──", Theme.Accent);
            foreach (var (label, value) in all)
            {
                chat.Write(label.PadRight(width), Theme.Dim);
                chat.WriteLine(value.Text, Theme.Of(value.Tone));
            }
        });
    }

    public void Message(ChatMessage message) => Ui(() =>
    {
        var control = message.Role == ChatMessage.UserRole
            ? chat.User(message.Content)
            : chat.Reply(message.Content, message.Versions);
        if (currentSession() is { } session && session.Messages.FindIndex(m => ReferenceEquals(m, message)) is >= 0 and var index)
            control.SetOrigin(session, index);
    });

    public void Clear(CommandContext context) => Ui(() =>
    {
        chat.Clear();
        chat.Welcome();
    });

    /// <summary>L'indicateur de la barre d'état tourne pendant le travail, qui reste sur le fil appelant.</summary>
    public T Busy<T>(string status, Func<T> work)
    {
        Ui(() => statusBar.SetBusy(status));
        try
        {
            return work();
        }
        finally
        {
            Ui(() => statusBar.SetBusy(null));
        }
    }

    public bool Confirm(string question) => Ui(() => PromptDialog.Confirm(owner, question));

    public int Choose(string question, IReadOnlyList<Styled[]> choices) =>
        Ui(() => PromptDialog.Choose(owner, question, choices.Select(c => string.Concat(c.Select(s => s.Text))).ToList()));

    public string Ask(string question) => Ui(() => PromptDialog.Ask(owner, question));

    private void Ui(Action action)
    {
        if (chat.InvokeRequired) chat.Invoke(action);
        else action();
    }

    private T Ui<T>(Func<T> function) => chat.InvokeRequired ? chat.Invoke(function) : function();
}
