using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Minecraft.NET.Core.Common;

[StructLayout(LayoutKind.Sequential)]
[SkipLocalsInit]
public struct Vector2<T>(T x, T y) :
    IEquatable<Vector2<T>>,
    IAdditionOperators<Vector2<T>, Vector2<T>, Vector2<T>>,
    ISubtractionOperators<Vector2<T>, Vector2<T>, Vector2<T>>,
    IMultiplyOperators<Vector2<T>, Vector2<T>, Vector2<T>>,
    IMultiplyOperators<Vector2<T>, T, Vector2<T>>,
    IDivisionOperators<Vector2<T>, Vector2<T>, Vector2<T>>,
    IDivisionOperators<Vector2<T>, T, Vector2<T>>,
    IUnaryNegationOperators<Vector2<T>, Vector2<T>>
    where T : unmanaged, INumber<T>
{
    public T X = x, Y = y;

    public static readonly Vector2<T> Zero = new(T.Zero, T.Zero);
    public static readonly Vector2<T> One = new(T.One, T.One);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2(T value) : this(value, value) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void operator +=(Vector2<T> other)
    { X += other.X; Y += other.Y; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void operator -=(Vector2<T> other)
    { X -= other.X; Y -= other.Y; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void operator *=(Vector2<T> other)
    { X *= other.X; Y *= other.Y; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void operator /=(Vector2<T> other)
    { X /= other.X; Y /= other.Y; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void operator *=(T value)
    { X *= value; Y *= value; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void operator /=(T value)
    { X /= value; Y /= value; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2<T> operator +(Vector2<T> left, Vector2<T> right)
        => new(left.X + right.X, left.Y + right.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2<T> operator -(Vector2<T> left, Vector2<T> right)
        => new(left.X - right.X, left.Y - right.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2<T> operator *(Vector2<T> left, Vector2<T> right)
        => new(left.X * right.X, left.Y * right.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2<T> operator /(Vector2<T> left, Vector2<T> right)
        => new(left.X / right.X, left.Y / right.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2<T> operator *(Vector2<T> left, T right)
        => new(left.X * right, left.Y * right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2<T> operator /(Vector2<T> left, T right)
        => new(left.X / right, left.Y / right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2<T> operator -(Vector2<T> value)
        => new(-value.X, -value.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Dot(Vector2<T> left, Vector2<T> right)
        => (left.X * right.X) + (left.Y * right.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T LengthSquared() => Dot(this, this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(Vector2<T> other)
        => X == other.X && Y == other.Y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override readonly bool Equals(object? obj) => obj is Vector2<T> other && Equals(other);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override readonly int GetHashCode() => HashCode.Combine(X, Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Vector2<T> left, Vector2<T> right) => left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Vector2<T> left, Vector2<T> right) => !(left == right);

    public override readonly string ToString() => $"<{X}, {Y}>";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly To Length<To>()
        where To : unmanaged, INumber<To>, IRootFunctions<To>
        => To.Sqrt(To.CreateTruncating(LengthSquared()));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector2<To> Normalize<To>()
        where To : unmanaged, INumber<To>, IRootFunctions<To>
    {
        To length = Length<To>();
        return new Vector2<To>(
            To.CreateTruncating(X) / length,
            To.CreateTruncating(Y) / length
        );
    }
}