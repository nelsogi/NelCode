using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace NelCode.Engine;

/// <summary>
/// Le modèle de MiniChat : Embedding [+ position (Classic)] → N x TransformerBlock → norme finale → x · Embeddingᵀ.
/// Les noms des champs sont ceux de MiniChat : ce sont les clés des poids dans weights.dat.
/// </summary>
public sealed class MiniGpt : Module<Tensor, Tensor>
{
    private readonly TorchSharp.Modules.Embedding _tokenEmbedding;
    private readonly TorchSharp.Modules.Embedding? _positionEmbedding;   // Classic seulement
    private readonly TorchSharp.Modules.Dropout _dropout;
    private readonly TorchSharp.Modules.ModuleList<TransformerBlock> _blocks;
    private readonly Module<Tensor, Tensor> _finalNorm;

    public ModelConfig Config { get; }

    public MiniGpt(ModelConfig config) : base(nameof(MiniGpt))
    {
        Config = config;

        _tokenEmbedding = Embedding(config.VocabSize, config.EmbedDim);
        var rope = config.Architecture == ModelArchitecture.Modern ? new Rope(config.BlockSize, config.HeadDim) : null;
        if (rope is null)
            _positionEmbedding = Embedding(config.BlockSize, config.EmbedDim);
        _dropout = Dropout(config.Dropout);
        _blocks = ModuleList(Enumerable.Range(0, config.Layers)
            .Select(_ => new TransformerBlock(config, rope))
            .ToArray());
        _finalNorm = TransformerBlock.Norm(config);

        RegisterComponents();
    }

    public override Tensor forward(Tensor ids) => Forward(ids, cache: null);

    /// <param name="ids">Avec cache : seulement les nouveaux tokens, qui suivent les cache.Length déjà vus.</param>
    /// <returns>Logits de forme (batch, temps, VocabSize).</returns>
    public Tensor Forward(Tensor ids, KvCache? cache)
    {
        long time = ids.shape[1];
        long start = cache?.Length ?? 0;
        if (start + time > Config.BlockSize)
            throw new ArgumentException($"Séquence trop longue ({start + time} > {Config.BlockSize}).");

        var x = _tokenEmbedding.forward(ids);
        if (_positionEmbedding is not null)
            x = x + _positionEmbedding.forward(arange(start, start + time, 1, ScalarType.Int64, ids.device));
        x = _dropout.forward(x);

        for (int layer = 0; layer < _blocks.Count; layer++)
            x = _blocks[layer].Forward(x, cache, layer);
        cache?.Advance(time);

        // Weight tying : la sortie réutilise la matrice de l'embedding.
        return functional.linear(_finalNorm.forward(x), _tokenEmbedding.weight!);
    }

    public long ParameterCount => parameters().Sum(p => p.numel());
}
