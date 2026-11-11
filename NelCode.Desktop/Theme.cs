using System.Runtime.InteropServices;
using NelCode.Commands;
using NelCode.Storage;

namespace NelCode.Desktop;

/// <summary>
/// Les couleurs, les polices et les symboles de Nel Code Desktop, à un seul endroit. Le thème (sombre ou clair)
/// est lu dans settings.json au démarrage : en changer demande de relancer la fenêtre.
/// </summary>
internal static class Theme
{
    // En premier : les couleurs ci-dessous en dépendent (initialisées dans l'ordre du fichier).
    public static readonly bool IsLight = Settings.Load(out _).Theme == "light";

    public static readonly Color Background = Pick(0x1E1E1E, 0xFFFFFF);   // la conversation
    public static readonly Color Sidebar = Pick(0x181818, 0xF3F3F3);      // liste de gauche, barre d'état
    public static readonly Color Surface = Pick(0x252526, 0xECECEC);      // zone de saisie, boutons, dialogues
    public static readonly Color Hover = Pick(0x2F2F31, 0xDDDDE0);        // élément sélectionné ou survolé
    public static readonly Color Border = Pick(0x3C3C3C, 0xCCCCCC);
    public static readonly Color Found = Pick(0x5C4A1E, 0xFFE58F);        // un résultat de Ctrl+F
    public static readonly Color FoundCurrent = Pick(0x9A6A12, 0xFFB938); // le résultat choisi

    public static readonly Color Text = Pick(0xD4D4D4, 0x1F1F1F);
    public static readonly Color User = Pick(0xB3B3B3, 0x4A4A4A);
    public static readonly Color Dim = Pick(0x808080, 0x737373);
    public static readonly Color Faded = Pick(0x5A5A5A, 0xB0B0B0);        // messages que le modèle ne voit plus
    public static readonly Color Accent = Pick(0xD97757, 0xC0582F);
    public static readonly Color Command = Pick(0xE0A87E, 0x9C4F1E);
    public static readonly Color Success = Pick(0x6CC67A, 0x2E7D32);
    public static readonly Color Warning = Pick(0xE5C07B, 0x8A6100);
    public static readonly Color Error = Pick(0xF14C4C, 0xC62828);

    public const string Logo = "✻";
    public const string Prompt = ">";
    public const string ReplyBullet = "●";

    // Cascadia Mono est livrée avec Windows Terminal ; sans elle, Consolas.
    private static readonly string MonoFamily =
        FontFamily.Families.Any(f => f.Name == "Cascadia Mono") ? "Cascadia Mono" : "Consolas";

    public static readonly Font Font = new(MonoFamily, 10f);
    public static readonly Font Small = new(MonoFamily, 9f);
    public static readonly Font Bold = new(MonoFamily, 10f, FontStyle.Bold);

    /// <summary>L'icône de l'exécutable (ApplicationIcon), pour les fenêtres.</summary>
    public static readonly Icon? AppIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

    public static Color Of(Tone tone) => tone switch
    {
        Tone.Dim => Dim,
        Tone.Accent => Accent,
        Tone.Command => Command,
        Tone.User => User,
        Tone.Success => Success,
        Tone.Warning => Warning,
        Tone.Error => Error,
        _ => Text,
    };

    /// <summary>
    /// Barre de titre aux couleurs du thème (Windows 10 20H1 et plus pour le mode sombre ; Windows 11 : aussi
    /// sa couleur exacte, même si « couleur d'accentuation sur les barres de titre » est activé).
    /// </summary>
    public static void DarkTitleBar(Form form)
    {
        int dark = IsLight ? 0 : 1;
        DwmSetWindowAttribute(form.Handle, DwmUseImmersiveDarkMode, ref dark, sizeof(int));
        int caption = ColorTranslator.ToWin32(Sidebar);
        DwmSetWindowAttribute(form.Handle, DwmCaptionColor, ref caption, sizeof(int));
        int text = ColorTranslator.ToWin32(Text);
        DwmSetWindowAttribute(form.Handle, DwmTextColor, ref text, sizeof(int));
    }

    /// <summary>Barres de défilement sombres pour une liste ou un texte (thème sombre seulement).</summary>
    public static void DarkScrollBars(Control control)
    {
        if (!IsLight)
            control.HandleCreated += (_, _) => SetWindowTheme(control.Handle, "DarkMode_Explorer", null);
    }

    /// <summary>Un bouton plat dans les couleurs du thème.</summary>
    public static Button Button(string text)
    {
        var button = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = Surface,
            ForeColor = Text,
            Font = Font,
            AutoSize = true,
            Cursor = Cursors.Hand,
        };
        button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.MouseOverBackColor = Hover;
        return button;
    }

    /// <summary>Un menu (clic droit) aux couleurs du thème.</summary>
    public static ContextMenuStrip Menu() => new()
    {
        Renderer = new ToolStripProfessionalRenderer(new MenuColors()),
        BackColor = Surface,
        ForeColor = Text,
        Font = Small,
        ShowImageMargin = false,
    };

    /// <summary>Une fenêtre de dialogue aux couleurs du thème (réglages, palette, questions).</summary>
    public static void Dialog(Form form, int width, int height)
    {
        form.AutoScaleDimensions = new SizeF(96F, 96F);
        form.AutoScaleMode = AutoScaleMode.Dpi;
        form.Text = "Nel Code";
        form.FormBorderStyle = FormBorderStyle.FixedDialog;
        form.StartPosition = FormStartPosition.CenterParent;
        form.MinimizeBox = form.MaximizeBox = form.ShowInTaskbar = form.ShowIcon = false;
        form.ClientSize = new Size(width, height);
        form.BackColor = Background;
        form.ForeColor = Text;
        form.Font = Font;
        form.HandleCreated += (_, _) => DarkTitleBar(form);
    }

    private static Color Pick(int dark, int light) => Color.FromArgb(unchecked((int)0xFF000000) | (IsLight ? light : dark));

    private sealed class MenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Surface;
        public override Color MenuBorder => Border;
        public override Color MenuItemSelected => Hover;
        public override Color MenuItemBorder => Hover;
        public override Color SeparatorDark => Border;
        public override Color SeparatorLight => Surface;
    }

    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmCaptionColor = 35;
    private const int DwmTextColor = 36;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string appName, string? idList);
}
