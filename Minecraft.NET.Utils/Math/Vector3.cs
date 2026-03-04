using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Minecraft.NET.Utils.Math;

/// <summary>
/// Представляет трехмерный вектор. Поддерживает аппаратное ускорение SIMD.
/// </summary>
/// <typeparam name="T">Неуправляемый числовой тип.</typeparam>
/// <remarks>
/// Инициализирует новый экземпляр <see cref="Vector3{T}"/> заданными компонентами.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
[SkipLocalsInit]
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public struct Vector3<T>(T x, T y, T z) : IEquatable<Vector3<T>>
    where T : unmanaged, INumber<T>
{
    /// <summary>X-компонента вектора.</summary>
    public T X = x;
    /// <summary>Y-компонента вектора.</summary>
    public T Y = y;
    /// <summary>Z-компонента вектора.</summary>
    public T Z = z;
    /// <summary>Поле выравнивания структуры для 128-битных регистров.</summary>
    public T _padding = default;

    /// <summary>Возвращает вектор, все компоненты которого равны нулю.</summary>
    public static readonly Vector3<T> Zero = new(T.Zero, T.Zero, T.Zero);

    /// <summary>Возвращает вектор, все компоненты которого равны единице.</summary>
    public static readonly Vector3<T> One = new(T.One, T.One, T.One);

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="Vector3{T}"/>, заполняя все компоненты одним значением.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3(T value) : this(value, value, value) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void operator +=(in Vector3<T> right)
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
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void operator -=(in Vector3<T> right)
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
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void operator *=(in Vector3<T> right)
    {
        if (Vector256.IsHardwareAccelerated && Vector256<T>.Count == 4)
        {
            (Vector256.LoadUnsafe(ref X) * Vector256.LoadUnsafe(ref Unsafe.AsRef(in right.X))).StoreUnsafe(ref X);
            return;
        }
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 4)
        {
            (Vector128.LoadUnsafe(ref X) * Vector128.LoadUnsafe(ref Unsafe.AsRef(in right.X))).StoreUnsafe(ref X);
            return;
        }

        X *= right.X;
        Y *= right.Y;
        Z *= right.Z;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void operator *=(T right)
    {
        if (Vector256.IsHardwareAccelerated && Vector256<T>.Count == 4)
        {
            (Vector256.LoadUnsafe(ref X) * Vector256.Create(right)).StoreUnsafe(ref X);
            return;
        }
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 4)
        {
            (Vector128.LoadUnsafe(ref X) * Vector128.Create(right)).StoreUnsafe(ref X);
            return;
        }

        X *= right;
        Y *= right;
        Z *= right;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void operator /=(T right)
    {
        if (Vector256.IsHardwareAccelerated && Vector256<T>.Count == 4)
        {
            (Vector256.LoadUnsafe(ref X) / Vector256.Create(right)).StoreUnsafe(ref X);
            return;
        }
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 4)
        {
            (Vector128.LoadUnsafe(ref X) / Vector128.Create(right)).StoreUnsafe(ref X);
            return;
        }

        X /= right;
        Y /= right;
        Z /= right;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3<T> operator +(in Vector3<T> left, in Vector3<T> right)
    {
        Vector3<T> res = left; res += right; return res;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3<T> operator -(in Vector3<T> left, in Vector3<T> right)
    {
        Vector3<T> res = left; res -= right; return res;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3<T> operator *(in Vector3<T> left, in Vector3<T> right)
    {
        Vector3<T> res = left; res *= right; return res;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3<T> operator *(in Vector3<T> left, T right)
    {
        Vector3<T> res = left; res *= right; return res;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3<T> operator /(in Vector3<T> left, T right)
    {
        Vector3<T> res = left; res /= right; return res;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3<T> operator -(in Vector3<T> value) => value * -T.One;

    /// <summary>Вычисляет скалярное произведение двух векторов.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Dot(in Vector3<T> left, in Vector3<T> right)
    {
        if (Vector256.IsHardwareAccelerated && Vector256<T>.Count == 4)
        {
            var mul = Vector256.LoadUnsafe(ref Unsafe.AsRef(in left.X)) * Vector256.LoadUnsafe(ref Unsafe.AsRef(in right.X));
            return mul.GetElement(0) + mul.GetElement(1) + mul.GetElement(2);
        }
        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 4)
        {
            var mul = Vector128.LoadUnsafe(ref Unsafe.AsRef(in left.X)) * Vector128.LoadUnsafe(ref Unsafe.AsRef(in right.X));
            return mul.GetElement(0) + mul.GetElement(1) + mul.GetElement(2);
        }

        return (left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z);
    }

    /// <summary>Вычисляет векторное произведение (Cross Product) двух векторов.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3<T> Cross(in Vector3<T> left, in Vector3<T> right)
        => new(
            (left.Y * right.Z) - (left.Z * right.Y),
            (left.Z * right.X) - (left.X * right.Z),
            (left.X * right.Y) - (left.Y * right.X)
        );

    /// <summary>Возвращает квадрат длины вектора.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T LengthSquared() => Dot(this, this);

    /// <summary>Возвращает длину вектора.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly To Length<To>() where To : unmanaged, INumber<To>, IRootFunctions<To>
        => To.Sqrt(To.CreateTruncating(LengthSquared()));

    /// <summary>Нормализует текущий вектор.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector3<To> Normalize<To>() where To : unmanaged, INumber<To>, IRootFunctions<To>
    {
        To len = Length<To>();
        if (len == To.Zero) return new Vector3<To>(To.Zero, To.Zero, To.Zero);
        return new Vector3<To>(To.CreateTruncating(X) / len, To.CreateTruncating(Y) / len, To.CreateTruncating(Z) / len);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(Vector3<T> other) => X == other.X && Y == other.Y && Z == other.Z;
    public override readonly bool Equals(object? obj) => obj is Vector3<T> other && Equals(other);
    public override readonly int GetHashCode() => HashCode.Combine(X, Y, Z);
    public static bool operator ==(Vector3<T> left, Vector3<T> right) => left.Equals(right);
    public static bool operator !=(Vector3<T> left, Vector3<T> right) => !(left == right);
}