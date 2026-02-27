using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Minecraft.NET.Core.Common;

[StructLayout(LayoutKind.Sequential)]
[SkipLocalsInit]
public struct Vector3<T>(T x, T y, T z) :
    IEquatable<Vector3<T>>,
    IAdditionOperators<Vector3<T>, Vector3<T>, Vector3<T>>,
    ISubtractionOperators<Vector3<T>, Vector3<T>, Vector3<T>>,
    IMultiplyOperators<Vector3<T>, Vector3<T>, Vector3<T>>,
    IMultiplyOperators<Vector3<T>, T, Vector3<T>>,
    IDivisionOperators<Vector3<T>, Vector3<T>, Vector3<T>>,
    IDivisionOperators<Vector3<T>, T, Vector3<T>>,
    IUnaryNegationOperators<Vector3<T>, Vector3<T>>
    where T : unmanaged, INumber<T>
{
    public T X = x, Y = y, Z = z;

    public static readonly Vector3<T> Zero = new(T.Zero, T.Zero, T.Zero);
    public static readonly Vector3<T> One = new(T.One, T.One, T.One);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3(T value) : this(value, value, value) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void operator +=(Vector3<T> other)
    { X += other.X; Y += other.Y; Z += other.Z; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void operator -=(Vector3<T> other)
    { X -= other.X; Y -= other.Y; Z -= other.Z; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void operator *=(Vector3<T> other)
    { X *= other.X; Y *= other.Y; Z *= other.Z; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void operator /=(Vector3<T> other)
    { X /= other.X; Y /= other.Y; Z /= other.Z; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void operator *=(T value)
    { X *= value; Y *= value; Z *= value; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void operator /=(T value)
    { X /= value; Y /= value; Z /= value; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3<T> operator +(Vector3<T> left, Vector3<T> right)
        => new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3<T> operator -(Vector3<T> left, Vector3<T> right)
        => new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3<T> operator *(Vector3<T> left, Vector3<T> right)
        => new(left.X * right.X, left.Y * right.Y, left.Z * right.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3<T> operator /(Vector3<T> left, Vector3<T> right)
        => new(left.X / right.X, left.Y / right.Y, left.Z / right.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3<T> operator *(Vector3<T> left, T right)
        => new(left.X * right, left.Y * right, left.Z * right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3<T> operator /(Vector3<T> left, T right)
        => new(left.X / right, left.Y / right, left.Z / right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3<T> operator -(Vector3<T> value)
        => new(-value.X, -value.Y, -value.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Dot(Vector3<T> left, Vector3<T> right)
        => (left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3<T> Cross(Vector3<T> left, Vector3<T> right)
        => new(
            (left.Y * right.Z) - (left.Z * right.Y),
            (left.Z * right.X) - (left.X * right.Z),
            (left.X * right.Y) - (left.Y * right.X)
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T LengthSquared() => Dot(this, this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(Vector3<T> other)
        => X == other.X && Y == other.Y && Z == other.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override readonly bool Equals(object? obj) => obj is Vector3<T> other && Equals(other);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override readonly int GetHashCode() => HashCode.Combine(X, Y, Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Vector3<T> left, Vector3<T> right) => left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Vector3<T> left, Vector3<T> right) => !(left == right);

    public override readonly string ToString() => $"<{X}, {Y}, {Z}>";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly To Length<To>()
        where To : unmanaged, INumber<To>, IRootFunctions<To>
        => To.Sqrt(To.CreateTruncating(LengthSquared()));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector3<To> Normalize<To>()
        where To : unmanaged, INumber<To>, IRootFunctions<To>
    {
        To length = Length<To>();
        return new Vector3<To>(
            To.CreateTruncating(X) / length,
            To.CreateTruncating(Y) / length,
            To.CreateTruncating(Z) / length
        );
    }
}