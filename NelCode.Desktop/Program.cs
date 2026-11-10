namespace NelCode.Desktop;

internal static class Program
{
    /// <summary>Dossier de travail : là où on lance Nel Code Desktop, ou --dir &lt;dossier&gt;.</summary>
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        string workingDirectory = Directory.GetCurrentDirectory();
        int dirIndex = Array.IndexOf(args, "--dir");
        if (dirIndex >= 0 && dirIndex + 1 < args.Length && Directory.Exists(args[dirIndex + 1]))
            workingDirectory = Path.GetFullPath(args[dirIndex + 1]);

        Application.Run(new MainForm(workingDirectory));
    }
}
