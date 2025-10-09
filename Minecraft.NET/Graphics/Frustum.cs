using Plane = System.Numerics.Plane;

namespace Minecraft.NET.Graphics;

public readonly record struct BoundingBox(Vector3 Min, Vector3 Max);

public class Frustum
{
    private readonly Plane[] _planes = new Plane[6];

    public void Update(Matrix4x4 vpMatrix)
    {
        _planes[0] = Plane.Normalize(new Plane(
            vpMatrix.M14 + vpMatrix.M11,
            vpMatrix.M24 + vpMatrix.M21,
            vpMatrix.M34 + vpMatrix.M31,
            vpMatrix.M44 + vpMatrix.M41));

        _planes[1] = Plane.Normalize(new Plane(
            vpMatrix.M14 - vpMatrix.M11,
            vpMatrix.M24 - vpMatrix.M21,
            vpMatrix.M34 - vpMatrix.M31,
            vpMatrix.M44 - vpMatrix.M41));

        _planes[2] = Plane.Normalize(new Plane(
            vpMatrix.M14 + vpMatrix.M12,
            vpMatrix.M24 + vpMatrix.M22,
            vpMatrix.M34 + vpMatrix.M32,
            vpMatrix.M44 + vpMatrix.M42));

        _planes[3] = Plane.Normalize(new Plane(
            vpMatrix.M14 - vpMatrix.M12,
            vpMatrix.M24 - vpMatrix.M22,
            vpMatrix.M34 - vpMatrix.M32,
            vpMatrix.M44 - vpMatrix.M42));

        _planes[4] = Plane.Normalize(new Plane(
            vpMatrix.M13,
            vpMatrix.M23,
            vpMatrix.M33,
            vpMatrix.M43));

        _planes[5] = Plane.Normalize(new Plane(
            vpMatrix.M14 - vpMatrix.M13,
            vpMatrix.M24 - vpMatrix.M23,
            vpMatrix.M34 - vpMatrix.M33,
            vpMatrix.M44 - vpMatrix.M43));
    }

    public bool Intersects(in BoundingBox box)
    {
        foreach (var plane in _planes)
        {
            Vector3 pVertex = new(
                plane.Normal.X > 0 ? box.Max.X : box.Min.X,
                plane.Normal.Y > 0 ? box.Max.Y : box.Min.Y,
                plane.Normal.Z > 0 ? box.Max.Z : box.Min.Z
            );

            if (Plane.DotCoordinate(plane, pVertex) < 0)
            {
                return false;
            }
        }

        return true;
    }
}