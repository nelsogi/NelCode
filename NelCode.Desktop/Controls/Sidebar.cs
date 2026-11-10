using NelCode.Storage;

namespace NelCode.Desktop.Controls;

/// <summary>
/// La colonne de gauche : le bouton Nouvelle conversation, les conversations groupées par date
/// (clic pour reprendre), les modèles installés (clic pour changer).
/// </summary>
internal sealed class Sidebar : UserControl
{
    private const int MaxSessions = 100;

    private readonly ListBox _sessions = List();
    private readonly ListBox _models = List();
    private string? _currentSession;
    private string? _activeModel;

    public Sidebar()
    {
        BackColor = Theme.Sidebar;
        Padding = new Padding(8);

        var newButton = Theme.Button("+  Nouvelle conversation");
        newButton.Dock = DockStyle.Top;
        newButton.AutoSize = false;
        newButton.Height = 34;
        newButton.Click += (_, _) => NewClicked?.Invoke();

        _models.Dock = DockStyle.Bottom;
        _models.Height = 150;
        _sessions.DrawItem += DrawSession;
        _models.DrawItem += DrawModel;
        _sessions.MouseClick += (_, e) =>
        {
            if (ItemAt(_sessions, e.Location) is Session session) SessionChosen?.Invoke(session);
        };
        _models.MouseClick += (_, e) =>
        {
            if (ItemAt(_models, e.Location) is ModelEntry model) ModelChosen?.Invoke(model.Name);
        };

        Controls.Add(_sessions);
        Controls.Add(Title("CONVERSATIONS", DockStyle.Top));
        Controls.Add(newButton);
        Controls.Add(Title("MODÈLES", DockStyle.Bottom));
        Controls.Add(_models);   // ajouté en dernier = placé en premier : tout en bas, sous son titre
    }

    public event Action? NewClicked;
    public event Action<Session>? SessionChosen;
    public event Action<string>? ModelChosen;

    /// <summary>Les conversations, de la plus récente à la plus ancienne ; currentId est mise en évidence.</summary>
    public void ShowSessions(IEnumerable<Session> sessions, string currentId)
    {
        _currentSession = currentId;
        _sessions.BeginUpdate();
        _sessions.Items.Clear();
        foreach (var group in sessions.Where(s => s.Messages.Count > 0).Take(MaxSessions).GroupBy(s => Group(s.Date)))
        {
            _sessions.Items.Add(group.Key);
            foreach (var session in group)
                _sessions.Items.Add(session);
        }
        _sessions.EndUpdate();
    }

    public void ShowModels(IEnumerable<ModelEntry> models, string? active)
    {
        _activeModel = active;
        _models.BeginUpdate();
        _models.Items.Clear();
        _models.Items.AddRange(models.ToArray<object>());
        _models.EndUpdate();
    }

    /// <summary>Le groupe d'une date : les sessions arrivent triées, les groupes sont donc dans l'ordre.</summary>
    private static string Group(DateTime date)
    {
        int days = (DateTime.Today - date.Date).Days;
        return days switch
        {
            <= 0 => "Aujourd'hui",
            1 => "Hier",
            < 7 => "7 derniers jours",
            < 30 => "30 derniers jours",
            _ => date.ToString("MMMM yyyy"),
        };
    }

    private void DrawSession(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var item = _sessions.Items[e.Index];
        if (item is string group)
        {
            Fill(e, Theme.Sidebar);
            var bounds = e.Bounds with { Y = e.Bounds.Y + 4, Height = e.Bounds.Height - 4 };
            TextRenderer.DrawText(e.Graphics, group, Theme.Small, bounds, Theme.Dim, Flags);
            return;
        }

        var session = (Session)item;
        bool current = session.Id == _currentSession;
        Fill(e, current ? Theme.Hover : Theme.Sidebar);
        string first = session.Messages[0].Content.ReplaceLineEndings(" ");
        TextRenderer.DrawText(e.Graphics, first, Theme.Small, Inset(e.Bounds), current ? Theme.Text : Theme.User,
            Flags | TextFormatFlags.EndEllipsis);
    }

    private void DrawModel(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var model = (ModelEntry)_models.Items[e.Index];
        bool active = model.Name == _activeModel;
        Fill(e, active ? Theme.Hover : Theme.Sidebar);
        string size = $"{model.Bytes / 1e6:0} Mo";
        var bounds = Inset(e.Bounds);
        TextRenderer.DrawText(e.Graphics, size, Theme.Small, bounds, Theme.Dim, Flags | TextFormatFlags.Right);
        TextRenderer.DrawText(e.Graphics, (active ? $"{Theme.ReplyBullet} " : "  ") + model.Name, Theme.Small,
            bounds with { Width = bounds.Width - TextRenderer.MeasureText(size + "  ", Theme.Small).Width },
            active ? Theme.Accent : Theme.Command, Flags | TextFormatFlags.EndEllipsis);
    }

    private const TextFormatFlags Flags = TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;

    private static Rectangle Inset(Rectangle r) => r with { X = r.X + 6, Width = r.Width - 12 };

    private static void Fill(DrawItemEventArgs e, Color color)
    {
        using var brush = new SolidBrush(color);
        e.Graphics.FillRectangle(brush, e.Bounds);
    }

    private static object? ItemAt(ListBox list, Point point)
    {
        int index = list.IndexFromPoint(point);
        return index >= 0 && list.GetItemRectangle(index).Contains(point) ? list.Items[index] : null;
    }

    private static ListBox List()
    {
        var list = new ListBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Sidebar,
            ForeColor = Theme.Text,
            Font = Theme.Small,
            DrawMode = DrawMode.OwnerDrawFixed,
            IntegralHeight = false,
            SelectionMode = SelectionMode.None,
            Cursor = Cursors.Hand,
        };
        list.ItemHeight = list.Font.Height + 10;
        Theme.DarkScrollBars(list);
        return list;
    }

    private static Label Title(string text, DockStyle dock) => new()
    {
        Text = text,
        Dock = dock,
        AutoSize = false,
        Height = 30,
        TextAlign = ContentAlignment.BottomLeft,
        ForeColor = Theme.Dim,
        Font = Theme.Small,
        Padding = new Padding(4, 0, 0, 4),
    };
}
