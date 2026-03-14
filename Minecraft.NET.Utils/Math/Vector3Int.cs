using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Minecraft.NET.Utils.Math;

[StructLayout(LayoutKind.Sequential)]
public record struct Vector3Int(int X, int Y, int Z)
{
    public static readonly Vector3Int Zero = new(0, 0, 0);
    public static readonly Vector3Int One = new(1, 1, 1);

    public Vector3Int(int value) : this(value, value, value) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3Int operator +(in Vector3Int l, in Vector3Int r) => new(l.X + r.X, l.Y + r.Y, l.Z + r.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3Int operator -(in Vector3Int l, in Vector3Int r) => new(l.X - r.X, l.Y - r.Y, l.Z - r.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3Int operator *(in Vector3Int l, in Vector3Int r) => new(l.X * r.X, l.Y * r.Y, l.Z * r.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3Int operator *(in Vector3Int l, int r) => new(l.X * r, l.Y * r, l.Z * r);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3Int operator /(in Vector3Int l, int r) => new(l.X / r, l.Y / r, l.Z / r);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3Int operator -(in Vector3Int v) => new(-v.X, -v.Y, -v.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Dot(in Vector3Int l, in Vector3Int r) => (l.X * r.X) + (l.Y * r.Y) + (l.Z * r.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3Int Cross(in Vector3Int l, in Vector3Int r) => new((l.Y * r.Z) - (l.Z * r.Y), (l.Z * r.X) - (l.X * r.Z), (l.X * r.Y) - (l.Y * r.X));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int LengthSquared() => Dot(this, this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float Length() => MathF.Sqrt(LengthSquared());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Vector3(Vector3Int v) => new(v.X, v.Y, v.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator Vector3Int(Vector3 v) => new((int)v.X, (int)v.Y, (int)v.Z);
}