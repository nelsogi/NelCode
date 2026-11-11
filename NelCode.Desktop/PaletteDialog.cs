using System.Globalization;

namespace NelCode.Desktop;

/// <summary>Une entrée de la palette : son nom, à droite son raccourci ou sa description, et ce qu'elle fait.</summary>
internal sealed record PaletteItem(string Label, string Hint, Action Run);

/// <summary>
/// La palette de commandes (Ctrl+K), comme dans VS Code : une recherche en haut de la fenêtre, la liste filtrée
/// dessous. ↑↓ choisissent, Entrée lance, Échap ou un clic ailleurs ferme.
/// </summary>
internal sealed class PaletteDialog : Form
{
    private const int MaxShown = 12;

    private readonly IReadOnlyList<PaletteItem> _items;
    private readonly TextBox _search = new()
    {
        Dock = DockStyle.Top,
        BorderStyle = BorderStyle.None,
        BackColor = Theme.Surface,
        ForeColor = Theme.Text,
        Font = Theme.Font,
        PlaceholderText = "Rechercher une commande...",
    };
    private readonly ListBox _list = new()
    {
        Dock = DockStyle.Fill,
        BorderStyle = BorderStyle.None,
        BackColor = Theme.Surface,
        ForeColor = Theme.Text,
        Font = Theme.Font,
        DrawMode = DrawMode.OwnerDrawFixed,
        IntegralHeight = false,
    };
    private PaletteItem? _chosen;

    private PaletteDialog(Form owner, IReadOnlyList<PaletteItem> items)
    {
        _items = items;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Theme.Border;   // le cadre d'un pixel
        Padding = new Padding(1);
        _list.ItemHeight = Theme.Font.Height + 8;
        Theme.DarkScrollBars(_list);
        int width = Math.Min(620, owner.ClientSize.Width - 40);
        Size = new Size(width, _list.ItemHeight * MaxShown + 44);
        var top = owner.PointToScreen(Point.Empty);
        Location = new Point(top.X + (owner.ClientSize.Width - width) / 2, top.Y + 40);

        var box = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Theme.Surface, Padding = new Padding(12, 10, 12, 0) };
        box.Controls.Add(_search);
        var rule = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Theme.Border };
        Controls.Add(_list);
        Controls.Add(rule);
        Controls.Add(box);

        _search.TextChanged += (_, _) => Filter();
        _search.KeyDown += OnKeyDown;
        _list.DrawItem += DrawItem;
        _list.MouseClick += (_, _) => Choose();
        Deactivate += (_, _) => Close();
        Filter();
    }

    /// <summary>Ouvre la palette ; l'entrée choisie est lancée après sa fermeture.</summary>
    public static void Show(Form owner, IReadOnlyList<PaletteItem> items)
    {
        PaletteItem? chosen;
        using (var palette = new PaletteDialog(owner, items))
        {
            palette.ShowDialog(owner);
            chosen = palette._chosen;
        }
        chosen?.Run();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Up or Keys.Down when _list.Items.Count > 0:
                e.SuppressKeyPress = true;
                int delta = e.KeyCode == Keys.Up ? -1 : 1;
                _list.SelectedIndex = (_list.SelectedIndex + delta + _list.Items.Count) % _list.Items.Count;
                break;
            case Keys.Enter:
                e.SuppressKeyPress = true;
                Choose();
                break;
            case Keys.Escape:
                e.SuppressKeyPress = true;
                Close();
                break;
        }
    }

    private void Choose()
    {
        _chosen = _list.SelectedItem as PaletteItem;
        if (_chosen is not null) Close();
    }

    /// <summary>Les entrées dont le nom ou la description contient la recherche (sans casse ni accents).</summary>
    private void Filter()
    {
        string query = _search.Text.Trim();
        var compare = CultureInfo.CurrentCulture.CompareInfo;
        bool Has(string text) => compare.IndexOf(text, query, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
        _list.BeginUpdate();
        _list.Items.Clear();
        _list.Items.AddRange(_items.Where(i => Has(i.Label) || Has(i.Hint)).ToArray<object>());
        if (_list.Items.Count > 0) _list.SelectedIndex = 0;
        _list.EndUpdate();
    }

    private void DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var item = (PaletteItem)_list.Items[e.Index];
        bool selected = (e.State & DrawItemState.Selected) != 0;
        using (var background = new SolidBrush(selected ? Theme.Hover : Theme.Surface))
            e.Graphics.FillRectangle(background, e.Bounds);

        var flags = TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
        var bounds = e.Bounds with { X = e.Bounds.X + 12, Width = e.Bounds.Width - 24 };
        int labelWidth = TextRenderer.MeasureText(item.Label, Theme.Font).Width + 16;
        TextRenderer.DrawText(e.Graphics, item.Label, Theme.Font, bounds, selected ? Theme.Accent : item.Label.StartsWith('/') ? Theme.Command : Theme.Text, flags);
        TextRenderer.DrawText(e.Graphics, item.Hint, Theme.Small, bounds with { X = bounds.X + labelWidth, Width = bounds.Width - labelWidth },
            Theme.Dim, flags | TextFormatFlags.Right);
    }
}
