using System.Text.RegularExpressions;

namespace NelCode.Engine;

/// <summary>
/// Le tokenizer BPE de MiniChat (sans l'apprentissage des fusions) : le texte est coupé en mots,
/// puis chaque mot rejoue les fusions apprises, la plus ancienne d'abord.
/// Tokens spéciaux (&lt;|doc|&gt;, &lt;|user|&gt;, &lt;|assistant|&gt;, &lt;|end|&gt;) : un token chacun, ids après les fusions.
/// </summary>
public sealed partial class BpeTokenizer
{
    public const string DocToken = "<|doc|>";
    public const string UserToken = "<|user|>";
    public const string AssistantToken = "<|assistant|>";
    public const string EndToken = "<|end|>";

    private readonly string[] _tokens;                          // id -> texte du token
    private readonly Dictionary<char, int> _charToId;
    private readonly (int Left, int Right)[] _merges;           // fusion n° i -> crée le token Alphabet.Length + i
    private readonly Dictionary<(int, int), int> _mergeRank;
    private readonly Dictionary<string, int[]> _cache = new();
    private readonly Dictionary<string, int> _specialIds;
    private readonly Regex _specialPattern;
    private readonly string _alphabet;

    public BpeTokenizer(string alphabet, IEnumerable<(int Left, int Right)> merges, IEnumerable<string> specialTokens)
    {
        _alphabet = alphabet;
        _merges = merges.ToArray();
        var special = specialTokens.ToArray();

        var tokens = alphabet.Select(c => c.ToString()).ToList();
        foreach (var (left, right) in _merges)
            tokens.Add(tokens[left] + tokens[right]);
        _specialIds = special.Select((s, i) => (s, tokens.Count + i)).ToDictionary(p => p.s, p => p.Item2);
        tokens.AddRange(special);
        _tokens = tokens.ToArray();

        _charToId = alphabet.Select((c, i) => (c, i)).ToDictionary(p => p.c, p => p.i);
        _mergeRank = _merges.Select((m, i) => (m, i)).ToDictionary(p => p.m, p => p.i);
        // Les plus longs d'abord, au cas où l'un commencerait comme un autre.
        _specialPattern = new Regex(string.Join('|', special.OrderByDescending(s => s.Length).Select(Regex.Escape)));
    }

    public int VocabSize => _tokens.Length;

    public IReadOnlyCollection<int> SpecialIds => _specialIds.Values;

    /// <summary>Texte -> ids. Les tokens spéciaux écrits dans le texte donnent leur id ; les caractères inconnus sont ignorés.</summary>
    public long[] Encode(string text)
    {
        var ids = new List<long>();
        int start = 0;
        foreach (Match special in _specialPattern.Matches(text))
        {
            EncodePlain(text[start..special.Index], ids);
            ids.Add(_specialIds[special.Value]);
            start = special.Index + special.Length;
        }
        EncodePlain(text[start..], ids);
        return ids.ToArray();
    }

    public string Decode(long id) => _tokens[id];

    private void EncodePlain(string text, List<long> ids)
    {
        foreach (Match m in PreTokenizer().Matches(text))
            foreach (int id in EncodeWord(m.Value))
                ids.Add(id);
    }

    private int[] EncodeWord(string word)
    {
        if (_cache.TryGetValue(word, out var cached))
            return cached;

        var symbols = new List<int>();
        foreach (char c in word)
            if (_charToId.TryGetValue(c, out int id))
                symbols.Add(id);

        while (symbols.Count > 1)
        {
            int bestRank = int.MaxValue;
            for (int i = 0; i + 1 < symbols.Count; i++)
                if (_mergeRank.TryGetValue((symbols[i], symbols[i + 1]), out int rank) && rank < bestRank)
                    bestRank = rank;

            if (bestRank == int.MaxValue) break;
            MergePair(symbols, _merges[bestRank], _alphabet.Length + bestRank);
        }

        return _cache[word] = symbols.ToArray();
    }

    /// <summary>Remplace chaque paire (gauche, droite) par newId, de gauche à droite.</summary>
    private static void MergePair(List<int> symbols, (int Left, int Right) pair, int newId)
    {
        for (int i = 0; i + 1 < symbols.Count; i++)
        {
            if (symbols[i] == pair.Left && symbols[i + 1] == pair.Right)
            {
                symbols[i] = newId;
                symbols.RemoveAt(i + 1);
            }
        }
    }

    // Même découpage que MiniChat : \n seul, " ?lettres", " ?chiffres", " ?ponctuation", autres espaces.
    [GeneratedRegex(@"\n| ?[\p{L}\p{M}]+| ?\p{N}+| ?[^\s\p{L}\p{M}\p{N}]+|[^\S\n]+")]
    private static partial Regex PreTokenizer();
}
