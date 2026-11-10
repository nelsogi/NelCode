using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace NelCode.Engine;

/// <summary>Un bloc : x + Attention(norme(x)), puis x + FeedForward(norme(x)). Mêmes noms de champs que MiniChat.</summary>
public sealed class TransformerBlock : Module<Tensor, Tensor>
{
    private readonly Module<Tensor, Tensor> _norm1;
    private readonly CausalSelfAttention _attention;
    private readonly Module<Tensor, Tensor> _norm2;
    private readonly Module<Tensor, Tensor> _feedForward;

    public TransformerBlock(ModelConfig config, Rope? rope) : base(nameof(TransformerBlock))
    {
        bool modern = config.Architecture == ModelArchitecture.Modern;
        _norm1 = Norm(config);
        _attention = new CausalSelfAttention(config, rope);
        _norm2 = Norm(config);
        _feedForward = modern ? new SwiGlu(config) : new FeedForward(config);

        RegisterComponents();
    }

    /// <summary>RMSNorm (Modern) ou LayerNorm (Classic).</summary>
    public static Module<Tensor, Tensor> Norm(ModelConfig config) =>
        config.Architecture == ModelArchitecture.Modern ? new RmsNorm(config.EmbedDim) : LayerNorm(config.EmbedDim);

    public override Tensor forward(Tensor x) => Forward(x, cache: null, layer: 0);

    public Tensor Forward(Tensor x, KvCache? cache, int layer)
    {
        x = x + _attention.Forward(_norm1.forward(x), cache, layer);
        x = x + _feedForward.forward(_norm2.forward(x));
        return x;
    }
}
