using NelCode.Storage;

namespace NelCode.Desktop.Controls;

/// <summary>
/// La conversation, style terminal :
///   modèle · GPU/CPU · dossier de travail (cliquable)      <- l'en-tête
///   > Bonjour !                                            <- l'utilisateur (MessageControl)
///   ● Bonjour, comment vas-tu ?                            <- la réponse, écrite au fur et à mesure
/// Une liste de MessageControl qui défile, et qui suit la fin tant qu'on n'est pas remonté lire plus haut.
/// Ce qu'il faut écrire est décidé ailleurs (MainForm, DesktopOutput).
/// </summary>
internal sealed class ChatView : UserControl
{
    private readonly Label _model = new() { Dock = DockStyle.Top, AutoSize = true, ForeColor = Theme.Text, Font = Theme.Bold };
    private readonly Label _device = new() { AutoSize = true, Margin = new Padding(0), Font = Theme.Small };
    private readonly LinkLabel _folder = new()
    {
        AutoSize = true,
        Margin = new Padding(0),
        Font = Theme.Small,
        LinkColor = Theme.Dim,
        ActiveLinkColor = Theme.Accent,
        LinkBehavior = LinkBehavior.HoverUnderline,
    };
    private readonly ScrollPanel _list = new()
    {
        Dock = DockStyle.Fill,
        AutoScroll = true,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        Padding = new Padding(14, 10, 4, 0),
        BackColor = Theme.Background,
    };
    private bool _follow = true;   // suit la fin de la conversation
    private bool _scrollPending;

