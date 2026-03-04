using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Minecraft.NET.Utils.Math;

/// <summary>
/// Представляет выровненный по осям ограничивающий параллелепипед (AABB).
/// </summary>
/// <typeparam name="T">Неуправляемый числовой тип.</typeparam>
public struct BoundingBox<T>(Vector3<T> min, Vector3<T> max)
    where T : unmanaged, INumber<T>
{
    /// <summary>Вектор минимальных координат.</summary>
    public Vector3<T> Min = min;

    /// <summary>Вектор максимальных координат.</summary>
    public Vector3<T> Max = max;

    /// <summary>
    /// Проверяет пересечение данного параллелепипеда с другим.
    /// </summary>
    /// <param name="other">Другой параллелепипед для проверки.</param>
    /// <returns>True, если есть пересечение, иначе False.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Intersects(in BoundingBox<T> other)
    {
        if (Vector256.IsHardwareAccelerated && Vector256<T>.Count == 4)
        {
            var min1 = Vector256.LoadUnsafe(ref Unsafe.AsRef(in Min.X));
            var max2 = Vector256.LoadUnsafe(ref Unsafe.AsRef(in other.Max.X));
            var max1 = Vector256.LoadUnsafe(ref Unsafe.AsRef(in Max.X));
            var min2 = Vector256.LoadUnsafe(ref Unsafe.AsRef(in other.Min.X));

            var cmp1 = Vector256.LessThanOrEqual(min1, max2);
            var cmp2 = Vector256.GreaterThanOrEqual(max1, min2);
            var intersect = Vector256.BitwiseAnd(cmp1, cmp2);

            return (Vector256.ExtractMostSignificantBits(intersect) & 7) == 7;
        }

        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 4)
        {
            var min1 = Vector128.LoadUnsafe(ref Unsafe.AsRef(in Min.X));
            var max2 = Vector128.LoadUnsafe(ref Unsafe.AsRef(in other.Max.X));
            var max1 = Vector128.LoadUnsafe(ref Unsafe.AsRef(in Max.X));
            var min2 = Vector128.LoadUnsafe(ref Unsafe.AsRef(in other.Min.X));

            var cmp1 = Vector128.LessThanOrEqual(min1, max2);
            var cmp2 = Vector128.GreaterThanOrEqual(max1, min2);
            var intersect = Vector128.BitwiseAnd(cmp1, cmp2);

            return (Vector128.ExtractMostSignificantBits(intersect) & 7) == 7;
        }

        return Min.X <= other.Max.X && Max.X >= other.Min.X &&
               Min.Y <= other.Max.Y && Max.Y >= other.Min.Y &&
               Min.Z <= other.Max.Z && Max.Z >= other.Min.Z;
    }

    /// <summary>Смещает параллелепипед в пространстве на заданный вектор.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly BoundingBox<T> Offset(in Vector3<T> offset) => new(Min + offset, Max + offset);
}