using System.Runtime.InteropServices;
using NelCode.Commands;

namespace NelCode.Desktop;

/// <summary>Les couleurs, les polices et les symboles de Nel Code Desktop, à un seul endroit (thème sombre).</summary>
internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(0x1E, 0x1E, 0x1E);   // la conversation
    public static readonly Color Sidebar = Color.FromArgb(0x18, 0x18, 0x18);      // liste de gauche, barre d'état
    public static readonly Color Surface = Color.FromArgb(0x25, 0x25, 0x26);      // zone de saisie, boutons, dialogues
    public static readonly Color Hover = Color.FromArgb(0x2F, 0x2F, 0x31);        // élément sélectionné ou survolé
    public static readonly Color Border = Color.FromArgb(0x3C, 0x3C, 0x3C);

    public static readonly Color Text = Color.FromArgb(0xD4, 0xD4, 0xD4);
    public static readonly Color User = Color.FromArgb(0xB3, 0xB3, 0xB3);
    public static readonly Color Dim = Color.FromArgb(0x80, 0x80, 0x80);
    public static readonly Color Accent = Color.FromArgb(0xD9, 0x77, 0x57);
    public static readonly Color Command = Color.FromArgb(0xE0, 0xA8, 0x7E);
    public static readonly Color Success = Color.FromArgb(0x6C, 0xC6, 0x7A);
    public static readonly Color Warning = Color.FromArgb(0xE5, 0xC0, 0x7B);
    public static readonly Color Error = Color.FromArgb(0xF1, 0x4C, 0x4C);

    public const string Logo = "✻";
    public const string Prompt = ">";
    public const string ReplyBullet = "●";

    // Cascadia Mono est livrée avec Windows Terminal ; sans elle, Consolas.
    private static readonly string MonoFamily =
        FontFamily.Families.Any(f => f.Name == "Cascadia Mono") ? "Cascadia Mono" : "Consolas";

    public static readonly Font Font = new(MonoFamily, 10f);
    public static readonly Font Small = new(MonoFamily, 9f);
    public static readonly Font Bold = new(MonoFamily, 10f, FontStyle.Bold);

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
    /// Barre de titre sombre (Windows 10 20H1 et plus). Windows 11 : aussi sa couleur exacte, même si
    /// « couleur d'accentuation sur les barres de titre » est activé. Ailleurs, sans effet.
    /// </summary>
    public static void DarkTitleBar(Form form)
    {
        int on = 1;
        DwmSetWindowAttribute(form.Handle, DwmUseImmersiveDarkMode, ref on, sizeof(int));
        int caption = ColorTranslator.ToWin32(Sidebar);
        DwmSetWindowAttribute(form.Handle, DwmCaptionColor, ref caption, sizeof(int));
        int text = ColorTranslator.ToWin32(Text);
        DwmSetWindowAttribute(form.Handle, DwmTextColor, ref text, sizeof(int));
    }

    /// <summary>Barres de défilement sombres pour une liste ou un texte.</summary>
    public static void DarkScrollBars(Control control) =>
        control.HandleCreated += (_, _) => SetWindowTheme(control.Handle, "DarkMode_Explorer", null);

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

    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmCaptionColor = 35;
    private const int DwmTextColor = 36;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string appName, string? idList);
}
