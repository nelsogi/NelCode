using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using NelCode.Storage;

namespace NelCode.Desktop.Controls;

/// <summary>
/// Un bloc de la conversation, style terminal :
///   > Bonjour !                                   <- User : Copier · Modifier au survol
///   ● Bonjour, comment vas-tu ?                   <- Reply : Copier · Régénérer au survol, ‹ 1/2 › s'il y a des versions
///   12 tokens · 40 tokens/s  ‹ 1/2 ›  Copier  Régénérer
/// ou la sortie des commandes (Log), sans boutons.
/// </summary>
internal sealed partial class MessageControl : UserControl
{
    public enum Kind { Log, User, Reply }

    // L'étoile qui tourne avant le premier token.
    private static readonly string[] Frames = ["·", "✢", "✳", "✶", "✻", "✽", "✻", "✶", "✳", "✢"];

    private readonly MessageText _text = new();
    private readonly FlowLayoutPanel _actions = new()
    {
        Dock = DockStyle.Top,
        Height = 24,
        WrapContents = false,
        Margin = Padding.Empty,
        Padding = new Padding(0, 1, 0, 4),
    };
    private readonly List<Label> _onHover = [];   // visibles seulement au survol
    private readonly Label? _copy, _stats, _versions, _save, _cancel;
    private System.Windows.Forms.Timer? _thinking;
    private int _frame;
    private bool _pendingNewline;   // le dernier "\n" d'un bloc n'est écrit qu'avec la suite : pas de ligne vide en trop
    private bool _editing;
    private bool _faded, _found;

    public MessageControl(Kind kind)
    {
        Role = kind;
        BackColor = Theme.Background;
        Margin = Padding.Empty;

        Controls.Add(_actions);
        Controls.Add(_text);   // ajouté en dernier = placé en premier : le texte en haut, les boutons dessous
        _text.SizeChanged += (_, _) => FitHeight();
        _text.KeyDown += OnTextKeyDown;

        switch (kind)
        {
            case Kind.User:
                _copy = Action("Copier", Copy, hover: true);
                Action("Modifier", BeginEdit, hover: true);
                _save = Action("Valider (Entrée)", () => EditSubmitted?.Invoke(this, _text.Text));
                _cancel = Action("Annuler (Échap)", CancelEdit);
                _save.Visible = _cancel.Visible = false;
                break;
            case Kind.Reply:
                _stats = Action("", null);
                _versions = Action("", () => { });
                // Un seul label « ‹ 1/2 › » : la moitié gauche recule, la droite avance.
                _versions.MouseClick += (_, e) => VersionStep?.Invoke(this, e.X < _versions.Width / 2 ? -1 : +1);
                _copy = Action("Copier", Copy, hover: true);
                Action("Régénérer", () => RegenerateClicked?.Invoke(this), hover: true);
                ShowVersions(1, 0);
                break;
        }
        HookHover(this);
        FitHeight();
    }

    public Kind Role { get; }

    /// <summary>Le texte du message, sans "> " ni "● " ni "(interrompu)" : ce que Copier copie.</summary>
    public string Content { get; private set; } = "";

    /// <summary>Où est ce message : Session.Messages[Index]. Null : pas (encore) dans une conversation.</summary>
    public Session? Session { get; private set; }
    public int Index { get; private set; } = -1;

    public void SetOrigin(Session session, int index) => (Session, Index) = (session, index);

    public event Action<MessageControl>? RegenerateClicked;
    public event Action<MessageControl, string>? EditSubmitted;
    /// <summary>‹ (-1) ou › (+1).</summary>
    public event Action<MessageControl, int>? VersionStep;

    /// <summary>Un morceau de texte coloré à la suite (blocs Log).</summary>
    public void Append(string text, Color color)
    {
        if (_pendingNewline) Write("\n", color);
        _pendingNewline = text.EndsWith('\n');
        Write(_pendingNewline ? text[..^1] : text, color);
    }

    public void SetUser(string text)
    {
        Content = text;
        _text.Clear();
        Write($"{Theme.Prompt} ", Theme.Dim);
        Write(text, Shade(Theme.User));
    }

