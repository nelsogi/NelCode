using System.Text;
using NelCode.Commands;
using NelCode.Desktop.Controls;
using StatusBar = NelCode.Desktop.Controls.StatusBar;   // pas celle, obsolète, de WinForms
using NelCode.Engine;
using NelCode.Storage;
using TorchSharp;

namespace NelCode.Desktop;

/// <summary>
/// La fenêtre : place les quatre zones et relie leurs événements au CommandContext de NelCode.Core.
/// Tout le travail (commandes, réponses, chargements) tourne en tâche de fond : la fenêtre ne se fige jamais.
/// </summary>
internal sealed class MainForm : Form
{
    private readonly string _workingDirectory;
    private readonly CommandRegistry _registry = new();
    private readonly Sidebar _sidebar = new() { Dock = DockStyle.Left, Width = 260 };
    private readonly ChatView _chat = new() { Dock = DockStyle.Fill };
    private readonly InputBox _input;
    private readonly StatusBar _status = new() { Dock = DockStyle.Bottom };
    private readonly DesktopOutput _output;
    private readonly Settings _settings;           // les mêmes que ceux du CommandContext, lus avant lui
    private readonly string? _settingsWarning;

    private CommandContext? _context;          // créé en fond au démarrage (TorchSharp met un moment à s'initialiser)
    private bool _busy;
    private CancellationTokenSource? _reply;   // la réponse en cours, qu'Échap ou Stop arrête

    public MainForm(string workingDirectory)
    {
        _workingDirectory = workingDirectory;
        _settings = Settings.Load(out _settingsWarning);
        _input = new InputBox(_registry.Commands) { Dock = DockStyle.Bottom, Busy = true };
        _output = new DesktopOutput(this, _chat, _status, () => _context?.Session);

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Nel Code";
        Icon = Theme.AppIcon;
        ClientSize = new Size(1180, 760);
        MinimumSize = new Size(720, 420);
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Font = Theme.Font;
        KeyPreview = true;
        AllowDrop = true;

        var main = new Panel { Dock = DockStyle.Fill };
        main.Controls.Add(_chat);
        main.Controls.Add(_input);
        // Le séparateur se tire pour élargir la colonne de gauche.
        var separator = new Splitter { Dock = DockStyle.Left, Width = 3, BackColor = Theme.Border, MinSize = 180, MinExtra = 420 };

        // Ajouté en dernier = placé en premier : la barre d'état prend toute la largeur, puis la colonne de gauche.
        Controls.Add(main);
        Controls.Add(separator);
        Controls.Add(_sidebar);
        Controls.Add(_status);
        ResumeLayout();

        _input.Submitted += OnSubmitted;
        _input.StopClicked += () => _reply?.Cancel();
        _sidebar.NewClicked += OnNewConversation;
        _sidebar.SettingsClicked += OpenSettings;
        _chat.FindClosed += _input.FocusText;
        _sidebar.SessionChosen += OnSessionChosen;
        _sidebar.ModelChosen += OnModelChosen;
        _sidebar.RenameClicked += OnRenameSession;
        _sidebar.DeleteClicked += OnDeleteSession;
        _sidebar.ExportClicked += OnExportSession;
        _chat.FolderClicked += OnFolderClicked;
        _chat.RegenerateClicked += OnRegenerate;
        _chat.EditSubmitted += OnEditSubmitted;
        _chat.VersionStep += OnVersionStep;
        AcceptDrops(this);

        _chat.Welcome();
        _chat.SetHeader("Démarrage...", "", false, workingDirectory);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.DarkTitleBar(this);
    }

