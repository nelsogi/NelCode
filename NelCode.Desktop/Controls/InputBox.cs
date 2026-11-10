using System.ComponentModel;
using NelCode.Commands;

namespace NelCode.Desktop.Controls;

/// <summary>
/// La zone de saisie, comme celle de la console :
///   › /temp    Température : plus bas = ...       <- commandes qui commencent par ce qui est tapé
///     /topk    Tirage parmi les K ...
///   > /t_
/// Entrée envoie, Maj+Entrée va à la ligne. Liste ouverte : ↑↓ choisissent, Tab complète, Entrée lance la
/// commande choisie, Échap ferme la liste. Liste fermée : ↑↓ parcourent l'historique, Échap vide la ligne.
/// </summary>
internal sealed class InputBox : UserControl
{
    private const int MaxSuggestions = 6;
    private const int TextLines = 3;

    private readonly IReadOnlyList<ICommand> _commands;
    private readonly List<string> _history = new();
    private int _historyIndex;
    private string _draft = "";
    private bool _dismissed;   // liste fermée par Échap, rouverte dès que le texte change

    private readonly ListBox _suggestions = new()
    {
        Dock = DockStyle.Top,
        Visible = false,
        BorderStyle = BorderStyle.None,
        BackColor = Theme.Surface,
        ForeColor = Theme.Text,
        Font = Theme.Font,
        DrawMode = DrawMode.OwnerDrawFixed,
        IntegralHeight = false,
        TabStop = false,
    };
    private readonly TextBox _text = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        AcceptsReturn = true,
        BorderStyle = BorderStyle.None,
        BackColor = Theme.Surface,
        ForeColor = Theme.Text,
        Font = Theme.Font,
        ScrollBars = ScrollBars.None,
    };

    public InputBox(IEnumerable<ICommand> commands)
    {
        _commands = commands.ToList();
        BackColor = Theme.Surface;
        Padding = new Padding(10, 8, 10, 8);

        var prompt = new Label
        {
            Text = Theme.Prompt,
            Dock = DockStyle.Left,
            AutoSize = true,
            ForeColor = Theme.Accent,
            Font = Theme.Bold,
            Padding = new Padding(0, 0, 4, 0),
        };
        var row = new Panel { Dock = DockStyle.Fill };
        row.Controls.Add(_text);
        row.Controls.Add(prompt);
        var gap = new Panel { Dock = DockStyle.Top, Height = 6 };
        var hint = new Label
        {
            Text = "/ commandes · ↑↓ historique · Maj+Entrée nouvelle ligne · Échap arrête une réponse",
            Dock = DockStyle.Bottom,
            AutoSize = false,
            Height = Theme.Small.Height + 4,
            ForeColor = Theme.Dim,
            Font = Theme.Small,
        };

        Controls.Add(row);
        Controls.Add(hint);
        Controls.Add(gap);
        Controls.Add(_suggestions);

        _suggestions.DrawItem += DrawSuggestion;
        _suggestions.MouseClick += (_, _) => { Complete(" "); _text.Focus(); };
        _text.TextChanged += (_, _) => { _dismissed = false; UpdateSuggestions(); };
        _text.PreviewKeyDown += (_, e) => e.IsInputKey |= e.KeyCode == Keys.Tab;
        _text.KeyDown += OnKeyDown;
        UpdateSuggestions();
    }

    /// <summary>Une ligne envoyée (Entrée). La zone est déjà vidée.</summary>
    public event Action<string>? Submitted;

    /// <summary>Pendant une réponse ou une commande : on peut taper, mais Entrée n'envoie pas.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Busy { get; set; }

    public void FocusText() => _text.Focus();

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var matches = Matches();
        bool open = matches.Count > 0;
        switch (e.KeyCode)
        {
            case Keys.Enter when !e.Shift:
                e.SuppressKeyPress = true;
                if (Busy) return;
                if (open) Complete("");
                Submit();
                break;
            case Keys.Tab when open:
                e.SuppressKeyPress = true;
                Complete(" ");
                break;
            case Keys.Tab:
                e.SuppressKeyPress = true;
                break;
            case Keys.Up or Keys.Down when open:
                e.SuppressKeyPress = true;
                int delta = e.KeyCode == Keys.Up ? -1 : 1;
                _suggestions.SelectedIndex = (_suggestions.SelectedIndex + delta + matches.Count) % matches.Count;
                break;
            case Keys.Up or Keys.Down when !_text.Text.Contains('\n'):
                e.SuppressKeyPress = true;
                MoveInHistory(e.KeyCode == Keys.Up ? -1 : 1);
                break;
            case Keys.Escape:
                e.SuppressKeyPress = true;
                if (open)
                {
                    _dismissed = true;
                    UpdateSuggestions();
                }
                else
                {
                    _text.Clear();
                }
                break;
        }
    }

    private void Submit()
    {
        string line = _text.Text;
        if (line.Trim().Length == 0) return;
        if (_history.Count == 0 || _history[^1] != line)
            _history.Add(line);
        _historyIndex = _history.Count;
        _draft = "";
        _text.Clear();
        Submitted?.Invoke(line);
    }

    /// <summary>Remplace le texte par la commande choisie, suivie de after.</summary>
    private void Complete(string after)
    {
        if (_suggestions.SelectedItem is ICommand command)
            SetText("/" + command.Name + after);
    }

    private void SetText(string text)
    {
        _text.Text = text;
        _text.SelectionStart = text.Length;
    }

    /// <summary>
    /// ↑ (-1) ou ↓ (+1) dans l'historique. La ligne en cours d'écriture est gardée et revient en bas.
    /// La liste reste fermée sur une ligne rappelée : ↑↓ continuent dans l'historique, même sur "/models".
    /// </summary>
    private void MoveInHistory(int delta)
    {
        int index = Math.Clamp(_historyIndex + delta, 0, _history.Count);
        if (index == _historyIndex) return;
        if (_historyIndex == _history.Count)
            _draft = _text.Text;
        _historyIndex = index;
        SetText(index == _history.Count ? _draft : _history[index]);
        _dismissed = true;
        UpdateSuggestions();
    }

    /// <summary>Les commandes à proposer : le texte commence par "/" et on tape encore le nom (pas d'espace).</summary>
    private List<ICommand> Matches()
    {
        string text = _text.Text;
        if (_dismissed || !text.StartsWith('/') || text.Any(char.IsWhiteSpace))
            return new List<ICommand>();
        return _commands.Where(c => c.Name.StartsWith(text[1..], StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>Remplit la liste, sur la commande au nom exact s'il y en a une, et ajuste la hauteur.</summary>
    private void UpdateSuggestions()
    {
        var matches = Matches();
        _suggestions.BeginUpdate();
        _suggestions.Items.Clear();
        _suggestions.Items.AddRange(matches.ToArray<object>());
        if (matches.Count > 0)
        {
            string name = _text.Text.TrimStart('/');
            _suggestions.SelectedIndex = Math.Max(0, matches.FindIndex(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));
        }
        _suggestions.EndUpdate();

        _suggestions.ItemHeight = _suggestions.Font.Height + 4;
        _suggestions.Height = Math.Min(matches.Count, MaxSuggestions) * _suggestions.ItemHeight;
        _suggestions.Visible = matches.Count > 0;
        Height = Padding.Vertical + _text.Font.Height * TextLines + 6 + Theme.Small.Height + 4 + (_suggestions.Visible ? _suggestions.Height : 0);
    }

    private void DrawSuggestion(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var command = (ICommand)_suggestions.Items[e.Index];
        bool selected = (e.State & DrawItemState.Selected) != 0;
        using (var background = new SolidBrush(selected ? Theme.Hover : Theme.Surface))
            e.Graphics.FillRectangle(background, e.Bounds);

        int nameWidth = _commands.Max(c => c.Name.Length) + 2;
        string name = (selected ? "› " : "  ") + ("/" + command.Name).PadRight(nameWidth);
        var flags = TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
        TextRenderer.DrawText(e.Graphics, name, selected ? Theme.Bold : Theme.Font, e.Bounds,
            selected ? Theme.Accent : Theme.Command, flags);
        int x = TextRenderer.MeasureText(e.Graphics, name + " ", Theme.Font, e.Bounds.Size, flags).Width;
        var rest = e.Bounds with { X = e.Bounds.X + x, Width = e.Bounds.Width - x };
        TextRenderer.DrawText(e.Graphics, command.Description, Theme.Font, rest,
            selected ? Theme.Text : Theme.Dim, flags | TextFormatFlags.EndEllipsis);
    }
}
