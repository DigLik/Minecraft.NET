using System.Runtime.CompilerServices;

namespace Minecraft.NET.Utils.Math;

public struct BoundingBox<T>(Vector3<T> min, Vector3<T> max)
    where T : unmanaged, INumber<T>
{
    public Vector3<T> Min = min, Max = max;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Intersects(BoundingBox<T> other)
        => Min.X <= other.Max.X && Max.X >= other.Min.X &&
           Min.Y <= other.Max.Y && Max.Y >= other.Min.Y &&
           Min.Z <= other.Max.Z && Max.Z >= other.Min.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly BoundingBox<T> Offset(Vector3<T> offset)
        => new(Min + offset, Max + offset);
}