    /// <summary>
    /// La taille, la position et la colonne de gauche de la dernière fois. Après base.OnLoad : la mise à l'échelle
    /// (écran à 150 %...) est déjà faite, les valeurs sauvegardées sont en pixels réels.
    /// </summary>
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        if (_settings.Window is not { } window) return;
        var bounds = new Rectangle(window.X, window.Y, window.Width, window.Height);
        if (Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(bounds)))   // l'écran n'a pas été débranché
        {
            Bounds = bounds;
            if (window.Maximized) WindowState = FormWindowState.Maximized;
        }
        _sidebar.Width = Math.Clamp(window.SidebarWidth, 180, ClientSize.Width / 2);
    }

    /// <summary>Retient la fenêtre, dans le settings.json du disque (la console a pu le changer entre-temps).</summary>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        var saved = Settings.Load(out _);
        saved.Window = new WindowPlacement(bounds.X, bounds.Y, bounds.Width, bounds.Height,
            WindowState == FormWindowState.Maximized, _sidebar.Width);
        try { saved.Save(); }
        catch (IOException) { }   // tant pis pour la place de la fenêtre
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _input.FocusText();

        if (_settingsWarning is not null)
            _output.Warning(_settingsWarning);

        await RunAsync(() =>
        {
            var device = _output.Busy("Démarrage de TorchSharp...", () => torch.cuda.is_available() ? torch.CUDA : torch.CPU);
            _context = new CommandContext(_registry, _output, _settings, device, _workingDirectory);
            _context.LoadDefaultModel();
        });
        if (_context is null)
            _output.Error("Nel Code n'a pas pu démarrer TorchSharp : rien ne peut être envoyé.");
        else if (ModelLibrary.List().Count == 0)
            NoModelMessage();
    }

    private void NoModelMessage()
    {
        _chat.WriteLine("╭──────────────────────────────────────────────────────────╮", Theme.Warning);
        _chat.Write("│  ", Theme.Warning);
        _chat.Write("Aucun modèle installé.".PadRight(56), Theme.Warning);
        _chat.WriteLine("│", Theme.Warning);
        _chat.Write("│  ", Theme.Warning);
        _chat.Write("Glisse un fichier .nelmodel ici pour l'installer,".PadRight(56), Theme.Text);
        _chat.WriteLine("│", Theme.Warning);
        _chat.Write("│  ", Theme.Warning);
        _chat.Write("ou tape /import <fichier.nelmodel>.".PadRight(56), Theme.Dim);
        _chat.WriteLine("│", Theme.Warning);
        _chat.WriteLine("╰──────────────────────────────────────────────────────────╯\n", Theme.Warning);
    }

    /// <summary>Échap pendant une réponse l'arrête, où que soit le focus ; les raccourcis de la fenêtre.</summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape && _reply is { IsCancellationRequested: false } reply)
        {
            reply.Cancel();
            return true;
        }
        Action? shortcut = keyData switch
        {
            Keys.Control | Keys.N => OnNewConversation,
            Keys.Control | Keys.K => ShowPalette,
            Keys.Control | Keys.L => ClearView,
            Keys.Control | Keys.F => _chat.ShowFind,
            Keys.Control | Keys.Oemcomma => OpenSettings,
            Keys.F2 => RenameCurrent,
            Keys.F1 => ShowAbout,
            _ => null,
        };
        if (shortcut is null) return base.ProcessCmdKey(ref msg, keyData);
        shortcut();
        return true;
    }

    // --- Raccourcis, palette, réglages, À propos ---------------------------------------------------------------

    private void ShowPalette()
    {
        var items = new List<PaletteItem>
        {
            new("Nouvelle conversation", "Ctrl+N", OnNewConversation),
            new("Rechercher dans la conversation", "Ctrl+F", _chat.ShowFind),
            new("Effacer l'affichage", "Ctrl+L", ClearView),
            new("Renommer la conversation", "F2", RenameCurrent),
            new("Réglages", "Ctrl+,", OpenSettings),
            new("À propos de Nel Code", "F1", ShowAbout),
        };
        // Une commande sans argument est lancée ; les autres attendent la suite dans la zone de saisie.
        items.AddRange(_registry.Commands.Select(c => new PaletteItem("/" + c.Name, c.Description,
            () => { if (c.Usage == "/" + c.Name) OnSubmitted(c.Usage); else _input.Prefill($"/{c.Name} "); })));
        PaletteDialog.Show(this, items);
    }

    /// <summary>Ctrl+L : efface l'écran, comme /clear (la conversation continue).</summary>
    private void ClearView()
    {
        if (_busy) return;   // la réponse en cours s'écrit dans un message affiché
        _chat.Clear();
        _chat.Welcome();
    }

    private void RenameCurrent()
    {
        if (_context is { Session.Messages.Count: > 0 } context)
            OnRenameSession(context.Session);
    }

    private void OpenSettings()
    {
        bool themeChanged = SettingsDialog.Edit(this, _settings);
        RefreshViews();
        if (themeChanged && !_busy && PromptDialog.Confirm(this, "Relancer Nel Code maintenant pour changer de thème ? (la conversation est sauvegardée)"))
            Application.Restart();
    }

    private void ShowAbout()
    {
        string version = Application.ProductVersion.Split('+')[0];   // sans le commit ajouté par le SDK
        string model = _context?.Chat is { } chat
            ? $"{_context.ModelName} · {chat.Generator.Model.ParameterCount / 1e6:0.0} M paramètres · contexte {chat.BlockSize} tokens"
            : "aucun";
        string device = _context is null ? "—" : _context.OnGpu ? GpuName() ?? "GPU NVIDIA (CUDA)" : "CPU";
        PromptDialog.Inform(this, "À propos de Nel Code",
            $"{Theme.Logo} Nel Code Desktop {version}\n" +
            "Discute avec les modèles MiniChat, sur ta machine.\n\n" +
            $"Modèle   {model}\n" +
            $"Calcul   {device}\n" +
            $".NET     {Environment.Version}\n" +
            $"Données  {AppPaths.Root}");
    }

    /// <summary>Le nom de la carte NVIDIA (nvidia-smi est installé avec le pilote), null s'il ne répond pas.</summary>
    private static string? GpuName()
    {
        try
        {
            using var smi = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("nvidia-smi", "--query-gpu=name --format=csv,noheader")
            {
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            });
            if (smi is null || !smi.WaitForExit(2000)) return null;
            string name = smi.StandardOutput.ReadToEnd().Split('\n')[0].Trim();
            return name.Length > 0 ? $"{name} (CUDA)" : null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    // --- Ce que l'utilisateur fait ----------------------------------------------------------------------------

    private async void OnSubmitted(string line)
    {
        line = line.Trim();
        if (!Ready || line.Length == 0) return;

        _chat.FollowEnd();
        if (line.StartsWith('/'))
        {
            _chat.Command(line);
            await RunCommandAsync(line);
            return;
        }

        var session = _context!.Session;
        _chat.User(line).SetOrigin(session, session.Messages.Count);
        var reply = _chat.StartReply();
        string? text = await GenerateAsync(reply, (onChar, token) => _context.SendMessage(line, onChar, token));
        if (text?.Trim().Length > 0)
            reply.SetOrigin(session, session.Messages.Count - 1);   // SendMessage l'a ajoutée à la conversation
        else if (text is null && reply.Content.Length == 0)
            reply.Dispose();   // erreur (pas de modèle...) : l'erreur seule, sans "●"
        RefreshViews();   // les messages ont leur place : le grisage du contexte peut les trouver
    }

    /// <summary>Même question, nouvelle réponse, gardée comme une version de plus. La suite de la conversation est retirée.</summary>
    private async void OnRegenerate(MessageControl reply)
    {
        if (!Ready || !InCurrentSession(reply, ChatMessage.AssistantRole)) return;
        var session = _context!.Session;
        int index = reply.Index;
        var old = session.Messages[index];
        string question = session.Messages[index - 1].Content;
        var turns = session.Turns.Take((index - 1) / 2).ToList();   // les messages vont par paires

        session.Messages.RemoveRange(index + 1, session.Messages.Count - index - 1);
        SessionStore.Save(session);
        _chat.RemoveAfter(reply);
        _chat.FollowEnd();
        reply.StartThinking();

        string? text = await GenerateAsync(reply, (onChar, token) =>
            _context.RequireChat().Reply(turns, question, _settings.Sampling, _settings.MaxReplyTokens, onChar, token));
        if (text?.Trim() is not { Length: > 0 } added)
        {
            reply.SetReply(old.Content, old.Versions);   // rien de nouveau : l'ancienne réponse revient
            return;
        }
        var versions = old.Versions?.ToList() ?? [old.Content];
        versions.Add(added);
        session.Messages[index] = old with { Content = added, Versions = versions };
        session.Date = DateTime.Now;
        SessionStore.Save(session);
        reply.ShowVersions(versions.Count, versions.Count - 1);
        RefreshViews();
    }

    /// <summary>‹ › : la version affichée devient celle de la conversation (celle que le modèle verra ensuite).</summary>
    private void OnVersionStep(MessageControl reply, int delta)
    {
        if (!Ready || !InCurrentSession(reply, ChatMessage.AssistantRole)) return;
        var session = _context!.Session;
        var message = session.Messages[reply.Index];
        if (message.Versions is not { Count: > 1 } versions) return;
        int shown = Math.Clamp(versions.IndexOf(message.Content) + delta, 0, versions.Count - 1);
        session.Messages[reply.Index] = message with { Content = versions[shown] };
        SessionStore.Save(session);
        reply.SetReply(versions[shown], versions);
        RefreshViews();   // une autre version n'a pas la même longueur : le contexte change
    }

    /// <summary>Message modifié : la conversation repart de lui (ce qui le suivait est retiré).</summary>
    private void OnEditSubmitted(MessageControl user, string text)
    {
        if (!Ready || text.Trim().Length == 0) return;   // reste en modification
        var session = _context!.Session;
        if (user.Session != session)
        {
            user.CancelEdit();
            _output.Warning("Ce message n'est plus dans la conversation en cours.");
            return;
        }
        int index = Math.Min(user.Index, session.Messages.Count);
        session.Messages.RemoveRange(index, session.Messages.Count - index);
        if (session.Messages.Count > 0) SessionStore.Save(session);
        else SessionStore.Delete(session.Id);
        _chat.RemoveFrom(user);
        OnSubmitted(text);
    }

    private bool InCurrentSession(MessageControl message, string role) =>
        message.Session is { } session && session == _context?.Session
        && message.Index < session.Messages.Count && session.Messages[message.Index].Role == role;

    private async void OnNewConversation()
    {
        if (!Ready) return;
        _output.Clear(_context!);
        await RunCommandAsync("/new");
    }

    private async void OnSessionChosen(Session session)
    {
        if (!Ready || session.Id == _context!.Session.Id) return;
        _output.Clear(_context);
        await RunAsync(() => ResumeCommand.Resume(_context, session));
    }

    private void OnRenameSession(Session listed)
    {
        if (!Ready) return;
        var session = Current(listed);
        string name = PromptDialog.Ask(this, "Nouveau nom de la conversation :", Sidebar.TitleOf(session));
        if (name.Length == 0) return;
        session.Title = name;
        SessionStore.Save(session);
        RefreshViews();
    }

    private void OnDeleteSession(Session listed)
    {
        if (!Ready) return;
        if (!PromptDialog.Confirm(this, $"Supprimer définitivement la conversation « {Sidebar.TitleOf(listed)} » ?")) return;
        SessionStore.Delete(listed.Id);
        if (listed.Id == _context!.Session.Id)
        {
            _output.Clear(_context);
            _context.NewSession();
        }
        RefreshViews();
    }

    private void OnExportSession(Session listed, string extension)
    {
        var session = Current(listed);
        string title = Sidebar.TitleOf(session);
        bool markdown = extension == ".md";
        using var dialog = new SaveFileDialog
        {
            Title = "Exporter la conversation",
            FileName = string.Concat(title.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)) + extension,
            Filter = markdown ? "Markdown (*.md)|*.md" : "Texte (*.txt)|*.txt",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            File.WriteAllText(dialog.FileName, Export(session, title, markdown));
            _output.Success($"Conversation exportée : {dialog.FileName}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _output.Error($"Export impossible : {e.Message}");
        }
    }

    /// <summary>La conversation en texte ("> question", "● réponse") ou en Markdown.</summary>
    private static string Export(Session session, string title, bool markdown)
    {
        var text = new StringBuilder();
        string about = $"{session.Date:g} · {session.Model}";
        text.AppendLine(markdown ? $"# {title}" : title).AppendLine();
        text.AppendLine(markdown ? $"*{about}*" : about).AppendLine();
        foreach (var message in session.Messages)
        {
            bool user = message.Role == ChatMessage.UserRole;
            if (markdown)
                text.AppendLine(user ? "**Moi**" : "**MiniChat**").AppendLine().AppendLine(message.Content).AppendLine();
            else
                text.AppendLine($"{(user ? Theme.Prompt : Theme.ReplyBullet)} {message.Content}").AppendLine();
        }
        return text.ToString();
    }

    /// <summary>La conversation de la liste (lue sur le disque), ou l'objet en mémoire si c'est celle en cours.</summary>
    private Session Current(Session listed) => listed.Id == _context?.Session.Id ? _context.Session : listed;

    private async void OnModelChosen(string name)
    {
        if (!Ready || name == _context!.ModelName) return;
        await RunCommandAsync($"/model \"{name}\"");
    }

    private void OnFolderClicked()
    {
        if (!Ready) return;
        using var dialog = new FolderBrowserDialog { SelectedPath = _context!.WorkingDirectory, Description = "Dossier de travail" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _context.WorkingDirectory = dialog.SelectedPath;
        _output.Success($"Dossier de travail : {dialog.SelectedPath}");
        RefreshViews();
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (!Ready || DroppedModel(e) is not { } path) return;
        string line = $"/import \"{path}\"";
        _chat.Command(line);
        await RunCommandAsync(line);
    }

    // --- Le travail en fond -----------------------------------------------------------------------------------

    private bool Ready => !_busy && _context is not null;

    private Task RunCommandAsync(string line) => RunAsync(() =>
    {
        _context!.Registry.Execute(_context, line);
        _output.Line(Tone.Text, "");
    });

    /// <summary>
    /// generate écrit la réponse dans reply au fur et à mesure : les caractères arrivent du fil de génération
    /// par IProgress, qui les remet au fil de l'interface dans l'ordre. Envoyer devient Stop pendant ce temps.
    /// </summary>
    /// <returns>La réponse (peut-être coupée par Stop), null si la génération a échoué.</returns>
    private async Task<string?> GenerateAsync(MessageControl reply, Func<Action<char>, CancellationToken, string> generate)
    {
        using var cancellation = _reply = new CancellationTokenSource();
        bool any = false;
        var progress = new Progress<char>(c =>
        {
            if (!any)
            {
                reply.BeginReply();
                _status.SetBusy("Réponse... (Échap pour arrêter)");
            }
            any = true;
            reply.AppendReply(c.ToString());
        });

        string text = "";
        _input.Generating = true;
        bool ok = await RunAsync(() => _output.Busy("Réflexion... (Échap pour arrêter)",
            () => text = generate(((IProgress<char>)progress).Report, cancellation.Token)));
        _input.Generating = false;
        _reply = null;

        // Les derniers caractères postés par IProgress passent avant cette suite : la réponse est complète.
        if (!ok && !any) return null;
        if (!any) reply.BeginReply();
        if (!any && !cancellation.IsCancellationRequested)
            reply.Note("(pas de réponse)");
        if (cancellation.IsCancellationRequested)
            reply.Note(any ? " (interrompu)" : "(interrompu)");
        if (ok && _context!.Chat?.Generator.LastStats is { Tokens: > 0 } stats)
            reply.SetStats($"{stats.Tokens} token{(stats.Tokens > 1 ? "s" : "")} · {stats.TokensPerSecond:0} tokens/s");
        return ok ? text : null;
    }

    /// <summary>Lance le travail en fond ; une erreur est écrite dans la conversation. False si elle a échoué.</summary>
    private async Task<bool> RunAsync(Action work)
    {
        if (_busy) return false;
        SetBusy(true);
        // L'erreur est attrapée dans la tâche même : si elle en sortait, le débogueur de Visual Studio
        // s'arrêterait dessus ("non gérée par le code utilisateur"), même pour une simple faute de frappe.
        Exception? error = await Task.Run(() =>
        {
            try { work(); return null; }
            catch (Exception e) { return e; }
        });
        SetBusy(false);
        RefreshViews();

        if (error is not null)
        {
            _output.Error(error is UserException ? error.Message : $"{error.GetType().Name} : {error.Message}");
            _output.Line(Tone.Text, "");
        }
        if (_context?.QuitRequested == true) Close();
        return error is null;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _input.Busy = busy || _context is null;
    }

    /// <summary>En-tête, colonne de gauche et barre d'état d'après l'état du CommandContext.</summary>
    private void RefreshViews()
    {
        if (_context is not { } context) return;

        string device = context.OnGpu ? "GPU NVIDIA (CUDA)" : "CPU";
        string model = context.Chat is { } chat
            ? $"{context.ModelName}  ·  {chat.Generator.Model.ParameterCount / 1e6:0.0} M paramètres  ·  {ModelsCommand.Describe(chat.Generator.Model.Config)}"
            : "Aucun modèle — /import <fichier.nelmodel>, ou glisse un .nelmodel ici";
        _chat.SetHeader(model, device, context.OnGpu, context.WorkingDirectory);

        string speed = context.Chat?.Generator.LastStats is { } stats ? $"{stats.TokensPerSecond:0} tokens/s" : "— tokens/s";
        string window = "contexte —";
        if (context.Chat is { } engine && !_busy)   // pas pendant une réponse : le tokenizer y sert déjà
        {
            var (firstTurn, tokens) = engine.Context(context.Session.Turns, _settings.MaxReplyTokens);
            window = $"contexte {tokens}/{engine.BlockSize}";
            _chat.FadeBefore(context.Session, firstTurn * 2);   // deux messages par tour
        }
        _status.SetInfo(context.ModelName ?? "aucun modèle", device, speed, window, $"température {_settings.Temperature}");

        _sidebar.ShowSessions(SessionStore.List(), context.Session.Id);
        _sidebar.ShowModels(ModelLibrary.List(), context.ModelName);
    }

    // --- Glisser-déposer d'un .nelmodel -----------------------------------------------------------------------

    /// <summary>
    /// Chaque contrôle doit accepter le dépôt lui-même : le dépôt ne remonte pas au parent. Aussi ceux ajoutés
    /// plus tard (les messages).
    /// </summary>
    private void AcceptDrops(Control control)
    {
        control.AllowDrop = true;
        control.DragEnter += (_, e) => e.Effect = DroppedModel(e) is not null ? DragDropEffects.Copy : DragDropEffects.None;
        control.DragDrop += OnDrop;
        control.ControlAdded += (_, e) => AcceptDrops(e.Control!);
        foreach (Control child in control.Controls)
            AcceptDrops(child);
    }

    private static string? DroppedModel(DragEventArgs e) =>
        e.Data?.GetData(DataFormats.FileDrop) is string[] files
            ? files.FirstOrDefault(f => f.EndsWith(NelModelPackage.Extension, StringComparison.OrdinalIgnoreCase))
            : null;
}
