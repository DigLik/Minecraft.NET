using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Minecraft.NET.Utils.Math;

[StructLayout(LayoutKind.Sequential)]
public struct Vector2Int(int x, int y) : IEquatable<Vector2Int>
{
    public int X = x, Y = y;

    public static readonly Vector2Int Zero = new(0, 0);
    public static readonly Vector2Int One = new(1, 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2Int operator +(in Vector2Int l, in Vector2Int r) => new(l.X + r.X, l.Y + r.Y);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2Int operator -(in Vector2Int l, in Vector2Int r) => new(l.X - r.X, l.Y - r.Y);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2Int operator *(in Vector2Int l, int r) => new(l.X * r, l.Y * r);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2Int operator /(in Vector2Int l, int r) => new(l.X / r, l.Y / r);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2Int operator -(in Vector2Int v) => new(-v.X, -v.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int LengthSquared() => (X * X) + (Y * Y);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float Length() => MathF.Sqrt(LengthSquared());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Vector2(Vector2Int v) => new(v.X, v.Y);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator Vector2Int(Vector2 v) => new((int)v.X, (int)v.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(Vector2Int other) => X == other.X && Y == other.Y;
    public override readonly bool Equals(object? obj) => obj is Vector2Int o && Equals(o);
    public override readonly int GetHashCode() => HashCode.Combine(X, Y);
    public static bool operator ==(Vector2Int l, Vector2Int r) => l.Equals(r);
    public static bool operator !=(Vector2Int l, Vector2Int r) => !l.Equals(r);
}