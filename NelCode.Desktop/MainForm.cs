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
    private CancellationTokenSource? _reply;   // la réponse en cours, qu'Échap arrête

    public MainForm(string workingDirectory)
    {
        _workingDirectory = workingDirectory;
        _input = new InputBox(_registry.Commands) { Dock = DockStyle.Bottom, Busy = true };
        _output = new DesktopOutput(this, _chat, _status);

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
        _sidebar.NewClicked += OnNewConversation;
        _sidebar.SessionChosen += OnSessionChosen;
        _sidebar.ModelChosen += OnModelChosen;
        _chat.FolderClicked += OnFolderClicked;
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

        _chat.User(line);
        if (line.StartsWith('/'))
            await RunCommandAsync(line);
        else
            await ReplyAsync(line);
    }

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
        _chat.User(line);
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
    /// La réponse s'écrit au fur et à mesure : les caractères arrivent du fil de génération par IProgress,
    /// qui les remet au fil de l'interface dans l'ordre.
    /// </summary>
    private async Task ReplyAsync(string message)
    {
        using var reply = _reply = new CancellationTokenSource();
        bool started = false, any = false;
        var progress = new Progress<char>(c =>
        {
            if (!any) _status.SetBusy("Réponse... (Échap pour arrêter)");
            any = true;
            _chat.Write(c.ToString(), Theme.Text);
        });

        bool ok = await RunAsync(() =>
        {
            _context!.RequireChat();   // pas de modèle : l'erreur seule, sans "●"
            _output.Busy("Réflexion... (Échap pour arrêter)", () =>
            {
                _chat.Invoke(() => _chat.Write($"{Theme.ReplyBullet} ", Theme.Accent));
                started = true;
                _context.SendMessage(message, ((IProgress<char>)progress).Report, reply.Token);
            });
        });
        _reply = null;

        // Les derniers caractères postés par IProgress passent avant cette suite : la réponse est complète.
        if (!started) return;
        if (!any && !reply.IsCancellationRequested && ok)
            _chat.Write("(pas de réponse)", Theme.Dim);
        if (reply.IsCancellationRequested)
            _chat.Write(" (interrompu)", Theme.Dim);
        _chat.Write("\n", Theme.Text);
        if (ok && _context!.Chat?.Generator.LastStats is { Tokens: > 0 } stats)
            _chat.WriteLine($"  {stats.Tokens} token{(stats.Tokens > 1 ? "s" : "")} · {stats.TokensPerSecond:0} tokens/s", Theme.Dim);
        _chat.Write("\n", Theme.Text);
    }

    /// <summary>Lance le travail en fond ; une erreur est écrite dans la conversation. False si elle a échoué.</summary>
    private async Task<bool> RunAsync(Action work)
    {
        if (_busy) return false;
        SetBusy(true);
        try
        {
            await Task.Run(work);
            return true;
        }
        catch (UserException e)
        {
            _output.Error(e.Message);
        }
        catch (Exception e)
        {
            _output.Error($"{e.GetType().Name} : {e.Message}");
        }
        finally
        {
            SetBusy(false);
            RefreshViews();
            if (_context?.QuitRequested == true) Close();
        }
        _output.Line(Tone.Text, "");
        return false;
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

    /// <summary>Chaque contrôle doit accepter le dépôt lui-même : le dépôt ne remonte pas au parent.</summary>
    private void AcceptDrops(Control control)
    {
        control.AllowDrop = true;
        control.DragEnter += (_, e) => e.Effect = DroppedModel(e) is not null ? DragDropEffects.Copy : DragDropEffects.None;
        control.DragDrop += OnDrop;
        foreach (Control child in control.Controls)
            AcceptDrops(child);
    }

    private static string? DroppedModel(DragEventArgs e) =>
        e.Data?.GetData(DataFormats.FileDrop) is string[] files
            ? files.FirstOrDefault(f => f.EndsWith(NelModelPackage.Extension, StringComparison.OrdinalIgnoreCase))
            : null;
}
