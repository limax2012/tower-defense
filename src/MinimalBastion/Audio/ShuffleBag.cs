namespace MinimalBastion.Audio;

public sealed class ShuffleBag
{
    private readonly int[] _order;
    private int _position;

    public ShuffleBag(int itemCount, Random? random = null)
    {
        if (itemCount <= 0) throw new ArgumentOutOfRangeException(nameof(itemCount));
        _order = new int[itemCount];
        for (var index = 0; index < _order.Length; index++) _order[index] = index;

        var shuffleRandom = random ?? Random.Shared;
        for (var index = _order.Length - 1; index > 0; index--)
        {
            var swapIndex = shuffleRandom.Next(index + 1);
            (_order[index], _order[swapIndex]) = (_order[swapIndex], _order[index]);
        }
    }

    public int Next()
    {
        if (_position >= _order.Length) _position = 0;
        return _order[_position++];
    }
}
