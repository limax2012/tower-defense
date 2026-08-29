namespace MinimalBastion.Audio;

public sealed class ShuffleBag
{
    private readonly int[] _order;
    private readonly Random _random;
    private int _position;
    private int _lastIndex = -1;

    public ShuffleBag(int itemCount, Random? random = null)
    {
        if (itemCount <= 0) throw new ArgumentOutOfRangeException(nameof(itemCount));
        _order = new int[itemCount];
        _random = random ?? Random.Shared;
        _position = itemCount;
    }

    public int Next()
    {
        if (_position >= _order.Length) Refill();
        var next = _order[_position++];
        _lastIndex = next;
        return next;
    }

    private void Refill()
    {
        for (var index = 0; index < _order.Length; index++) _order[index] = index;
        for (var index = _order.Length - 1; index > 0; index--)
        {
            var swapIndex = _random.Next(index + 1);
            (_order[index], _order[swapIndex]) = (_order[swapIndex], _order[index]);
        }

        if (_order.Length > 1 && _order[0] == _lastIndex)
        {
            var swapIndex = 1 + _random.Next(_order.Length - 1);
            (_order[0], _order[swapIndex]) = (_order[swapIndex], _order[0]);
        }
        _position = 0;
    }
}