    public ChatView()
    {
        BackColor = Theme.Background;
        Theme.DarkScrollBars(_list);
        _folder.LinkClicked += (_, _) => FolderClicked?.Invoke();
        _list.ClientSizeChanged += (_, _) =>
        {
            foreach (Control control in _list.Controls) Fit(control);
        };
        // Seuls la molette et la barre de défilement (l'utilisateur) décident si on suit la fin.
        _list.MouseWheel += (_, _) => _follow = AtEnd();
        _list.Scroll += (_, _) => _follow = AtEnd();

        var line = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        line.Controls.Add(_device);
        line.Controls.Add(new Label { Text = "  ·  ", AutoSize = true, Margin = new Padding(0), ForeColor = Theme.Dim, Font = Theme.Small });
        line.Controls.Add(_folder);

        var header = new Panel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(14, 10, 14, 10) };
        header.Controls.Add(line);
        header.Controls.Add(_model);
        var rule = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Theme.Border };

        _find.TextChanged += (_, _) => RunFind();
        _find.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) StepFind(e.Shift ? -1 : 1);
            else if (e.KeyCode == Keys.Escape) HideFind();
            else return;
            e.SuppressKeyPress = true;
        };
        var findBox = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(8, 6, 8, 0) };
        findBox.Controls.Add(_find);
        findBox.Controls.Add(_findCount);
        _findBar.Controls.Add(findBox);

        Controls.Add(_list);
        Controls.Add(_findBar);
        Controls.Add(rule);
        Controls.Add(header);
    }

    // --- Ctrl+F : rechercher dans la conversation ------------------------------------------------------------

    private readonly Panel _findBar = new() { Dock = DockStyle.Top, Height = 36, Visible = false, Padding = new Padding(14, 6, 14, 2) };
    private readonly TextBox _find = new()
    {
        Dock = DockStyle.Fill,
        BorderStyle = BorderStyle.None,
        BackColor = Theme.Surface,
        ForeColor = Theme.Text,
        Font = Theme.Font,
        PlaceholderText = "Rechercher dans la conversation (Entrée : suivant, Maj+Entrée : précédent, Échap : fermer)",
    };
    private readonly Label _findCount = new() { Dock = DockStyle.Right, AutoSize = true, ForeColor = Theme.Dim, Font = Theme.Small };
    private List<(MessageControl Message, int Start, int Length)> _matches = [];
    private int _current = -1;

    public void ShowFind()
    {
        _findBar.Visible = true;
        _find.Focus();
        _find.SelectAll();
        RunFind();
    }

    private void HideFind()
    {
        _findBar.Visible = false;
        foreach (var message in Messages()) message.ClearFound();
        _matches = [];
        FindClosed?.Invoke();
    }

    /// <summary>La recherche fermée (Échap) : la saisie reprend le focus.</summary>
    public event Action? FindClosed;

    private void RunFind()
    {
        string query = _find.Text.Trim();
        _matches = Messages().SelectMany(m => m.Find(query).Select(f => (m, f.Start, f.Length))).ToList();
        _current = -1;
        StepFind(1);
    }

    /// <summary>Le résultat suivant (+1) ou précédent (-1), en boucle, mis en évidence et amené dans la vue.</summary>
    private void StepFind(int delta)
    {
        _findCount.Text = _find.Text.Trim().Length == 0 ? "" : _matches.Count == 0 ? "aucun résultat" : "";
        if (_matches.Count == 0) return;
        if (_current >= 0)
        {
            var (old, oldStart, oldLength) = _matches[_current];
            if (!old.IsDisposed) old.Mark(oldStart, oldLength, Theme.Found);
        }
        _current = (_current + delta + _matches.Count) % _matches.Count;
        var (message, start, length) = _matches[_current];
        if (message.IsDisposed) return;
        message.Mark(start, length, Theme.FoundCurrent);
        _findCount.Text = $"{_current + 1}/{_matches.Count}";

        _follow = false;
        int y = -_list.AutoScrollPosition.Y + message.Top + message.OffsetOf(start);
        _list.AutoScrollPosition = new Point(0, Math.Max(0, y - _list.ClientSize.Height / 3));
    }

    // --- Contexte du modèle -------------------------------------------------------------------------------------

    /// <summary>Grise les messages de la conversation avant firstMessage : le modèle ne les voit plus.</summary>
    public void FadeBefore(Session session, int firstMessage)
    {
        foreach (var message in Messages())
            if (message.Session == session)
                message.SetFaded(message.Index < firstMessage);
    }

    private IEnumerable<MessageControl> Messages() => _list.Controls.OfType<MessageControl>();

    /// <summary>Clic sur le dossier de travail.</summary>
    public event Action? FolderClicked;
    public event Action<MessageControl>? RegenerateClicked;
    public event Action<MessageControl, string>? EditSubmitted;
    public event Action<MessageControl, int>? VersionStep;

    public void SetHeader(string model, string device, bool gpu, string folder)
    {
        _model.Text = model;
        _device.Text = device;
        _device.ForeColor = gpu ? Theme.Success : Theme.Dim;
        _folder.Text = folder;
    }

    /// <summary>Du texte à la suite, dans le dernier bloc de sortie (ou un nouveau).</summary>
    public void Write(string text, Color color)
    {
        var last = _list.Controls.Count > 0 ? _list.Controls[^1] as MessageControl : null;
        if (last is not { Role: MessageControl.Kind.Log })
            last = Add(new MessageControl(MessageControl.Kind.Log));
        last.Append(text, color);
    }

    public void WriteLine(string text, Color color) => Write(text + "\n", color);

    public void Clear()
    {
        _list.SuspendLayout();
        while (_list.Controls.Count > 0) _list.Controls[^1].Dispose();
        _list.ResumeLayout();
        _follow = true;
    }

    public void Welcome()
    {
        Write($"{Theme.Logo} ", Theme.Accent);
        Write("Bienvenue dans ", Theme.Text);
        WriteLine("Nel Code", Theme.Accent);
        WriteLine("/help pour les commandes, ou écris directement pour discuter. " +
                  "Glisse un .nelmodel sur la fenêtre pour l'installer.\n", Theme.Dim);
    }

    /// <summary>Une commande tapée : écrite comme la sortie, sans boutons.</summary>
    public void Command(string line)
    {
        Write($"{Theme.Prompt} ", Theme.Dim);
        WriteLine(line, Theme.User);
    }

    public MessageControl User(string message)
    {
        var control = Add(new MessageControl(MessageControl.Kind.User));
        control.SetUser(message);
        return control;
    }

    /// <summary>Une réponse complète (conversation reprise).</summary>
    public MessageControl Reply(string reply, IReadOnlyList<string>? versions)
    {
        var control = Add(new MessageControl(MessageControl.Kind.Reply));
        control.SetReply(reply, versions);
        return control;
    }

    /// <summary>Une réponse à venir : « Réflexion... » jusqu'au premier token.</summary>
    public MessageControl StartReply()
    {
        var control = Add(new MessageControl(MessageControl.Kind.Reply));
        control.StartThinking();
        return control;
    }

    /// <summary>Retire ce message et tout ce qui suit.</summary>
    public void RemoveFrom(MessageControl message) => RemoveFromIndex(_list.Controls.GetChildIndex(message));

    /// <summary>Retire tout ce qui suit ce message.</summary>
    public void RemoveAfter(MessageControl message) => RemoveFromIndex(_list.Controls.GetChildIndex(message) + 1);

    /// <summary>Revient en bas et suit la fin (nouveau message envoyé).</summary>
    public void FollowEnd()
    {
        _follow = true;
        ScrollSoon();
    }

    private void RemoveFromIndex(int index)
    {
        _list.SuspendLayout();
        while (_list.Controls.Count > index) _list.Controls[^1].Dispose();
        _list.ResumeLayout();
    }

    private MessageControl Add(MessageControl message)
    {
        message.RegenerateClicked += m => RegenerateClicked?.Invoke(m);
        message.EditSubmitted += (m, text) => EditSubmitted?.Invoke(m, text);
        message.VersionStep += (m, delta) => VersionStep?.Invoke(m, delta);
        message.SizeChanged += (_, _) => ScrollSoon();
        Fit(message);
        _list.Controls.Add(message);
        ScrollSoon();
        return message;
    }

    private void Fit(Control control) => control.Width = Math.Max(0, _list.ClientSize.Width - _list.Padding.Horizontal);

    private bool AtEnd() => -_list.AutoScrollPosition.Y + _list.ClientSize.Height >= _list.DisplayRectangle.Height - 24;

    /// <summary>Descend en bas après la mise en page (une seule fois, même pour beaucoup de caractères d'un coup).</summary>
    private void ScrollSoon()
    {
        if (_scrollPending || !_follow || !IsHandleCreated) return;
        _scrollPending = true;
        BeginInvoke(() =>
        {
            _scrollPending = false;
            if (_follow) _list.AutoScrollPosition = new Point(0, _list.DisplayRectangle.Height);
        });
    }

    private sealed class ScrollPanel : FlowLayoutPanel
    {
        public ScrollPanel() => DoubleBuffered = true;

        // Cliquer dans un long message (pour sélectionner) ne fait pas sauter la vue sur son début.
        protected override Point ScrollToControl(Control activeControl) => DisplayRectangle.Location;
    }
}
