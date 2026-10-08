namespace MaximalBastion.Gameplay;

/// <summary>Planar simulation coordinates. Arithmetic order matches the desktop simulation.</summary>
public struct Vector2 : IEquatable<Vector2>
{
    public float X;
    public float Y;
    public Vector2(float x, float y) { X = x; Y = y; }
    public Vector2(float value) : this(value, value) { }
    public static Vector2 Zero => default;
    public static Vector2 One => new(1f, 1f);
    public static Vector2 UnitX => new(1f, 0f);
    public static Vector2 UnitY => new(0f, 1f);
    public float LengthSquared() => X * X + Y * Y;
    public float Length() => (float)Math.Sqrt(X * X + Y * Y);
    public Point ToPoint() => new((int)X, (int)Y);
    public static float DistanceSquared(Vector2 a, Vector2 b)
    {
        float x = a.X - b.X, y = a.Y - b.Y;
        return x * x + y * y;
    }
    public static float Distance(Vector2 a, Vector2 b) => (float)Math.Sqrt(DistanceSquared(a, b));
    public static float Dot(Vector2 a, Vector2 b) => a.X * b.X + a.Y * b.Y;
    public static Vector2 Lerp(Vector2 a, Vector2 b, float amount) => new(
        a.X + (b.X - a.X) * amount, a.Y + (b.Y - a.Y) * amount);
    public static Vector2 Normalize(Vector2 value)
    {
        float inverse = 1f / (float)Math.Sqrt(value.X * value.X + value.Y * value.Y);
        return new(value.X * inverse, value.Y * inverse);
    }
    public void Normalize() => this = Normalize(this);
    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2 operator -(Vector2 a) => new(-a.X, -a.Y);
    public static Vector2 operator *(Vector2 a, float b) => new(a.X * b, a.Y * b);
    public static Vector2 operator *(float a, Vector2 b) => b * a;
    public static Vector2 operator /(Vector2 a, float b) { float inverse = 1f / b; return a * inverse; }
    public static bool operator ==(Vector2 a, Vector2 b) => a.X == b.X && a.Y == b.Y;
    public static bool operator !=(Vector2 a, Vector2 b) => !(a == b);
    public bool Equals(Vector2 other) => this == other;
    public override bool Equals(object? obj) => obj is Vector2 other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public override string ToString() => $"{{X:{X} Y:{Y}}}";
}

public readonly struct Point : IEquatable<Point>
{
    public int X { get; }
    public int Y { get; }
    public Point(int x, int y) { X = x; Y = y; }
    public bool Equals(Point other) => X == other.X && Y == other.Y;
    public override bool Equals(object? obj) => obj is Point other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y);
}

public readonly struct Rectangle
{
    public int X { get; }
    public int Y { get; }
    public int Width { get; }
    public int Height { get; }
    public int Left => X;
    public int Right => X + Width;
    public int Top => Y;
    public int Bottom => Y + Height;
    public Point Center => new(X + Width / 2, Y + Height / 2);
    public Rectangle(int x, int y, int width, int height) { X = x; Y = y; Width = width; Height = height; }
    public bool Contains(Point p) => Contains(p.X, p.Y);
    public bool Contains(int x, int y) => X <= x && x < X + Width && Y <= y && y < Y + Height;
}

public static class MathHelper
{
    public const float TwoPi = 6.28318548f;
    public static float Clamp(float value, float min, float max)
    {
        value = value > max ? max : value;
        return value < min ? min : value;
    }
}

/// <summary>Authored sRGB channels and the stable RGBA packed value used by snapshots.</summary>
public struct Color : IEquatable<Color>
{
    public uint PackedValue { get; set; }
    public byte R => (byte)PackedValue;
    public byte G => (byte)(PackedValue >> 8);
    public byte B => (byte)(PackedValue >> 16);
    public byte A => (byte)(PackedValue >> 24);
    public static Color White => new(255, 255, 255);
    public Color(int r, int g, int b, int a = 255) => PackedValue =
        (uint)(Math.Clamp(r, 0, 255) | Math.Clamp(g, 0, 255) << 8 | Math.Clamp(b, 0, 255) << 16 | Math.Clamp(a, 0, 255) << 24);
    public Color(uint packedValue) => PackedValue = packedValue;
    public static Color Lerp(Color a, Color b, float amount)
    {
        amount = MathHelper.Clamp(amount, 0, 1);
        return new((int)(a.R + (b.R - a.R) * amount), (int)(a.G + (b.G - a.G) * amount),
            (int)(a.B + (b.B - a.B) * amount), (int)(a.A + (b.A - a.A) * amount));
    }
    public static Color FromNonPremultiplied(int r, int g, int b, int a) => new(r * a / 255, g * a / 255, b * a / 255, a);
    public static Color operator *(Color c, float amount) => new((int)(c.R * amount), (int)(c.G * amount), (int)(c.B * amount), (int)(c.A * amount));
    public static bool operator ==(Color a, Color b) => a.PackedValue == b.PackedValue;
    public static bool operator !=(Color a, Color b) => !(a == b);
    public bool Equals(Color other) => this == other;
    public override bool Equals(object? obj) => obj is Color other && Equals(other);
    public override int GetHashCode() => PackedValue.GetHashCode();
}
