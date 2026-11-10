using TorchSharp;
using static TorchSharp.torch;

namespace NelCode.Engine;

/// <summary>
/// RoPE (Modern) : Q et K tournent d'un angle qui dépend de la position ; la paire (i, i + headDim/2)
/// tourne de Theta^(-2i/headDim) radian par position. Pas de paramètres. La table des cosinus et sinus
/// est calculée une fois par device.
/// </summary>
public sealed class Rope
{
    public const double Theta = 10_000;

    private readonly int _positions;
    private readonly int _headDim;
    private (Device Device, Tensor Cos, Tensor Sin)? _table;

    public Rope(int positions, int headDim)
    {
        _positions = positions;
        _headDim = headDim;
    }

    /// <param name="x">Q ou K, forme (batch, têtes, temps, headDim).</param>
    /// <param name="start">Position du premier token de x.</param>
    public Tensor Apply(Tensor x, long start)
    {
        var (_, cosTable, sinTable) = Table(x.device);
        long time = x.shape[2];
        var cos = cosTable.narrow(0, start, time).to(x.dtype);
        var sin = sinTable.narrow(0, start, time).to(x.dtype);

        var halves = x.chunk(2, -1);
        var rotated = cat(new[] { -halves[1], halves[0] }, -1);
        return x * cos + rotated * sin;
    }

    private (Device, Tensor, Tensor) Table(Device device)
    {
        if (_table is { } table && table.Device.type == device.type && table.Device.index == device.index)
            return table;

        using var scope = NewDisposeScope();
        var speeds = exp(arange(0, _headDim, 2, ScalarType.Float32, device) * (-Math.Log(Theta) / _headDim));
        var angles = arange(0, _positions, 1, ScalarType.Float32, device).unsqueeze(1) * speeds.unsqueeze(0);
        var cos = cat(new[] { angles.cos(), angles.cos() }, -1);
        var sin = cat(new[] { angles.sin(), angles.sin() }, -1);

        // Gardée hors des DisposeScope : elle sert à toutes les générations suivantes.
        _table = (device, cos.DetachFromDisposeScope(), sin.DetachFromDisposeScope());
        return _table.Value;
    }
}
