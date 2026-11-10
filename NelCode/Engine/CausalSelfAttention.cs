using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace NelCode.Engine;

/// <summary>
/// Attention causale multi-têtes (Q, K, V calculés d'un coup par _qkv). Modern : Q et K tournés par RoPE,
/// projection sans biais. Le calcul passe par scaled_dot_product_attention. Mêmes noms de champs que MiniChat.
/// </summary>
public sealed class CausalSelfAttention : Module<Tensor, Tensor>
{
    private readonly TorchSharp.Modules.Linear _qkv;
    private readonly TorchSharp.Modules.Linear _projection;
    private readonly TorchSharp.Modules.Dropout _outputDropout;
    private readonly int _heads;
    private readonly int _headDim;
    private readonly Rope? _rope;

    public CausalSelfAttention(ModelConfig config, Rope? rope) : base(nameof(CausalSelfAttention))
    {
        if (config.EmbedDim % config.Heads != 0)
            throw new ArgumentException("EmbedDim doit être divisible par Heads.");

        _heads = config.Heads;
        _headDim = config.HeadDim;
        _rope = rope;

        _qkv = Linear(config.EmbedDim, 3 * config.EmbedDim, hasBias: false);
        _projection = Linear(config.EmbedDim, config.EmbedDim, hasBias: rope is null);
        _outputDropout = Dropout(config.Dropout);

        RegisterComponents();
    }

    public override Tensor forward(Tensor x) => Forward(x, cache: null, layer: 0);

    /// <param name="cache">Les Keys/Values des tokens déjà vus ; x ne contient que les nouveaux tokens.</param>
    /// <param name="layer">Numéro de la couche : sa place dans le cache.</param>
    public Tensor Forward(Tensor x, KvCache? cache, int layer)
    {
        long batch = x.shape[0], time = x.shape[1], channels = x.shape[2];
        long start = cache?.Length ?? 0;

        var parts = _qkv.forward(x).chunk(3, 2);
        var q = SplitHeads(parts[0], batch, time);
        var k = SplitHeads(parts[1], batch, time);
        var v = SplitHeads(parts[2], batch, time);

        if (_rope is not null)
        {
            q = _rope.Apply(q, start);
            k = _rope.Apply(k, start);
        }

        // Le masque causal de scaled_dot_product_attention suppose que Q et K commencent au même token :
        // vrai sans cache ou cache vide. Sinon le nouveau token i voit les start + i + 1 premières positions.
        Tensor? mask = null;
        bool causal = true;
        if (cache is not null)
        {
            (k, v) = cache.Append(layer, k, v);
            if (start > 0)
            {
                causal = false;
                if (time > 1)
                    mask = arange(start + time, device: x.device).unsqueeze(0)
                        .le((arange(time, device: x.device) + start).unsqueeze(1));
            }
        }

        var attention = functional.scaled_dot_product_attention(q, k, v, mask, 0.0, causal);

        var output = attention.transpose(1, 2).contiguous().view(batch, time, channels);
        return _outputDropout.forward(_projection.forward(output));
    }

    private Tensor SplitHeads(Tensor t, long batch, long time) =>
        t.view(batch, time, _heads, _headDim).transpose(1, 2);
}
