global using Enum = MaximalBastion.Gameplay.PortableEnum;

namespace MaximalBastion.Gameplay
{
    internal static class PortableEnum
    {
        public static T[] GetValues<T>() where T : struct, System.Enum => (T[])System.Enum.GetValues(typeof(T));
        public static bool IsDefined<T>(T value) where T : struct, System.Enum => System.Enum.IsDefined(typeof(T), value);
        public static bool IsDefined(Type type, object value) => System.Enum.IsDefined(type, value);
        public static T Parse<T>(string value, bool ignoreCase) where T : struct => System.Enum.Parse<T>(value, ignoreCase);
        public static bool TryParse<T>(string? value, bool ignoreCase, out T result) where T : struct => System.Enum.TryParse(value, ignoreCase, out result);
    }
}

namespace System.Linq
{
    internal static class PortableEnumerable
    {
        public static TSource MinBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> selector)
        {
            using var iterator = source.GetEnumerator();
            if (!iterator.MoveNext()) throw new InvalidOperationException("Sequence contains no elements");
            var best = iterator.Current;
            var key = selector(best);
            while (iterator.MoveNext())
            {
                var candidateKey = selector(iterator.Current);
                if (Comparer<TKey>.Default.Compare(candidateKey, key) >= 0) continue;
                best = iterator.Current;
                key = candidateKey;
            }
            return best;
        }
    }
}
