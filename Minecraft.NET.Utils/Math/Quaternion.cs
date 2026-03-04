using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Minecraft.NET.Utils.Math;

/// <summary>
/// Представляет кватернион для вычисления вращений в трехмерном пространстве.
/// </summary>
/// <typeparam name="T">Неуправляемый числовой тип, поддерживающий тригонометрию.</typeparam>
[StructLayout(LayoutKind.Sequential)]
[SkipLocalsInit]
public struct Quaternion<T>(T x, T y, T z, T w) : IEquatable<Quaternion<T>>
    where T : unmanaged, INumber<T>, ITrigonometricFunctions<T>, IRootFunctions<T>
{
    /// <summary>X-компонента вектора кватерниона.</summary>
    public T X = x;

    /// <summary>Y-компонента вектора кватерниона.</summary>
    public T Y = y;

    /// <summary>Z-компонента вектора кватерниона.</summary>
    public T Z = z;

    /// <summary>Скалярная компонента W.</summary>
    public T W = w;

    /// <summary>Возвращает единичный (отсутствующий поворот) кватернион.</summary>
    public static readonly Quaternion<T> Identity = new(T.Zero, T.Zero, T.Zero, T.One);

    /// <summary>Перемножает два кватерниона (комбинирует вращения).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quaternion<T> operator *(in Quaternion<T> left, in Quaternion<T> right)
    {
        return new Quaternion<T>(
            left.W * right.X + left.X * right.W + left.Y * right.Z - left.Z * right.Y,
            left.W * right.Y - left.X * right.Z + left.Y * right.W + left.Z * right.X,
            left.W * right.Z + left.X * right.Y - left.Y * right.X + left.Z * right.W,
            left.W * right.W - left.X * right.X - left.Y * right.Y - left.Z * right.Z
        );
    }

    /// <summary>Вычисляет скалярное произведение двух кватернионов.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Dot(in Quaternion<T> left, in Quaternion<T> right)
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

    /// <summary>Возвращает длину кватерниона.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T Length() => T.Sqrt(Dot(this, this));

    /// <summary>Нормализует кватернион.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Quaternion<T> Normalize()
    {
        T len = Length();
        if (len == T.Zero) return Identity;
        return new Quaternion<T>(X / len, Y / len, Z / len, W / len);
    }

    /// <summary>Сферическая линейная интерполяция между двумя кватернионами.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quaternion<T> Slerp(in Quaternion<T> q1, in Quaternion<T> q2, T t)
    {
        T cosHalfTheta = Dot(q1, q2);
        Quaternion<T> target = q2;

        if (cosHalfTheta < T.Zero)
        {
            target = new Quaternion<T>(-target.X, -target.Y, -target.Z, -target.W);
            cosHalfTheta = -cosHalfTheta;
        }

        T threshold = T.CreateTruncating(0.999);
        if (cosHalfTheta >= threshold)
        {
            T tInv = T.One - t;
            return new Quaternion<T>(
                q1.X * tInv + target.X * t,
                q1.Y * tInv + target.Y * t,
                q1.Z * tInv + target.Z * t,
                q1.W * tInv + target.W * t
            ).Normalize();
        }

        T halfTheta = T.Acos(cosHalfTheta);
        T sinHalfTheta = T.Sqrt(T.One - cosHalfTheta * cosHalfTheta);

        T ratioA = T.Sin((T.One - t) * halfTheta) / sinHalfTheta;
        T ratioB = T.Sin(t * halfTheta) / sinHalfTheta;

        return new Quaternion<T>(
            q1.X * ratioA + target.X * ratioB,
            q1.Y * ratioA + target.Y * ratioB,
            q1.Z * ratioA + target.Z * ratioB,
            q1.W * ratioA + target.W * ratioB
        );
    }

    public readonly bool Equals(Quaternion<T> other) => X == other.X && Y == other.Y && Z == other.Z && W == other.W;
    public override readonly bool Equals(object? obj) => obj is Quaternion<T> other && Equals(other);
    public override readonly int GetHashCode() => HashCode.Combine(X, Y, Z, W);
    public static bool operator ==(Quaternion<T> left, Quaternion<T> right) => left.Equals(right);
    public static bool operator !=(Quaternion<T> left, Quaternion<T> right) => !(left == right);
}