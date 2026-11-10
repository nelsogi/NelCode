using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace NelCode.Engine;

/// <summary>RMSNorm (Modern) : y = x / sqrt(moyenne(x²) + eps) * weight.</summary>
public sealed class RmsNorm : Module<Tensor, Tensor>
{
    private readonly TorchSharp.Modules.Parameter weight;   // nom du poids dans weights.dat
    private readonly double _eps;

    public RmsNorm(int dim, double eps = 1e-5) : base(nameof(RmsNorm))
    {
        weight = Parameter(ones(dim));
        _eps = eps;
        RegisterComponents();
    }

    public override Tensor forward(Tensor x) =>
        x * rsqrt(x.pow(2).mean(new long[] { -1 }, keepdim: true) + _eps) * weight;
}
