using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace NelCode.Engine;

/// <summary>FeedForward Modern : down( silu(gate · x) * (up · x) ), sans biais.</summary>
public sealed class SwiGlu : Module<Tensor, Tensor>
{
    private readonly TorchSharp.Modules.Linear _gate;
    private readonly TorchSharp.Modules.Linear _up;
    private readonly TorchSharp.Modules.Linear _down;
    private readonly TorchSharp.Modules.Dropout _dropout;

    public SwiGlu(ModelConfig config) : base(nameof(SwiGlu))
    {
        _gate = Linear(config.EmbedDim, config.SwiGluHidden, hasBias: false);
        _up = Linear(config.EmbedDim, config.SwiGluHidden, hasBias: false);
        _down = Linear(config.SwiGluHidden, config.EmbedDim, hasBias: false);
        _dropout = Dropout(config.Dropout);
        RegisterComponents();
    }

    public override Tensor forward(Tensor x) =>
        _dropout.forward(_down.forward(functional.silu(_gate.forward(x)) * _up.forward(x)));
}
