using System.Globalization;
using NelCode.Storage;

namespace NelCode.Desktop.Controls;

/// <summary>
/// La colonne de gauche : le bouton Nouvelle conversation, la recherche (titre et contenu), les conversations
/// groupées par date (clic pour reprendre, clic droit : Renommer / Exporter / Supprimer), les modèles
/// installés (clic pour changer).
/// </summary>
internal sealed class Sidebar : UserControl
{
    private const int MaxSessions = 100;
    private const int TitleLength = 40;

    private readonly ListBox _sessions = List();
    private readonly ListBox _models = List();
    private readonly TextBox _search = new()
    {
        Dock = DockStyle.Fill,
        BorderStyle = BorderStyle.None,
        BackColor = Theme.Surface,
        ForeColor = Theme.Text,
        Font = Theme.Small,
        PlaceholderText = "Rechercher...",
    };
    private readonly ContextMenuStrip _menu = Theme.Menu();
    private List<Session> _all = [];
    private Session? _menuSession;   // la conversation du clic droit
    private string? _currentSession;
    private string? _activeModel;

    public Sidebar()
    {
        BackColor = Theme.Sidebar;
        Padding = new Padding(8);

        var newButton = Theme.Button("+  Nouvelle conversation");
        newButton.Dock = DockStyle.Fill;
        newButton.Font = Theme.Small;   // tient à côté de ⚙
        newButton.AutoSize = false;
        newButton.Click += (_, _) => NewClicked?.Invoke();
        var settingsButton = Theme.Button("⚙");
        settingsButton.Dock = DockStyle.Right;
        settingsButton.AutoSize = false;
        settingsButton.Width = 38;
        settingsButton.Click += (_, _) => SettingsClicked?.Invoke();
        new ToolTip().SetToolTip(settingsButton, "Réglages (Ctrl+,)");
        var topRow = new Panel { Dock = DockStyle.Top, Height = 34 };
        topRow.Controls.Add(newButton);
        topRow.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 6 });   // écart entre les deux boutons
        topRow.Controls.Add(settingsButton);

        _models.Dock = DockStyle.Bottom;
        _models.Height = 150;
        _sessions.DrawItem += DrawSession;
        _models.DrawItem += DrawModel;
        _sessions.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left && ItemAt(_sessions, e.Location) is Session session) SessionChosen?.Invoke(session);
        };
        _sessions.MouseUp += (_, e) =>
        {
            if (e.Button != MouseButtons.Right || ItemAt(_sessions, e.Location) is not Session session) return;
            _menuSession = session;
            _menu.Show(_sessions, e.Location);
        };
        _menu.Items.Add("Renommer...", null, (_, _) => RenameClicked?.Invoke(_menuSession!));
        _menu.Items.Add("Exporter en .txt...", null, (_, _) => ExportClicked?.Invoke(_menuSession!, ".txt"));
        _menu.Items.Add("Exporter en .md...", null, (_, _) => ExportClicked?.Invoke(_menuSession!, ".md"));
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Supprimer...", null, (_, _) => DeleteClicked?.Invoke(_menuSession!)).ForeColor = Theme.Error;

        _search.TextChanged += (_, _) => ShowFiltered();
        var searchBox = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(6, 5, 6, 0) };
        searchBox.Controls.Add(_search);
        var searchRow = new Panel { Dock = DockStyle.Top, Height = 34, Padding = new Padding(0, 0, 0, 6) };
        searchRow.Controls.Add(searchBox);
        _models.MouseClick += (_, e) =>
        {
            if (ItemAt(_models, e.Location) is ModelEntry model) ModelChosen?.Invoke(model.Name);
        };

        Controls.Add(_sessions);
        Controls.Add(searchRow);
        Controls.Add(Title("CONVERSATIONS", DockStyle.Top));
        Controls.Add(topRow);
        Controls.Add(Title("MODÈLES", DockStyle.Bottom));
        Controls.Add(_models);   // ajouté en dernier = placé en premier : tout en bas, sous son titre
    }

    public event Action? NewClicked;
    public event Action? SettingsClicked;
    public event Action<Session>? SessionChosen;
    public event Action<string>? ModelChosen;
    public event Action<Session>? RenameClicked;
    public event Action<Session>? DeleteClicked;
    /// <summary>La conversation et l'extension voulue : ".txt" ou ".md".</summary>
    public event Action<Session, string>? ExportClicked;

    /// <summary>Le nom donné, sinon le début du premier message (coupé à un espace).</summary>
    public static string TitleOf(Session session)
    {
        if (!string.IsNullOrWhiteSpace(session.Title)) return session.Title;
        string first = string.Join(' ', (session.Messages.FirstOrDefault()?.Content ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (first.Length == 0) return "Nouvelle conversation";
        if (first.Length <= TitleLength) return first;
        int cut = first.LastIndexOf(' ', TitleLength);
        return first[..(cut > TitleLength / 2 ? cut : TitleLength)] + "…";
    }

    /// <summary>Les conversations, de la plus récente à la plus ancienne ; currentId est mise en évidence.</summary>
    public void ShowSessions(IEnumerable<Session> sessions, string currentId)
    {
        _currentSession = currentId;
        _all = sessions.Where(s => s.Messages.Count > 0).ToList();
        ShowFiltered();
    }

    /// <summary>Les conversations dont le titre ou un message contient la recherche (sans casse ni accents).</summary>
    private void ShowFiltered()
    {
        string query = _search.Text.Trim();
        var compare = CultureInfo.CurrentCulture.CompareInfo;
        bool Has(string text) => compare.IndexOf(text, query, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
        var shown = query.Length == 0 ? _all : _all.Where(s => Has(TitleOf(s)) || s.Messages.Any(m => Has(m.Content)));

        _sessions.BeginUpdate();
        _sessions.Items.Clear();
        foreach (var group in shown.Take(MaxSessions).GroupBy(s => Group(s.Date)))
        {
            _sessions.Items.Add(group.Key);
            foreach (var session in group)
                _sessions.Items.Add(session);
        }
        if (_sessions.Items.Count == 0 && query.Length > 0)
            _sessions.Items.Add("Aucun résultat");
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
        TextRenderer.DrawText(e.Graphics, TitleOf(session), Theme.Small, Inset(e.Bounds), current ? Theme.Text : Theme.User,
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