    /// <summary>Grisé : le modèle ne voit plus ce message (hors de son contexte).</summary>
    public void SetFaded(bool faded)
    {
        if (faded == _faded || _editing || _thinking?.Enabled == true) return;
        _faded = faded;
        string content = Content;
        if (Role == Kind.User)
        {
            SetUser(content);
        }
        else if (Role == Kind.Reply)
        {
            BeginReply();
            AppendReply(content);
        }
    }

    /// <summary>Surligne les occurrences de query (sans casse ni accents) ; renvoie leurs positions dans le texte.</summary>
    public List<(int Start, int Length)> Find(string query)
    {
        ClearFound();
        var found = new List<(int, int)>();
        if (query.Length == 0 || _editing) return found;
        string text = _text.Text;
        var compare = CultureInfo.CurrentCulture.CompareInfo;
        for (int from = 0; from < text.Length;)
        {
            int at = compare.IndexOf(text.AsSpan(from), query, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace, out int length);
            if (at < 0 || length == 0) break;
            found.Add((from + at, length));
            from += at + length;
        }
        foreach (var (start, length) in found)
            Mark(start, length, Theme.Found);
        return found;
    }

    public void Mark(int start, int length, Color color)
    {
        _found = true;
        _text.Select(start, length);
        _text.SelectionBackColor = color;
        _text.Select(_text.TextLength, 0);
    }

    public void ClearFound()
    {
        if (!_found) return;
        _found = false;
        _text.SelectAll();
        _text.SelectionBackColor = _text.BackColor;
        _text.Select(_text.TextLength, 0);
    }

    /// <summary>La hauteur d'un caractère du texte, depuis le haut du message (pour y faire défiler la vue).</summary>
    public int OffsetOf(int charIndex) => _text.Top + _text.GetPositionFromCharIndex(charIndex).Y;

    /// <summary>Une réponse complète, avec ses versions s'il y en a (la version affichée est text).</summary>
    public void SetReply(string text, IReadOnlyList<string>? versions)
    {
        BeginReply();
        AppendReply(text);
        SetStats("");
        ShowVersions(versions?.Count ?? 1, versions?.ToList().IndexOf(text) ?? 0);
    }

    /// <summary>« ✻ Réflexion... » animé, jusqu'à BeginReply.</summary>
    public void StartThinking()
    {
        if (_thinking is null)
        {
            _thinking = new System.Windows.Forms.Timer { Interval = 120 };
            _thinking.Tick += (_, _) => DrawThinking();
        }
        Content = "";
        SetStats("");
        _thinking.Start();
        DrawThinking();
    }

    /// <summary>Arrête la réflexion et écrit "● " : la réponse suit par AppendReply.</summary>
    public void BeginReply()
    {
        _thinking?.Stop();
        Content = "";
        _text.Clear();
        Write($"{Theme.ReplyBullet} ", Shade(Theme.Accent));
    }

    public void AppendReply(string text)
    {
        Content += text;
        Write(text, Shade(Theme.Text));
    }

    private Color Shade(Color color) => _faded ? Theme.Faded : color;

    /// <summary>Une remarque en gris après la réponse, hors du texte copié : "(interrompu)".</summary>
    public void Note(string text) => Write(text, Theme.Dim);

    public void SetStats(string text)
    {
        _stats!.Text = text;
        _stats.Visible = text.Length > 0;
    }

    public void ShowVersions(int count, int index)
    {
        _versions!.Visible = count > 1;
        _versions.Text = $"‹ {index + 1}/{count} ›";
    }

    public void CancelEdit()
    {
        _editing = false;
        _text.ReadOnly = true;
        _text.BackColor = Theme.Background;
        _save!.Visible = _cancel!.Visible = false;
        SetUser(Content);
        UpdateHover();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _thinking?.Dispose();
        base.Dispose(disposing);
    }

    private void BeginEdit()
    {
        _editing = true;
        UpdateHover();
        _text.ReadOnly = false;
        _text.BackColor = Theme.Surface;
        _text.Clear();
        Write(Content, Theme.Text);
        _save!.Visible = _cancel!.Visible = true;
        _text.Focus();
        _text.SelectionStart = _text.TextLength;
    }

