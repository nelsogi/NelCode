using static NelCode.Engine.BpeTokenizer;

namespace NelCode.Engine;

/// <summary>
/// Le format des conversations appris par les modèles de MiniChat :
///   &lt;|doc|&gt;&lt;|assistant|&gt;Je suis MiniChat...&lt;|end|&gt;&lt;|user|&gt;Bonjour !&lt;|end|&gt;&lt;|assistant|&gt;...&lt;|end|&gt;
/// </summary>
public static class ChatFormat
{
    /// <summary>La phrase d'identité vue à l'entraînement : à garder telle quelle.</summary>
    public const string IdentityText = "Je suis MiniChat, un assistant qui répond en français.";

    public static readonly string Identity = DocToken + Assistant(IdentityText);

    public static string User(string text) => UserToken + text.Trim() + EndToken;

    public static string Assistant(string text) => AssistantToken + text.Trim() + EndToken;

    public static string Turn(string user, string reply) => User(user) + Assistant(reply);
}
