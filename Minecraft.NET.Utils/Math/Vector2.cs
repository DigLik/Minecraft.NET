using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Minecraft.NET.Utils.Math;

[StructLayout(LayoutKind.Sequential)]
[SkipLocalsInit]
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public struct Vector2<T>(T x, T y) :
    IEquatable<Vector2<T>>,
    IAdditionOperators<Vector2<T>, Vector2<T>, Vector2<T>>,
    ISubtractionOperators<Vector2<T>, Vector2<T>, Vector2<T>>,
    IMultiplyOperators<Vector2<T>, Vector2<T>, Vector2<T>>,
    IMultiplyOperators<Vector2<T>, T, Vector2<T>>,
    IDivisionOperators<Vector2<T>, Vector2<T>, Vector2<T>>,
    IDivisionOperators<Vector2<T>, T, Vector2<T>>,
    IUnaryNegationOperators<Vector2<T>, Vector2<T>>,
    IFormattable,
    ISpanFormattable
    where T : unmanaged, INumber<T>
{
    public T X = x, Y = y;

    public static readonly Vector2<T> Zero = new(T.Zero, T.Zero);
    public static readonly Vector2<T> One = new(T.One, T.One);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2(T value) : this(value, value) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void operator +=(Vector2<T> right)
    {
        if (Vector64.IsHardwareAccelerated && Vector64<T>.Count == 2)
        {
            (Vector64.LoadUnsafe(ref X) + Vector64.LoadUnsafe(ref right.X)).StoreUnsafe(ref X);
            return;
        }
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 2)
        {
            (Vector128.LoadUnsafe(ref X) + Vector128.LoadUnsafe(ref right.X)).StoreUnsafe(ref X);
            return;
        }
        X += right.X; Y += right.Y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void operator -=(Vector2<T> right)
    {
        if (Vector64.IsHardwareAccelerated && Vector64<T>.Count == 2)
        {
            (Vector64.LoadUnsafe(ref X) - Vector64.LoadUnsafe(ref right.X)).StoreUnsafe(ref X);
            return;
        }
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 2)
        {
            (Vector128.LoadUnsafe(ref X) - Vector128.LoadUnsafe(ref right.X)).StoreUnsafe(ref X);
            return;
        }
        X -= right.X; Y -= right.Y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void operator *=(Vector2<T> right)
    {
        if (Vector64.IsHardwareAccelerated && Vector64<T>.Count == 2)
        {
            (Vector64.LoadUnsafe(ref X) * Vector64.LoadUnsafe(ref right.X)).StoreUnsafe(ref X);
            return;
        }
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 2)
        {
            (Vector128.LoadUnsafe(ref X) * Vector128.LoadUnsafe(ref right.X)).StoreUnsafe(ref X);
            return;
        }
        X *= right.X; Y *= right.Y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void operator /=(Vector2<T> right)
    {
        if (Vector64.IsHardwareAccelerated && Vector64<T>.Count == 2)
        {
            (Vector64.LoadUnsafe(ref X) / Vector64.LoadUnsafe(ref right.X)).StoreUnsafe(ref X);
            return;
        }
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 2)
        {
            (Vector128.LoadUnsafe(ref X) / Vector128.LoadUnsafe(ref right.X)).StoreUnsafe(ref X);
            return;
        }
        X /= right.X; Y /= right.Y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void operator *=(T right)
    {
        if (Vector64.IsHardwareAccelerated && Vector64<T>.Count == 2)
        {
            (Vector64.LoadUnsafe(ref X) * Vector64.Create(right)).StoreUnsafe(ref X);
            return;
        }
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 2)
        {
            (Vector128.LoadUnsafe(ref X) * Vector128.Create(right)).StoreUnsafe(ref X);
            return;
        }
        X *= right; Y *= right;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void operator /=(T right)
    {
        if (Vector64.IsHardwareAccelerated && Vector64<T>.Count == 2)
        {
            (Vector64.LoadUnsafe(ref X) / Vector64.Create(right)).StoreUnsafe(ref X);
            return;
        }
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 2)
        {
            (Vector128.LoadUnsafe(ref X) / Vector128.Create(right)).StoreUnsafe(ref X);
            return;
        }
        X /= right; Y /= right;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2<T> operator +(Vector2<T> left, Vector2<T> right)
    {
        if (Vector64.IsHardwareAccelerated && Vector64<T>.Count == 2)
        {
            (Vector64.LoadUnsafe(ref left.X) + Vector64.LoadUnsafe(ref right.X)).StoreUnsafe(ref left.X);
            return left;
        }
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 2)
        {
            (Vector128.LoadUnsafe(ref left.X) + Vector128.LoadUnsafe(ref right.X)).StoreUnsafe(ref left.X);
            return left;
        }
        return new(left.X + right.X, left.Y + right.Y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2<T> operator -(Vector2<T> left, Vector2<T> right)
    {
        if (Vector64.IsHardwareAccelerated && Vector64<T>.Count == 2)
        {
            (Vector64.LoadUnsafe(ref left.X) - Vector64.LoadUnsafe(ref right.X)).StoreUnsafe(ref left.X);
            return left;
        }
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 2)
        {
            (Vector128.LoadUnsafe(ref left.X) - Vector128.LoadUnsafe(ref right.X)).StoreUnsafe(ref left.X);
            return left;
        }
        return new(left.X - right.X, left.Y - right.Y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2<T> operator *(Vector2<T> left, Vector2<T> right)
    {
        if (Vector64.IsHardwareAccelerated && Vector64<T>.Count == 2)
        {
            (Vector64.LoadUnsafe(ref left.X) * Vector64.LoadUnsafe(ref right.X)).StoreUnsafe(ref left.X);
            return left;
        }
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 2)
        {
            (Vector128.LoadUnsafe(ref left.X) * Vector128.LoadUnsafe(ref right.X)).StoreUnsafe(ref left.X);
            return left;
        }
        return new(left.X * right.X, left.Y * right.Y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2<T> operator /(Vector2<T> left, Vector2<T> right)
    {
        if (Vector64.IsHardwareAccelerated && Vector64<T>.Count == 2)
        {
            (Vector64.LoadUnsafe(ref left.X) / Vector64.LoadUnsafe(ref right.X)).StoreUnsafe(ref left.X);
            return left;
        }
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 2)
        {
            (Vector128.LoadUnsafe(ref left.X) / Vector128.LoadUnsafe(ref right.X)).StoreUnsafe(ref left.X);
            return left;
        }
        return new(left.X / right.X, left.Y / right.Y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2<T> operator *(Vector2<T> left, T right)
    {
        if (Vector64.IsHardwareAccelerated && Vector64<T>.Count == 2)
        {
            (Vector64.LoadUnsafe(ref left.X) * Vector64.Create(right)).StoreUnsafe(ref left.X);
            return left;
        }
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 2)
        {
            (Vector128.LoadUnsafe(ref left.X) * Vector128.Create(right)).StoreUnsafe(ref left.X);
            return left;
        }
        return new(left.X * right, left.Y * right);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2<T> operator /(Vector2<T> left, T right)
    {
        if (Vector64.IsHardwareAccelerated && Vector64<T>.Count == 2)
        {
            (Vector64.LoadUnsafe(ref left.X) / Vector64.Create(right)).StoreUnsafe(ref left.X);
            return left;
        }
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 2)
        {
            (Vector128.LoadUnsafe(ref left.X) / Vector128.Create(right)).StoreUnsafe(ref left.X);
            return left;
        }
        return new(left.X / right, left.Y / right);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2<T> operator -(Vector2<T> value) => new(-value.X, -value.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T LengthSquared() => (X * X) + (Y * Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly To Length<To>() where To : unmanaged, INumber<To>, IRootFunctions<To>
        => To.Sqrt(To.CreateTruncating(LengthSquared()));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector2<To> Normalize<To>() where To : unmanaged, INumber<To>, IRootFunctions<To>
    {
        To length = Length<To>();
        if (length == To.Zero) return new Vector2<To>(To.Zero);
        return new Vector2<To>(To.CreateTruncating(X) / length, To.CreateTruncating(Y) / length);
    }

    public readonly bool Equals(Vector2<T> other) => X == other.X && Y == other.Y;
    public override readonly bool Equals(object? obj) => obj is Vector2<T> other && Equals(other);
    public override readonly int GetHashCode() => HashCode.Combine(X, Y);
    public static bool operator ==(Vector2<T> left, Vector2<T> right) => left.Equals(right);
    public static bool operator !=(Vector2<T> left, Vector2<T> right) => !(left == right);

    public override readonly string ToString() => $"<{X}, {Y}>";

    public readonly string ToString(string? format, IFormatProvider? formatProvider)
        => $"<{X.ToString(format, formatProvider)}, {Y.ToString(format, formatProvider)}>";

    public readonly bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        => destination.TryWrite(provider, $"<{X}, {Y}>", out charsWritten);
}