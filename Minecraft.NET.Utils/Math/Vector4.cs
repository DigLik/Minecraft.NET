using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Minecraft.NET.Utils.Math;

/// <summary>
/// Представляет четырехмерный вектор. Поддерживает аппаратное ускорение SIMD.
/// </summary>
/// <typeparam name="T">Неуправляемый числовой тип.</typeparam>
[StructLayout(LayoutKind.Sequential)]
[SkipLocalsInit]
public struct Vector4<T>(T x, T y, T z, T w) : IEquatable<Vector4<T>>
    where T : unmanaged, INumber<T>
{
    /// <summary>X-компонента вектора.</summary>
    public T X = x;

    /// <summary>Y-компонента вектора.</summary>
    public T Y = y;

    /// <summary>Z-компонента вектора.</summary>
    public T Z = z;

    /// <summary>W-компонента вектора.</summary>
    public T W = w;

    /// <summary>Возвращает вектор, все компоненты которого равны нулю.</summary>
    public static readonly Vector4<T> Zero = new(T.Zero, T.Zero, T.Zero, T.Zero);

    /// <summary>Возвращает вектор, все компоненты которого равны единице.</summary>
    public static readonly Vector4<T> One = new(T.One, T.One, T.One, T.One);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void operator +=(in Vector4<T> right)
    {
        if (Vector256.IsHardwareAccelerated && Vector256<T>.Count == 4)
        {
            (Vector256.LoadUnsafe(ref X) + Vector256.LoadUnsafe(ref Unsafe.AsRef(in right.X))).StoreUnsafe(ref X);
            return;
        }
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 4)
        {
            (Vector128.LoadUnsafe(ref X) + Vector128.LoadUnsafe(ref Unsafe.AsRef(in right.X))).StoreUnsafe(ref X);
            return;
        }

        X += right.X;
        Y += right.Y;
        Z += right.Z;
        W += right.W;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void operator -=(in Vector4<T> right)
    {
        if (Vector256.IsHardwareAccelerated && Vector256<T>.Count == 4)
        {
            (Vector256.LoadUnsafe(ref X) - Vector256.LoadUnsafe(ref Unsafe.AsRef(in right.X))).StoreUnsafe(ref X);
            return;
        }
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 4)
        {
            (Vector128.LoadUnsafe(ref X) - Vector128.LoadUnsafe(ref Unsafe.AsRef(in right.X))).StoreUnsafe(ref X);
            return;
        }

        X -= right.X;
        Y -= right.Y;
        Z -= right.Z;
        W -= right.W;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4<T> operator +(in Vector4<T> left, in Vector4<T> right)
    {
        Vector4<T> result = left;
        result += right;
        return result;
    }

    /// <summary>Вычисляет скалярное произведение двух векторов.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Dot(in Vector4<T> left, in Vector4<T> right)
    {
        if (Vector256.IsHardwareAccelerated && Vector256<T>.Count == 4)
        {
            var mul = Vector256.LoadUnsafe(ref Unsafe.AsRef(in left.X)) * Vector256.LoadUnsafe(ref Unsafe.AsRef(in right.X));
            return mul.GetElement(0) + mul.GetElement(1) + mul.GetElement(2) + mul.GetElement(3);
        }
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 4)
        {
            var mul = Vector128.LoadUnsafe(ref Unsafe.AsRef(in left.X)) * Vector128.LoadUnsafe(ref Unsafe.AsRef(in right.X));
            return mul.GetElement(0) + mul.GetElement(1) + mul.GetElement(2) + mul.GetElement(3);
        }

        return (left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z) + (left.W * right.W);
    }

    /// <summary>Возвращает квадрат длины вектора.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T LengthSquared() => Dot(this, this);

    /// <summary>Возвращает длину вектора.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly To Length<To>() where To : unmanaged, INumber<To>, IRootFunctions<To>
        => To.Sqrt(To.CreateTruncating(LengthSquared()));

    /// <summary>Возвращает нормализованную копию вектора.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4<To> Normalize<To>() where To : unmanaged, INumber<To>, IRootFunctions<To>
    {
        To len = Length<To>();
        if (len == To.Zero) return new Vector4<To>(To.Zero, To.Zero, To.Zero, To.Zero);
        return new Vector4<To>(To.CreateTruncating(X) / len, To.CreateTruncating(Y) / len, To.CreateTruncating(Z) / len, To.CreateTruncating(W) / len);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(Vector4<T> other) => X == other.X && Y == other.Y && Z == other.Z && W == other.W;

    public override readonly bool Equals(object? obj) => obj is Vector4<T> other && Equals(other);
    public override readonly int GetHashCode() => HashCode.Combine(X, Y, Z, W);
    public static bool operator ==(Vector4<T> left, Vector4<T> right) => left.Equals(right);
    public static bool operator !=(Vector4<T> left, Vector4<T> right) => !(left == right);
}