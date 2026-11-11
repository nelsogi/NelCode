namespace NelCode.Desktop.Controls;

/// <summary>
/// La barre d'état :  ⠋ Chargement...      v2 · GPU NVIDIA · 41 tokens/s · contexte 180/256 · température 0.8
/// L'indicateur à gauche tourne pendant un travail long (chargement, réponse).
/// </summary>
internal sealed class StatusBar : UserControl
{
    private static readonly string[] Frames = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];

    private readonly Label _busy = new() { Dock = DockStyle.Left, AutoSize = true, ForeColor = Theme.Accent, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label _info = new() { Dock = DockStyle.Right, AutoSize = true, ForeColor = Theme.Dim, TextAlign = ContentAlignment.MiddleRight };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 80 };
    private string _status = "";
    private int _frame;

    public StatusBar()
    {
        BackColor = Theme.Sidebar;
        Font = Theme.Small;
        Height = 26;
        Padding = new Padding(10, 5, 10, 0);
        Controls.Add(_busy);
        Controls.Add(_info);
        _timer.Tick += (_, _) => _busy.Text = $"{Frames[_frame = (_frame + 1) % Frames.Length]} {_status}";
    }

    public void SetInfo(params string[] parts) => _info.Text = string.Join("  ·  ", parts);

    /// <summary>Lance l'indicateur avec ce texte, ou l'arrête (null).</summary>
    public void SetBusy(string? status)
    {
        _status = status ?? "";
        _timer.Enabled = status is not null;
        _busy.Text = status is null ? "" : $"{Frames[_frame]} {status}";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}
