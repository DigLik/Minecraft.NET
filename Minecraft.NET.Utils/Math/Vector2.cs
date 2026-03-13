using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Minecraft.NET.Utils.Math;

/// <summary>
/// Представляет двумерный вектор. Поддерживает аппаратное ускорение SIMD.
/// </summary>
/// <typeparam name="T">Неуправляемый числовой тип.</typeparam>
[StructLayout(LayoutKind.Sequential)]
[SkipLocalsInit]
public struct Vector2<T>(T x, T y) : IEquatable<Vector2<T>>
    where T : unmanaged, INumber<T>
{
    /// <summary>X-компонента вектора.</summary>
    public T X = x;

    /// <summary>Y-компонента вектора.</summary>
    public T Y = y;

    /// <summary>Возвращает вектор, обе компоненты которого равны нулю.</summary>
    public static readonly Vector2<T> Zero = new(T.Zero, T.Zero);

    /// <summary>Возвращает вектор, обе компоненты которого равны единице.</summary>
    public static readonly Vector2<T> One = new(T.One, T.One);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void operator +=(in Vector2<T> right)
    {
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 2)
        {
            (Vector128.LoadUnsafe(ref X) + Vector128.LoadUnsafe(ref Unsafe.AsRef(in right.X))).StoreUnsafe(ref X);
            return;
        }
        if (Vector64.IsHardwareAccelerated && Vector64<T>.Count == 2)
        {
            (Vector64.LoadUnsafe(ref X) + Vector64.LoadUnsafe(ref Unsafe.AsRef(in right.X))).StoreUnsafe(ref X);
            return;
        }
        
        X += right.X;
        Y += right.Y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void operator -=(in Vector2<T> right)
    {
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 2)
        {
            (Vector128.LoadUnsafe(ref X) - Vector128.LoadUnsafe(ref Unsafe.AsRef(in right.X))).StoreUnsafe(ref X);
            return;
        }
        if (Vector64.IsHardwareAccelerated && Vector64<T>.Count == 2)
        {
            (Vector64.LoadUnsafe(ref X) - Vector64.LoadUnsafe(ref Unsafe.AsRef(in right.X))).StoreUnsafe(ref X);
            return;
        }

        X -= right.X;
        Y -= right.Y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void operator *=(T right)
    {
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 2)
        {
            (Vector128.LoadUnsafe(ref X) * Vector128.Create(right)).StoreUnsafe(ref X);
            return;
        }
        if (Vector64.IsHardwareAccelerated && Vector64<T>.Count == 2)
        {
            (Vector64.LoadUnsafe(ref X) * Vector64.Create(right)).StoreUnsafe(ref X);
            return;
        }

        X *= right;
        Y *= right;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2<T> operator +(in Vector2<T> left, in Vector2<T> right)
    {
        Vector2<T> result = left;
        result += right;
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2<T> operator -(in Vector2<T> left, in Vector2<T> right)
    {
        Vector2<T> result = left;
        result -= right;
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2<T> operator *(in Vector2<T> left, T right)
    {
        Vector2<T> result = left;
        result *= right;
        return result;
    }

    /// <summary>Возвращает квадрат длины вектора.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T LengthSquared() => (X * X) + (Y * Y);

    /// <summary>Возвращает длину вектора.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly To Length<To>() where To : unmanaged, INumber<To>, IRootFunctions<To>
        => To.Sqrt(To.CreateTruncating(LengthSquared()));

    /// <summary>Возвращает нормализованную копию вектора.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector2<To> Normalize<To>() where To : unmanaged, INumber<To>, IRootFunctions<To>
    {
        To length = Length<To>();
        if (length == To.Zero) return new Vector2<To>(To.Zero, To.Zero);
        return new Vector2<To>(To.CreateTruncating(X) / length, To.CreateTruncating(Y) / length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(Vector2<T> other) => X == other.X && Y == other.Y;

    public override readonly bool Equals(object? obj) => obj is Vector2<T> other && Equals(other);
    public override readonly int GetHashCode() => HashCode.Combine(X, Y);
    public static bool operator ==(Vector2<T> left, Vector2<T> right) => left.Equals(right);
    public static bool operator !=(Vector2<T> left, Vector2<T> right) => !(left == right);
}