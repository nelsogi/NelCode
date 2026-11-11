using System.Text.Json.Serialization;

namespace NelCode.Storage;

/// <summary>Un message : Role = "user" ou "assistant".</summary>
public sealed record ChatMessage(string Role, string Content)
{
    public const string UserRole = "user";
    public const string AssistantRole = "assistant";

    /// <summary>Les versions d'une réponse régénérée (Content = celle affichée). Null : une seule version.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Versions { get; init; }
}

/// <summary>
/// Une conversation sauvegardée, un fichier JSON par conversation :
///   { "id": "2026-11-10_14-03-22", "model": "v1", "workingDirectory": "C:\\...", "date": "...",
///     "messages": [ { "role": "user", "content": "Bonjour" }, { "role": "assistant", "content": "..." } ] }
/// Les messages vont par paires (question, réponse) : une réponse interrompue par Échap est gardée telle quelle.
/// </summary>
public sealed class Session
{
    public string Id { get; set; } = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff");
    public string Model { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";

    /// <summary>Le nom donné par l'utilisateur. Null : titre tiré du premier message.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; set; }

    /// <summary>Dernier message (mis à jour à chaque sauvegarde).</summary>
    public DateTime Date { get; set; } = DateTime.Now;

    public List<ChatMessage> Messages { get; set; } = [];

    /// <summary>Les tours (question, réponse), dans le format attendu par ChatEngine.</summary>
    [JsonIgnore]
    public List<(string User, string Reply)> Turns =>
        Messages.Chunk(2)
            .Where(pair => pair.Length == 2 && pair[0].Role == ChatMessage.UserRole && pair[1].Role == ChatMessage.AssistantRole)
            .Select(pair => (pair[0].Content, pair[1].Content))
            .ToList();

    public void AddTurn(string user, string reply)
    {
        Messages.Add(new ChatMessage(ChatMessage.UserRole, user.Trim()));
        Messages.Add(new ChatMessage(ChatMessage.AssistantRole, reply.Trim()));
        Date = DateTime.Now;
    }
}
