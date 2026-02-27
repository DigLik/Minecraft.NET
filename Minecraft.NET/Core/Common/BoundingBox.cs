namespace Minecraft.NET.Core.Common;

public readonly record struct BoundingBox<T>(Vector3<T> Min, Vector3<T> Max)
    where T : unmanaged, INumber<T>;
