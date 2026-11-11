using NelCode.Storage;

namespace NelCode.Desktop;

/// <summary>
/// Les réglages (Ctrl+, ou ⚙) : un curseur par réglage de génération, avec son explication, et le thème.
/// OK les écrit dans settings.json, partagé avec la console. Seuls les curseurs déplacés changent leur
/// réglage : une température de 0,83 tapée avec /temp n'est pas arrondie en ouvrant la fenêtre.
/// </summary>
internal sealed class SettingsDialog : Form
{
    private const int RowWidth = 480;

    private sealed record Slider(TrackBar Bar, int Start, Func<Settings, double> Get, Action<Settings, double> Set, double Step)
    {
        public int Position(double value) => Math.Clamp((int)Math.Round(value / Step), Bar.Minimum, Bar.Maximum);
    }

    private readonly FlowLayoutPanel _rows = new()
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
    };
    private readonly List<Slider> _sliders = [];
    private readonly RadioButton _dark = Radio("Sombre"), _light = Radio("Clair");

    private SettingsDialog(Settings settings)
    {
        Theme.Dialog(this, RowWidth + 40, 580);
        Padding = new Padding(20, 6, 20, 16);

        AddSlider(settings, "Température", s => s.Temperature, (s, v) => s.Temperature = v, 0.1, 2.0, 0.05,
            v => $"{v:0.00}",
            "Plus bas : réponses sages, parfois répétitives. Plus haut : plus créatives, mais plus souvent incohérentes.");
        AddSlider(settings, "Top-k", s => s.TopK, (s, v) => s.TopK = (int)v, 0, Math.Max(100, settings.TopK), 1,
            v => v == 0 ? "pas de limite" : $"{v:0}",
            "Tirage parmi les K tokens les plus probables. 0 : tous les tokens.");
        AddSlider(settings, "Top-p", s => s.TopP, (s, v) => s.TopP = v, 0.05, 1.0, 0.05,
            v => v >= 1 ? "pas de limite" : $"{v:0.00}",
            "Tirage parmi les tokens qui font cette part des chances. 1 : pas de limite.");
        AddSlider(settings, "Longueur max des réponses", s => s.MaxReplyTokens, (s, v) => s.MaxReplyTokens = (int)v, 16, 128, 8,
            v => $"{v:0} tokens",
            "Environ 3 caractères par token. Ce qui est réservé à la réponse ne sert plus à voir la conversation.");

        _rows.Controls.Add(Title("Thème", ""));
        var themes = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 0, 0, 2) };
        themes.Controls.AddRange([_dark, _light]);
        _rows.Controls.Add(themes);
        _rows.Controls.Add(Explanation("Appliqué en relançant la fenêtre."));
        (settings.Theme == "light" ? _light : _dark).Checked = true;

        var cancel = Button("Annuler", DialogResult.Cancel);
        var ok = Button("OK", DialogResult.OK);
        var defaults = Button("Valeurs par défaut", DialogResult.None);
        defaults.Click += (_, _) =>
        {
            var initial = new Settings();
            foreach (var slider in _sliders)
                slider.Bar.Value = slider.Position(slider.Get(initial));
            _dark.Checked = true;
        };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.AddRange([cancel, ok, defaults]);
        AcceptButton = ok;
        CancelButton = cancel;

        Controls.Add(_rows);
        Controls.Add(buttons);
    }

    /// <summary>Ouvre les réglages ; OK les enregistre. True si le thème a changé.</summary>
    public static bool Edit(IWin32Window owner, Settings settings)
    {
        using var dialog = new SettingsDialog(settings);
        if (dialog.ShowDialog(owner) != DialogResult.OK) return false;

        foreach (var slider in dialog._sliders)
            if (slider.Bar.Value != slider.Start)
                slider.Set(settings, Math.Round(slider.Bar.Value * slider.Step, 2));
        string theme = dialog._light.Checked ? "light" : "dark";
        bool themeChanged = theme != settings.Theme;
        settings.Theme = theme;
        settings.Save();
        return themeChanged;
    }

    private void AddSlider(Settings settings, string name, Func<Settings, double> get, Action<Settings, double> set,
        double min, double max, double step, Func<double, string> format, string explanation)
    {
        var bar = new TrackBar
        {
            Minimum = (int)Math.Round(min / step),
            Maximum = (int)Math.Round(max / step),
            TickStyle = TickStyle.None,
            Width = RowWidth,
            BackColor = Theme.Background,
            Margin = Padding.Empty,
        };
        var slider = new Slider(bar, 0, get, set, step);
        bar.Value = slider.Position(get(settings));
        _sliders.Add(slider with { Start = bar.Value });

        var title = Title(name, format(get(settings)));
        bar.ValueChanged += (_, _) => title.Controls[1].Text = format(bar.Value * step);
        _rows.Controls.Add(title);
        _rows.Controls.Add(bar);
        _rows.Controls.Add(Explanation(explanation));
    }

    /// <summary>Le nom à gauche, la valeur à droite.</summary>
    private static Panel Title(string name, string value)
    {
        var row = new Panel { Width = RowWidth, Height = Theme.Bold.Height + 4, Margin = new Padding(0, 12, 0, 0) };
        row.Controls.Add(new Label { Text = name, Dock = DockStyle.Left, AutoSize = true, Font = Theme.Bold });
        row.Controls.Add(new Label { Text = value, Dock = DockStyle.Right, AutoSize = true, ForeColor = Theme.Accent });
        return row;
    }

    private static Label Explanation(string text) => new()
    {
        Text = text,
        AutoSize = true,
        MaximumSize = new Size(RowWidth, 0),
        ForeColor = Theme.Dim,
        Font = Theme.Small,
    };

    private static RadioButton Radio(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(0, 4, 20, 0) };

    private static Button Button(string text, DialogResult result)
    {
        var button = Theme.Button(text);
        button.DialogResult = result;
        button.MinimumSize = new Size(90, 30);
        button.Margin = new Padding(8, 0, 0, 0);
        return button;
    }
}