    private void OnTextKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_editing) return;
        if (e.KeyCode == Keys.Enter && !e.Shift)
        {
            e.SuppressKeyPress = true;
            BeginInvoke(() => EditSubmitted?.Invoke(this, _text.Text));   // après la touche : le message peut être retiré
        }
        else if (e.KeyCode == Keys.Escape)
        {
            e.SuppressKeyPress = true;
            CancelEdit();
        }
    }

    private void DrawThinking()
    {
        _frame = (_frame + 1) % Frames.Length;
        _text.Clear();
        Write($"{Frames[_frame]} Réflexion...", Theme.Accent);
    }

    private void Copy()
    {
        if (Content.Length == 0) return;
        Clipboard.SetText(Content);
        _copy!.Text = "✓ Copié";
    }

    /// <summary>
    /// RichEdit prend une police de repli pour un symbole (✻ ● ✓) et la garderait pour la suite du texte :
    /// chaque symbole est donc écrit à part, et la police remise après.
    /// </summary>
    private void Write(string text, Color color)
    {
        text = text.Replace((char)0x202F, (char)0xA0);   // espace fine des milliers (fr) : absente de la police
        foreach (string part in Symbols().Split(text))
        {
            if (part.Length == 0) continue;
            _text.SelectionStart = _text.TextLength;
            _text.SelectionLength = 0;
            _text.SelectionColor = color;
            _text.SelectionFont = Theme.Font;
            _text.AppendText(part);
        }
    }

    [GeneratedRegex(@"([ -⯿])")]   // ponctuation, flèches, formes, symboles
    private static partial Regex Symbols();

    private void FitHeight() => Height = _text.Height + (Role == Kind.Log ? 0 : _actions.Height);

    private Label Action(string text, Action? click, bool hover = false)
    {
        var label = new Label { Text = text, AutoSize = true, ForeColor = Theme.Dim, Font = Theme.Small, Margin = new Padding(0, 0, 12, 0) };
        if (click is not null)
        {
            label.Cursor = Cursors.Hand;
            label.MouseEnter += (_, _) => label.ForeColor = Theme.Accent;
            label.MouseLeave += (_, _) => label.ForeColor = Theme.Dim;
            label.Click += (_, _) => click();
        }
        if (hover)
        {
            label.Visible = false;
            _onHover.Add(label);
        }
        _actions.Controls.Add(label);
        return label;
    }

    /// <summary>Entrer dans un enfant fait « quitter » le parent : on regarde donc où est la souris.</summary>
    private void HookHover(Control control)
    {
        control.MouseEnter += (_, _) => UpdateHover();
        control.MouseLeave += (_, _) => UpdateHover();
        foreach (Control child in control.Controls)
            HookHover(child);
    }

    private void UpdateHover()
    {
        bool show = !_editing && ClientRectangle.Contains(PointToClient(Cursor.Position));
        foreach (var label in _onHover)
            label.Visible = show;
        if (!show && _copy is not null)
            _copy.Text = "Copier";
    }

    /// <summary>Un RichTextBox sans barre de défilement, aussi haut que son texte ; la molette fait défiler la conversation.</summary>
    private sealed class MessageText : RichTextBox
    {
        private const int WmMouseWheel = 0x020A;
        private const int EmRequestResize = 0x0400 + 65;
        private int _width;

        public MessageText()
        {
            Dock = DockStyle.Top;
            BorderStyle = BorderStyle.None;
            ReadOnly = true;
            ScrollBars = RichTextBoxScrollBars.None;
            DetectUrls = false;
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = Theme.Font;
            Height = Font.Height + 2;
            ContentsResized += (_, e) => Height = e.NewRectangle.Height + 2;
        }

        // Une nouvelle largeur change les retours à la ligne : RichEdit doit redonner sa hauteur.
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Width != _width && IsHandleCreated)
            {
                _width = Width;
                SendMessage(Handle, EmRequestResize, 0, 0);
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmMouseWheel)
                for (var parent = Parent; parent is not null; parent = parent.Parent)
                    if (parent is ScrollableControl { AutoScroll: true })
                    {
                        SendMessage(parent.Handle, m.Msg, m.WParam, m.LParam);
                        return;
                    }
            base.WndProc(ref m);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}
