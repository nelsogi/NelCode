namespace NelCode.Engine;

/// <summary>
/// Transforme le générateur en chat. Le prompt (comme ChatSession dans MiniChat) :
///   identité + les derniers tours COMPLETS qui tiennent + &lt;|user|&gt;message&lt;|end|&gt;&lt;|assistant|&gt;
/// et le modèle continue jusqu'à un token spécial (&lt;|end|&gt; en principe).
/// Ne garde pas la conversation : elle est passée à chaque réponse (elle vit dans la session sauvegardée).
/// </summary>
public sealed class ChatEngine
{
    private readonly HashSet<long> _stopTokens;

    public ChatEngine(TextGenerator generator)
    {
        Generator = generator;
        _stopTokens = generator.Tokenizer.SpecialIds.Select(id => (long)id).ToHashSet();
    }

    public TextGenerator Generator { get; }

    /// <summary>Ce que le modèle voit en tout, en tokens : prompt et réponse.</summary>
    public int BlockSize => Generator.Model.Config.BlockSize;

    /// <summary>Combien de tours de la conversation tenaient dans le dernier prompt (commande /info).</summary>
    public int TurnsInLastPrompt { get; private set; }

    /// <summary>
    /// La réponse au message, caractère par caractère via onChar, en maxReplyTokens tokens au plus (pas plus
    /// de la moitié du contexte). Annulée : la réponse s'arrête là.
    /// </summary>
    public string Reply(IReadOnlyList<(string User, string Reply)> turns, string message, Sampling sampling,
        int maxReplyTokens, Action<char> onChar, CancellationToken cancellation = default)
    {
        maxReplyTokens = Math.Min(maxReplyTokens, BlockSize / 2);
        string question = ChatFormat.User(message) + BpeTokenizer.AssistantToken;
        var (first, _) = Fit(turns, maxReplyTokens + Generator.CountTokens(question));
        TurnsInLastPrompt = turns.Count - first;
        string prompt = ChatFormat.Identity + string.Concat(turns.Skip(first).Select(t => ChatFormat.Turn(t.User, t.Reply))) + question;

        return Generator.Generate(prompt, maxReplyTokens, sampling, c =>
        {
            if (cancellation.IsCancellationRequested) return false;
            onChar(c);
            return true;
        }, _stopTokens);
    }

    /// <summary>
    /// Ce que le modèle verra de la conversation au prochain message (question non comptée) : le premier tour
    /// qu'il voit encore, et les tokens de l'identité et des tours vus.
    /// </summary>
    public (int FirstTurn, int Tokens) Context(IReadOnlyList<(string User, string Reply)> turns, int maxReplyTokens) =>
        Fit(turns, Math.Min(maxReplyTokens, BlockSize / 2));

    /// <summary>
    /// Le modèle voit BlockSize tokens, dont reserved pour la question et la réponse : on remonte les tours du
    /// plus récent au plus ancien et on s'arrête au premier qui ne tient plus. Chaque morceau commence et finit
    /// par un token spécial : leurs tokens s'additionnent exactement.
    /// </summary>
    private (int First, int Tokens) Fit(IReadOnlyList<(string User, string Reply)> turns, int reserved)
    {
        int used = Generator.CountTokens(ChatFormat.Identity);
        int budget = BlockSize - reserved - used;
        int first = turns.Count;
        while (first > 0)
        {
            var (user, reply) = turns[first - 1];
            int cost = Generator.CountTokens(ChatFormat.Turn(user, reply));
            if (cost > budget) break;
            budget -= cost;
            used += cost;
            first--;
        }
        return (first, used);
    }
}
