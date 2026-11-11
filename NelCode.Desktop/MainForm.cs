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

    private CommandContext? _context;          // créé en fond au démarrage (TorchSharp met un moment à s'initialiser)
    private bool _busy;
    private CancellationTokenSource? _reply;   // la réponse en cours, qu'Échap ou Stop arrête

    public MainForm(string workingDirectory)
    {
        _workingDirectory = workingDirectory;
        _input = new InputBox(_registry.Commands) { Dock = DockStyle.Bottom, Busy = true };
        _output = new DesktopOutput(this, _chat, _status, () => _context?.Session);

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Nel Code";
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
        var separator = new Panel { Dock = DockStyle.Left, Width = 1, BackColor = Theme.Border };

        // Ajouté en dernier = placé en premier : la barre d'état prend toute la largeur, puis la colonne de gauche.
        Controls.Add(main);
        Controls.Add(separator);
        Controls.Add(_sidebar);
        Controls.Add(_status);
        ResumeLayout();

        _input.Submitted += OnSubmitted;
        _input.StopClicked += () => _reply?.Cancel();
        _sidebar.NewClicked += OnNewConversation;
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

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _input.FocusText();

        var settings = Settings.Load(out string? warning);
        if (warning is not null)
            _output.Warning(warning);

        await RunAsync(() =>
        {
            var device = _output.Busy("Démarrage de TorchSharp...", () => torch.cuda.is_available() ? torch.CUDA : torch.CPU);
            _context = new CommandContext(_registry, _output, settings, device, _workingDirectory);
            _context.LoadDefaultModel();
        });
        if (_context is null)
            _output.Error("Nel Code n'a pas pu démarrer TorchSharp : rien ne peut être envoyé.");
    }

    /// <summary>Échap pendant une réponse l'arrête, où que soit le focus.</summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape && _reply is { IsCancellationRequested: false } reply)
        {
            reply.Cancel();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
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
            _context.RequireChat().Reply(turns, question, _context.Settings.Sampling, onChar, token));
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
        _status.SetInfo(context.ModelName ?? "aucun modèle", device, speed, $"température {context.Settings.Temperature}");

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
