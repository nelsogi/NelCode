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

        Controls.Add(_list);
        Controls.Add(rule);
        Controls.Add(header);
    }

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
