namespace NelCode.Desktop.Controls;

/// <summary>
/// La conversation, style terminal :
///   modèle · GPU/CPU · dossier de travail (cliquable)      <- l'en-tête
///   > Bonjour !                                            <- l'utilisateur
///   ● Bonjour, comment vas-tu ?                            <- la réponse, écrite au fur et à mesure
/// Un simple terminal coloré : ce qu'il faut écrire est décidé ailleurs (MainForm, DesktopOutput).
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
    private readonly RichTextBox _text = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        BorderStyle = BorderStyle.None,
        BackColor = Theme.Background,
        ForeColor = Theme.Text,
        Font = Theme.Font,
        DetectUrls = false,
        HideSelection = false,
        ScrollBars = RichTextBoxScrollBars.Vertical,
    };

    public ChatView()
    {
        BackColor = Theme.Background;
        Theme.DarkScrollBars(_text);
        _folder.LinkClicked += (_, _) => FolderClicked?.Invoke();

        var line = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        line.Controls.Add(_device);
        line.Controls.Add(new Label { Text = "  ·  ", AutoSize = true, Margin = new Padding(0), ForeColor = Theme.Dim, Font = Theme.Small });
        line.Controls.Add(_folder);

        var header = new Panel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(14, 10, 14, 10) };
        header.Controls.Add(line);
        header.Controls.Add(_model);
        var rule = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Theme.Border };

        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14, 10, 4, 0) };
        body.Controls.Add(_text);

        Controls.Add(body);
        Controls.Add(rule);
        Controls.Add(header);
    }

    /// <summary>Clic sur le dossier de travail.</summary>
    public event Action? FolderClicked;

    public void SetHeader(string model, string device, bool gpu, string folder)
    {
        _model.Text = model;
        _device.Text = device;
        _device.ForeColor = gpu ? Theme.Success : Theme.Dim;
        _folder.Text = folder;
    }

    public void Write(string text, Color color)
    {
        _text.SelectionStart = _text.TextLength;
        _text.SelectionLength = 0;
        _text.SelectionColor = color;
        _text.SelectionFont = _text.Font;   // RichEdit change de police pour un symbole (✻) et la garderait ensuite
        _text.AppendText(text.Replace((char)0x202F, (char)0xA0));   // espace fine des milliers (fr) : absente de la police
        _text.ScrollToCaret();
    }

    public void WriteLine(string text, Color color) => Write(text + "\n", color);

    public void Clear() => _text.Clear();

    public void Welcome()
    {
        Write($"{Theme.Logo} ", Theme.Accent);
        Write("Bienvenue dans ", Theme.Text);
        WriteLine("Nel Code", Theme.Accent);
        WriteLine("/help pour les commandes, ou écris directement pour discuter. " +
                  "Glisse un .nelmodel sur la fenêtre pour l'installer.\n", Theme.Dim);
    }

    public void User(string message)
    {
        Write($"{Theme.Prompt} ", Theme.Dim);
        WriteLine(message, Theme.User);
    }

    /// <summary>Une réponse complète (conversation reprise).</summary>
    public void Reply(string reply)
    {
        Write($"{Theme.ReplyBullet} ", Theme.Accent);
        WriteLine(reply + "\n", Theme.Text);
    }
}
