namespace MinimalBastion.Core;

internal static class MetricMath
{
    public static int Add(int current, int amount = 1) =>
        (int)Math.Min(int.MaxValue, (long)Math.Max(0, current) + Math.Max(0, amount));

    public static float Add(float current, float amount)
    {
        current = Normalize(current);
        amount = Normalize(amount);
        return (double)current + amount >= float.MaxValue ? float.MaxValue : current + amount;
    }

    public static float Normalize(float value)
    {
        if (float.IsNaN(value) || value <= 0) return 0;
        return float.IsPositiveInfinity(value) ? float.MaxValue : value;
    }
}
