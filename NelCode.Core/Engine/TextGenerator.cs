using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace NelCode.Engine;

/// <summary>Réglages du tirage au sort de chaque token.</summary>
/// <param name="Temperature">&lt; 1 : plus prudent. &gt; 1 : plus créatif.</param>
/// <param name="TopK">Tirage parmi les K tokens les plus probables (0 = tous).</param>
/// <param name="TopP">Tirage parmi les tokens qui font cette part des chances (1 = tous).</param>
public sealed record Sampling(double Temperature, int TopK, double TopP);

/// <summary>Bilan d'une génération : tokens écrits et durée.</summary>
public sealed record GenerationStats(int Tokens, int PromptTokens, TimeSpan Elapsed)
{
    public double TokensPerSecond => Elapsed.TotalSeconds > 0 ? Tokens / Elapsed.TotalSeconds : 0;
}

/// <summary>
/// Fait écrire le modèle token par token, avec le cache KV : le prompt passe une fois, puis chaque
/// nouveau token ne fait passer que lui-même. S'arrête sur un token d'arrêt (&lt;|end|&gt;...).
/// </summary>
public sealed class TextGenerator : IDisposable
{
    // Cache plein : on repart des 3/4 derniers tokens, recalculés d'un coup (comme MiniChat).
    private const double KeptWhenFull = 0.75;

    private readonly MiniGpt _model;
    private readonly BpeTokenizer _tokenizer;
    private readonly Device _device;

    public TextGenerator(MiniGpt model, BpeTokenizer tokenizer, Device device)
    {
        _model = model;
        _tokenizer = tokenizer;
        _device = device;
    }

    public MiniGpt Model => _model;
    public BpeTokenizer Tokenizer => _tokenizer;

    public int CountTokens(string text) => _tokenizer.Encode(text).Length;

    public GenerationStats? LastStats { get; private set; }

    /// <summary>Libère les poids (mémoire GPU) quand on change de modèle.</summary>
    public void Dispose() => _model.Dispose();

    /// <param name="onChar">Appelé à chaque caractère écrit. Renvoie false pour arrêter.</param>
    /// <param name="stopTokens">Tokens qui terminent le texte : la génération s'arrête, sans les écrire.</param>
    public string Generate(string prompt, int maxNewTokens, Sampling sampling, Func<char, bool> onChar, IReadOnlySet<long> stopTokens)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        _model.eval();
        using var noGrad = no_grad();
        using var scope = NewDisposeScope();

        var promptIds = _tokenizer.Encode(prompt);
        if (promptIds.Length == 0)
            return string.Empty;

        int blockSize = _model.Config.BlockSize;
        var ids = new List<long>(promptIds);
        var cache = new KvCache(_model.Config, _device);
        int pending = ids.Count;   // tokens pas encore passés dans le modèle
        var result = new System.Text.StringBuilder();
        int written = 0;

        try
        {
            for (int i = 0; i < maxNewTokens; i++)
            {
                using var tokenScope = NewDisposeScope();

                if (cache.Length + pending > blockSize)
                {
                    cache.Reset();
                    pending = Math.Min(ids.Count, Math.Max(1, (int)(blockSize * KeptWhenFull)));
                }
                var input = tensor(ids.Skip(ids.Count - pending).ToArray(), device: _device).unsqueeze(0);
                var logits = _model.Forward(input, cache).select(1, -1) / sampling.Temperature;
                var probabilities = functional.softmax(logits, -1);

                long next = SampleTopKTopP(probabilities, sampling.TopK, sampling.TopP).item<long>();
                written++;
                if (stopTokens.Contains(next))
                    return result.ToString();
                ids.Add(next);
                pending = 1;

                foreach (char c in _tokenizer.Decode(next))
                {
                    if (!onChar(c))
                        return result.ToString();
                    result.Append(c);
                }
            }

            return result.ToString();
        }
        finally
        {
            LastStats = new GenerationStats(written, promptIds.Length, clock.Elapsed);
        }
    }

    /// <summary>Tire un token parmi les K meilleurs dont les chances cumulées (avant lui) n'atteignent pas topP.</summary>
    /// <param name="probabilities">Forme (1, VocabSize).</param>
    private static Tensor SampleTopKTopP(Tensor probabilities, int topK, double topP)
    {
        var (sorted, indices) = probabilities.sort(dim: -1, descending: true);
        long vocab = sorted.shape[^1];

        var keep = topK > 0
            ? arange(vocab, device: sorted.device).lt(topK).unsqueeze(0)
            : ones_like(sorted, dtype: ScalarType.Bool);

        // Le premier est toujours gardé, même s'il dépasse topP à lui seul.
        var before = sorted.cumsum(-1) - sorted;
        keep = keep.logical_and(before.lt(topP));

        var filtered = sorted.masked_fill(keep.logical_not(), 0);
        return indices.gather(-1, multinomial(filtered, 1));
    }
}
