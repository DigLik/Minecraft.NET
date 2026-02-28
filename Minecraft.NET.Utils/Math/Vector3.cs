using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Minecraft.NET.Utils.Math;

[StructLayout(LayoutKind.Sequential)]
[SkipLocalsInit]
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public struct Vector3<T>(T x, T y, T z) :
    IEquatable<Vector3<T>>,
    IAdditionOperators<Vector3<T>, Vector3<T>, Vector3<T>>,
    ISubtractionOperators<Vector3<T>, Vector3<T>, Vector3<T>>,
    IMultiplyOperators<Vector3<T>, Vector3<T>, Vector3<T>>,
    IMultiplyOperators<Vector3<T>, T, Vector3<T>>,
    IDivisionOperators<Vector3<T>, Vector3<T>, Vector3<T>>,
    IDivisionOperators<Vector3<T>, T, Vector3<T>>,
    IUnaryNegationOperators<Vector3<T>, Vector3<T>>,
    IFormattable,
    ISpanFormattable
    where T : unmanaged, INumber<T>
{
    public T X = x, Y = y, Z = z, _padding = default;

    public static readonly Vector3<T> Zero = new(T.Zero, T.Zero, T.Zero);
    public static readonly Vector3<T> One = new(T.One, T.One, T.One);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3(T value) : this(value, value, value) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void operator +=(Vector3<T> right)
    {
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 4)
        {
            (Vector128.LoadUnsafe(ref X) + Vector128.LoadUnsafe(ref right.X)).StoreUnsafe(ref X);
            return;
        }
        if (Vector256.IsHardwareAccelerated && Vector256<T>.Count == 4)
        {
            (Vector256.LoadUnsafe(ref X) + Vector256.LoadUnsafe(ref right.X)).StoreUnsafe(ref X);
            return;
        }
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 2)
        {
            (Vector128.LoadUnsafe(ref X) + Vector128.LoadUnsafe(ref right.X)).StoreUnsafe(ref X);
            Z += right.Z;
            return;
        }
        X += right.X; Y += right.Y; Z += right.Z;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void operator -=(Vector3<T> right)
    {
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 4)
        {
            (Vector128.LoadUnsafe(ref X) - Vector128.LoadUnsafe(ref right.X)).StoreUnsafe(ref X);
            return;
        }
        if (Vector256.IsHardwareAccelerated && Vector256<T>.Count == 4)
        {
            (Vector256.LoadUnsafe(ref X) - Vector256.LoadUnsafe(ref right.X)).StoreUnsafe(ref X);
            return;
        }
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 2)
        {
            (Vector128.LoadUnsafe(ref X) - Vector128.LoadUnsafe(ref right.X)).StoreUnsafe(ref X);
            Z -= right.Z;
            return;
        }
        X -= right.X; Y -= right.Y; Z -= right.Z;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void operator *=(Vector3<T> right)
    {
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 4)
        {
            (Vector128.LoadUnsafe(ref X) * Vector128.LoadUnsafe(ref right.X)).StoreUnsafe(ref X);
            return;
        }
        if (Vector256.IsHardwareAccelerated && Vector256<T>.Count == 4)
        {
            (Vector256.LoadUnsafe(ref X) * Vector256.LoadUnsafe(ref right.X)).StoreUnsafe(ref X);
            return;
        }
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 2)
        {
            (Vector128.LoadUnsafe(ref X) * Vector128.LoadUnsafe(ref right.X)).StoreUnsafe(ref X);
            Z *= right.Z;
            return;
        }
        X *= right.X; Y *= right.Y; Z *= right.Z;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void operator /=(Vector3<T> right)
    {
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 4)
        {
            (Vector128.LoadUnsafe(ref X) / Vector128.LoadUnsafe(ref right.X)).StoreUnsafe(ref X);
            return;
        }
        if (Vector256.IsHardwareAccelerated && Vector256<T>.Count == 4)
        {
            (Vector256.LoadUnsafe(ref X) / Vector256.LoadUnsafe(ref right.X)).StoreUnsafe(ref X);
            return;
        }
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 2)
        {
            (Vector128.LoadUnsafe(ref X) / Vector128.LoadUnsafe(ref right.X)).StoreUnsafe(ref X);
            Z /= right.Z;
            return;
        }
        X /= right.X; Y /= right.Y; Z /= right.Z;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void operator *=(T right)
    {
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 4)
        {
            (Vector128.LoadUnsafe(ref X) * Vector128.Create(right)).StoreUnsafe(ref X);
            return;
        }
        if (Vector256.IsHardwareAccelerated && Vector256<T>.Count == 4)
        {
            (Vector256.LoadUnsafe(ref X) * Vector256.Create(right)).StoreUnsafe(ref X);
            return;
        }
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 2)
        {
            (Vector128.LoadUnsafe(ref X) * Vector128.Create(right)).StoreUnsafe(ref X);
            Z *= right;
            return;
        }
        X *= right; Y *= right; Z *= right;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void operator /=(T right)
    {
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 4)
        {
            (Vector128.LoadUnsafe(ref X) / Vector128.Create(right)).StoreUnsafe(ref X);
            return;
        }
        if (Vector256.IsHardwareAccelerated && Vector256<T>.Count == 4)
        {
            (Vector256.LoadUnsafe(ref X) / Vector256.Create(right)).StoreUnsafe(ref X);
            return;
        }
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 2)
        {
            (Vector128.LoadUnsafe(ref X) / Vector128.Create(right)).StoreUnsafe(ref X);
            Z /= right;
            return;
        }
        X /= right; Y /= right; Z /= right;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3<T> operator +(Vector3<T> left, Vector3<T> right)
    {
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 4)
        {
            (Vector128.LoadUnsafe(ref left.X) + Vector128.LoadUnsafe(ref right.X)).StoreUnsafe(ref left.X);
            return left;
        }

        if (Vector256.IsHardwareAccelerated && Vector256<T>.Count == 4)
        {
            (Vector256.LoadUnsafe(ref left.X) + Vector256.LoadUnsafe(ref right.X)).StoreUnsafe(ref left.X);
            return left;
        }

        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 2)
        {
            (Vector128.LoadUnsafe(ref left.X) + Vector128.LoadUnsafe(ref right.X)).StoreUnsafe(ref left.X);
            left.Z += right.Z;
            return left;
        }

        return new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3<T> operator -(Vector3<T> left, Vector3<T> right)
    {
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 4)
        {
            (Vector128.LoadUnsafe(ref left.X) - Vector128.LoadUnsafe(ref right.X)).StoreUnsafe(ref left.X);
            return left;
        }

        if (Vector256.IsHardwareAccelerated && Vector256<T>.Count == 4)
        {
            (Vector256.LoadUnsafe(ref left.X) - Vector256.LoadUnsafe(ref right.X)).StoreUnsafe(ref left.X);
            return left;
        }

        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 2)
        {
            (Vector128.LoadUnsafe(ref left.X) - Vector128.LoadUnsafe(ref right.X)).StoreUnsafe(ref left.X);
            left.Z -= right.Z;
            return left;
        }

        return new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3<T> operator *(Vector3<T> left, Vector3<T> right)
    {
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 4)
        {
            (Vector128.LoadUnsafe(ref left.X) * Vector128.LoadUnsafe(ref right.X)).StoreUnsafe(ref left.X);
            return left;
        }

        if (Vector256.IsHardwareAccelerated && Vector256<T>.Count == 4)
        {
            (Vector256.LoadUnsafe(ref left.X) * Vector256.LoadUnsafe(ref right.X)).StoreUnsafe(ref left.X);
            return left;
        }

        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 2)
        {
            (Vector128.LoadUnsafe(ref left.X) * Vector128.LoadUnsafe(ref right.X)).StoreUnsafe(ref left.X);
            left.Z *= right.Z;
            return left;
        }

        return new(left.X * right.X, left.Y * right.Y, left.Z * right.Z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3<T> operator /(Vector3<T> left, Vector3<T> right)
    {
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 4)
        {
            (Vector128.LoadUnsafe(ref left.X) / Vector128.LoadUnsafe(ref right.X)).StoreUnsafe(ref left.X);
            return left;
        }

        if (Vector256.IsHardwareAccelerated && Vector256<T>.Count == 4)
        {
            (Vector256.LoadUnsafe(ref left.X) / Vector256.LoadUnsafe(ref right.X)).StoreUnsafe(ref left.X);
            return left;
        }

        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 2)
        {
            (Vector128.LoadUnsafe(ref left.X) / Vector128.LoadUnsafe(ref right.X)).StoreUnsafe(ref left.X);
            left.Z /= right.Z;
            return left;
        }

        return new(left.X / right.X, left.Y / right.Y, left.Z / right.Z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3<T> operator *(Vector3<T> left, T right)
    {
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 4)
        {
            (Vector128.LoadUnsafe(ref left.X) * Vector128.Create(right)).StoreUnsafe(ref left.X);
            return left;
        }

        if (Vector256.IsHardwareAccelerated && Vector256<T>.Count == 4)
        {
            (Vector256.LoadUnsafe(ref left.X) * Vector256.Create(right)).StoreUnsafe(ref left.X);
            return left;
        }

        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 2)
        {
            (Vector128.LoadUnsafe(ref left.X) * Vector128.Create(right)).StoreUnsafe(ref left.X);
            left.Z *= right;
            return left;
        }

        return new(left.X * right, left.Y * right, left.Z * right);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3<T> operator /(Vector3<T> left, T right)
    {
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 4)
        {
            (Vector128.LoadUnsafe(ref left.X) / Vector128.Create(right)).StoreUnsafe(ref left.X);
            return left;
        }

        if (Vector256.IsHardwareAccelerated && Vector256<T>.Count == 4)
        {
            (Vector256.LoadUnsafe(ref left.X) / Vector256.Create(right)).StoreUnsafe(ref left.X);
            return left;
        }

        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 2)
        {
            (Vector128.LoadUnsafe(ref left.X) / Vector128.Create(right)).StoreUnsafe(ref left.X);
            left.Z /= right;
            return left;
        }

        return new(left.X / right, left.Y / right, left.Z / right);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3<T> operator -(Vector3<T> value)
        => new(-value.X, -value.Y, -value.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Dot(Vector3<T> left, Vector3<T> right)
    {
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 4)
        {
            var mul = Vector128.LoadUnsafe(ref left.X) * Vector128.LoadUnsafe(ref right.X);
            return mul.GetElement(0) + mul.GetElement(1) + mul.GetElement(2);
        }

        if (Vector256.IsHardwareAccelerated && Vector256<T>.Count == 4)
        {
            var mul = Vector256.LoadUnsafe(ref left.X) * Vector256.LoadUnsafe(ref right.X);
            return mul.GetElement(0) + mul.GetElement(1) + mul.GetElement(2);
        }

        return (left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z);
    }

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly To Length<To>()
        where To : unmanaged, INumber<To>, IRootFunctions<To>
        => To.Sqrt(To.CreateTruncating(LengthSquared()));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector3<To> Normalize<To>()
        where To : unmanaged, INumber<To>, IRootFunctions<To>
    {
        To length = Length<To>();
        if (length == To.Zero) return new Vector3<To>(To.Zero);

        return new Vector3<To>(
            To.CreateTruncating(X) / length,
            To.CreateTruncating(Y) / length,
            To.CreateTruncating(Z) / length
        );
    }

    public override readonly string ToString() => $"<{X}, {Y}, {Z}>";

    public readonly string ToString(string? format, IFormatProvider? formatProvider)
        => $"<{X.ToString(format, formatProvider)}, {Y.ToString(format, formatProvider)}, {Z.ToString(format, formatProvider)}>";

    public readonly bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        => destination.TryWrite(provider, $"<{X}, {Y}, {Z}>", out charsWritten);
}