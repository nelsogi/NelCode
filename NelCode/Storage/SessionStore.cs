using System.Text.Json;

namespace NelCode.Storage;

/// <summary>Les conversations, une par fichier dans %APPDATA%\NelCode\sessions\{id}.json.</summary>
public static class SessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,   // accents lisibles
    };

    public static void Save(Session session)
    {
        Directory.CreateDirectory(AppPaths.Sessions);
        string path = Path.Combine(AppPaths.Sessions, session.Id + ".json");
        // Écrit à côté puis remplace : une coupure pendant l'écriture ne perd pas la conversation.
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(session, JsonOptions));
        File.Move(path + ".tmp", path, overwrite: true);
    }

    /// <summary>De la plus récente à la plus ancienne. Un fichier illisible est ignoré.</summary>
    public static List<Session> List()
    {
        if (!Directory.Exists(AppPaths.Sessions))
            return [];

        var sessions = new List<Session>();
        foreach (string path in Directory.GetFiles(AppPaths.Sessions, "*.json"))
        {
            try
            {
                if (JsonSerializer.Deserialize<Session>(File.ReadAllText(path), JsonOptions) is { } session)
                    sessions.Add(session);
            }
            catch (JsonException) { }
        }
        return sessions.OrderByDescending(s => s.Date).ToList();
    }
}
