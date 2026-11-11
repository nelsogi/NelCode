namespace NelCode.Desktop;

/// <summary>Les questions posées pendant une commande (IOutput.Confirm, Choose, Ask), en petite fenêtre sombre.</summary>
internal sealed class PromptDialog : Form
{
    private readonly FlowLayoutPanel _buttons = new()
    {
        Dock = DockStyle.Bottom,
        AutoSize = true,
        FlowDirection = FlowDirection.RightToLeft,
        Padding = new Padding(0, 10, 0, 0),
    };
    private readonly Panel _body = new() { Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 0) };

    private PromptDialog(string question, int height)
    {
        Theme.Dialog(this, 560, height);
        Padding = new Padding(16);

        var label = new Label { Text = question, Dock = DockStyle.Top, AutoSize = true, MaximumSize = new Size(528, 0) };
        Controls.Add(_body);
        Controls.Add(_buttons);
        Controls.Add(label);
    }

    /// <summary>Un texte et OK (À propos).</summary>
    public static void Inform(IWin32Window owner, string title, string text)
    {
        using var dialog = new PromptDialog(text, 90 + Theme.Font.Height * text.Split('\n').Length);
        dialog.Text = title;
        dialog.AcceptButton = dialog.CancelButton = dialog.AddButton("OK", DialogResult.OK);
        dialog.ShowDialog(owner);
    }

    public static bool Confirm(IWin32Window owner, string question)
    {
        using var dialog = new PromptDialog(question, 120);
        dialog.CancelButton = dialog.AddButton("Non", DialogResult.No);
        dialog.AcceptButton = dialog.AddButton("Oui", DialogResult.Yes);
        dialog.ActiveControl = dialog._buttons.Controls[0];   // non par défaut
        return dialog.ShowDialog(owner) == DialogResult.Yes;
    }

    /// <returns>Le numéro du choix, -1 si la fenêtre est fermée.</returns>
    public static int Choose(IWin32Window owner, string question, IReadOnlyList<string> choices)
    {
        using var dialog = new PromptDialog(question, 160 + Math.Min(choices.Count, 10) * 24);
        var list = new ListBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
            IntegralHeight = false,
        };
        Theme.DarkScrollBars(list);
        list.Items.AddRange(choices.ToArray<object>());
        list.SelectedIndex = 0;
        list.DoubleClick += (_, _) => dialog.DialogResult = DialogResult.OK;
        dialog._body.Controls.Add(list);
        dialog.CancelButton = dialog.AddButton("Annuler", DialogResult.Cancel);
        dialog.AcceptButton = dialog.AddButton("OK", DialogResult.OK);
        dialog.ActiveControl = list;
        return dialog.ShowDialog(owner) == DialogResult.OK ? list.SelectedIndex : -1;
    }

    /// <returns>Le texte tapé, "" si annulé.</returns>
    public static string Ask(IWin32Window owner, string question, string initial = "")
    {
        using var dialog = new PromptDialog(question, 150);
        var text = new TextBox { Dock = DockStyle.Top, BorderStyle = BorderStyle.FixedSingle, BackColor = Theme.Surface, ForeColor = Theme.Text, Text = initial };
        text.SelectAll();
        dialog._body.Controls.Add(text);
        dialog.CancelButton = dialog.AddButton("Annuler", DialogResult.Cancel);
        dialog.AcceptButton = dialog.AddButton("OK", DialogResult.OK);
        dialog.ActiveControl = text;
        return dialog.ShowDialog(owner) == DialogResult.OK ? text.Text.Trim() : "";
    }

    private Button AddButton(string text, DialogResult result)
    {
        var button = Theme.Button(text);
        button.DialogResult = result;
        button.MinimumSize = new Size(90, 30);
        button.Margin = new Padding(8, 0, 0, 0);
        _buttons.Controls.Add(button);
        return button;
    }
}
