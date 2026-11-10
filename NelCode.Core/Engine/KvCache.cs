using static TorchSharp.torch;

namespace NelCode.Engine;

/// <summary>
/// Cache KV : chaque couche garde les Keys et Values déjà calculées, un nouveau token ne calcule que les siennes.
/// Place réservée d'un coup pour BlockSize tokens (batch de 1). À créer dans un DisposeScope.
/// </summary>
public sealed class KvCache
{
    private readonly Tensor[] _keys;     // un par couche, forme (1, têtes, BlockSize, headDim)
    private readonly Tensor[] _values;

    public KvCache(ModelConfig config, Device device)
    {
        long[] shape = { 1, config.Heads, config.BlockSize, config.HeadDim };
        _keys = Enumerable.Range(0, config.Layers).Select(_ => zeros(shape, device: device)).ToArray();
        _values = Enumerable.Range(0, config.Layers).Select(_ => zeros(shape, device: device)).ToArray();
        Capacity = config.BlockSize;
    }

    /// <summary>Nombre de tokens déjà dans le cache : la position du prochain token.</summary>
    public long Length { get; private set; }

    public long Capacity { get; }

    /// <summary>Range les Keys/Values des nouveaux tokens d'une couche, renvoie celles de tous les tokens jusqu'ici.</summary>
    public (Tensor Keys, Tensor Values) Append(int layer, Tensor keys, Tensor values)
    {
        long time = keys.shape[2];
        if (Length + time > Capacity)
            throw new InvalidOperationException($"Cache KV plein ({Length} + {time} > {Capacity}).");

        _keys[layer].narrow(2, Length, time).copy_(keys);
        _values[layer].narrow(2, Length, time).copy_(values);
        return (_keys[layer].narrow(2, 0, Length + time), _values[layer].narrow(2, 0, Length + time));
    }

    public void Advance(long time) => Length += time;

    public void Reset() => Length = 0;
}
