namespace NelCode.Engine;

/// <summary>
/// Transforme le générateur en chat. Le prompt (comme ChatSession dans MiniChat) :
///   identité + les derniers tours COMPLETS qui tiennent + &lt;|user|&gt;message&lt;|end|&gt;&lt;|assistant|&gt;
/// et le modèle continue jusqu'à un token spécial (&lt;|end|&gt; en principe).
/// Ne garde pas la conversation : elle est passée à chaque réponse (elle vit dans la session sauvegardée).
/// </summary>
public sealed class ChatEngine
{
    /// <summary>Longueur max d'une réponse, en tokens (~3 caractères chacun).</summary>
    public const int MaxReplyTokens = 80;

    private readonly HashSet<long> _stopTokens;

    public ChatEngine(TextGenerator generator)
    {
        Generator = generator;
        _stopTokens = generator.Tokenizer.SpecialIds.Select(id => (long)id).ToHashSet();
    }

    public TextGenerator Generator { get; }

    /// <summary>Combien de tours de la conversation tenaient dans le dernier prompt (commande /info).</summary>
    public int TurnsInLastPrompt { get; private set; }

    /// <summary>La réponse au message, caractère par caractère via onChar. Annulée : la réponse s'arrête là.</summary>
    public string Reply(IReadOnlyList<(string User, string Reply)> turns, string message, Sampling sampling,
        Action<char> onChar, CancellationToken cancellation = default)
    {
        string question = ChatFormat.User(message) + BpeTokenizer.AssistantToken;
        return Generator.Generate(BuildPrompt(turns, question), MaxReplyTokens, sampling, c =>
        {
            if (cancellation.IsCancellationRequested) return false;
            onChar(c);
            return true;
        }, _stopTokens);
    }

    /// <summary>
    /// Le modèle voit BlockSize tokens, dont MaxReplyTokens pour sa réponse : on remonte les tours du plus
    /// récent au plus ancien et on s'arrête au premier qui ne tient plus. Chaque morceau commence et finit
    /// par un token spécial : leurs tokens s'additionnent exactement.
    /// </summary>
    private string BuildPrompt(IReadOnlyList<(string User, string Reply)> turns, string question)
    {
        int budget = Generator.Model.Config.BlockSize - MaxReplyTokens
                     - Generator.CountTokens(ChatFormat.Identity) - Generator.CountTokens(question);

        int first = turns.Count;
        while (first > 0)
        {
            var (user, reply) = turns[first - 1];
            int cost = Generator.CountTokens(ChatFormat.Turn(user, reply));
            if (cost > budget) break;
            budget -= cost;
            first--;
        }

        TurnsInLastPrompt = turns.Count - first;
        return ChatFormat.Identity + string.Concat(turns.Skip(first).Select(t => ChatFormat.Turn(t.User, t.Reply))) + question;
    }
}
