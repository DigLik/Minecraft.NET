using System.Numerics;
using System.Runtime.CompilerServices;

namespace Minecraft.NET.Utils.Math;

public struct BoundingBox(Vector3 min, Vector3 max)
{
    public Vector3 Min = min, Max = max;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Intersects(in BoundingBox other)
        => Min.X <= other.Max.X &&
           Max.X >= other.Min.X &&
           Min.Y <= other.Max.Y &&
           Max.Y >= other.Min.Y &&
           Min.Z <= other.Max.Z &&
           Max.Z >= other.Min.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly BoundingBox Offset(in Vector3 offset) => new(Min + offset, Max + offset);
}