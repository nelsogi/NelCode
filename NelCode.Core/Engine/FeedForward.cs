using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace NelCode.Engine;

/// <summary>FeedForward Classic : élargir x4, GELU, rétrécir (pour relire les anciens modèles).</summary>
public sealed class FeedForward : Module<Tensor, Tensor>
{
    private readonly TorchSharp.Modules.Linear _expand;
    private readonly TorchSharp.Modules.Linear _shrink;
    private readonly TorchSharp.Modules.Dropout _dropout;

    public FeedForward(ModelConfig config) : base(nameof(FeedForward))
    {
        _expand = Linear(config.EmbedDim, 4 * config.EmbedDim);
        _shrink = Linear(4 * config.EmbedDim, config.EmbedDim);
        _dropout = Dropout(config.Dropout);
        RegisterComponents();
    }

    public override Tensor forward(Tensor x) =>
        _dropout.forward(_shrink.forward(functional.gelu(_expand.forward(x))));
}
