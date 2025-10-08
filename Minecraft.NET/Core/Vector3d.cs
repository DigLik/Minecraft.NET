namespace Minecraft.NET.Core;

public struct Vector3d(double x, double y, double z)
{
    public double X { get; set; } = x;
    public double Y { get; set; } = y;
    public double Z { get; set; } = z;

    public static readonly Vector3d Zero = new(0, 0, 0);
    public static readonly Vector3d UnitX = new(1, 0, 0);
    public static readonly Vector3d UnitY = new(0, 1, 0);
    public static readonly Vector3d UnitZ = new(0, 0, 1);

    public static Vector3d operator +(Vector3d a, Vector3d b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3d operator -(Vector3d a, Vector3d b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3d operator *(Vector3d a, double s) => new(a.X * s, a.Y * s, a.Z * s);
    public static Vector3d operator /(Vector3d v, double s) => new(v.X / s, v.Y / s, v.Z / s);

    public readonly double LengthSquared() => X * X + Y * Y + Z * Z;
    public readonly double Length() => Math.Sqrt(LengthSquared());

    public static Vector3d Normalize(Vector3d v)
    {
        double invLength = 1.0 / v.Length();
        return new Vector3d(v.X * invLength, v.Y * invLength, v.Z * invLength);
    }

    public static implicit operator Vector3(Vector3d v) => new((float)v.X, (float)v.Y, (float)v.Z);
    public static implicit operator Vector3d(Vector3 v) => new(v.X, v.Y, v.Z);
}