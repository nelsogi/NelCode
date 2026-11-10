namespace NelCode.Storage;

/// <summary>Où Nel Code range ses fichiers : %APPDATA%\NelCode\{models, sessions, settings.json}.</summary>
public static class AppPaths
{
    public static readonly string Root =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NelCode");

    public static readonly string Models = Path.Combine(Root, "models");
    public static readonly string Sessions = Path.Combine(Root, "sessions");
    public static readonly string Settings = Path.Combine(Root, "settings.json");
}
