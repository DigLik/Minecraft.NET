using System.Runtime.CompilerServices;
using Plane = System.Numerics.Plane;

namespace Minecraft.NET.Graphics;

public readonly record struct BoundingBox(Vector3 Min, Vector3 Max);
public readonly record struct BoundingSphere(Vector3 Center, float Radius);

public class Frustum
{
    private readonly float[] _nx = new float[8]; // Нормали X
    private readonly float[] _ny = new float[8]; // Нормали Y
    private readonly float[] _nz = new float[8]; // Нормали Z
    private readonly float[] _d = new float[8];  // Расстояния

    public void Update(Matrix4x4 vpMatrix)
    {
        var planes = new Plane[6];

        planes[0] = Plane.Normalize(new Plane(
            vpMatrix.M14 + vpMatrix.M11, vpMatrix.M24 + vpMatrix.M21,
            vpMatrix.M34 + vpMatrix.M31, vpMatrix.M44 + vpMatrix.M41)); // Left

        planes[1] = Plane.Normalize(new Plane(
            vpMatrix.M14 - vpMatrix.M11, vpMatrix.M24 - vpMatrix.M21,
            vpMatrix.M34 - vpMatrix.M31, vpMatrix.M44 - vpMatrix.M41)); // Right

        planes[2] = Plane.Normalize(new Plane(
            vpMatrix.M14 + vpMatrix.M12, vpMatrix.M24 + vpMatrix.M22,
            vpMatrix.M34 + vpMatrix.M32, vpMatrix.M44 + vpMatrix.M42)); // Bottom

        planes[3] = Plane.Normalize(new Plane(
            vpMatrix.M14 - vpMatrix.M12, vpMatrix.M24 - vpMatrix.M22,
            vpMatrix.M34 - vpMatrix.M32, vpMatrix.M44 - vpMatrix.M42)); // Top

        planes[4] = Plane.Normalize(new Plane(
            vpMatrix.M13, vpMatrix.M23, vpMatrix.M33, vpMatrix.M43)); // Near

        planes[5] = Plane.Normalize(new Plane(
            vpMatrix.M14 - vpMatrix.M13, vpMatrix.M24 - vpMatrix.M23,
            vpMatrix.M34 - vpMatrix.M33, vpMatrix.M44 - vpMatrix.M43)); // Far

        for (int i = 0; i < 6; i++)
        {
            _nx[i] = planes[i].Normal.X;
            _ny[i] = planes[i].Normal.Y;
            _nz[i] = planes[i].Normal.Z;
            _d[i] = planes[i].D;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Intersects(in BoundingSphere sphere)
    {
        for (int i = 0; i < 6; i++)
        {
            float dist = _nx[i] * sphere.Center.X + _ny[i] * sphere.Center.Y + _nz[i] * sphere.Center.Z + _d[i];
            if (dist < -sphere.Radius)
                return false;
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Intersects(in BoundingBox box)
    {
        var min_v = new Vector<float>(box.Min.X);
        var max_v = new Vector<float>(box.Max.X);
        var pVertexX = Vector.ConditionalSelect(Vector.GreaterThan(new Vector<float>(_nx, 0), Vector<float>.Zero), max_v, min_v);

        min_v = new Vector<float>(box.Min.Y);
        max_v = new Vector<float>(box.Max.Y);
        var pVertexY = Vector.ConditionalSelect(Vector.GreaterThan(new Vector<float>(_ny, 0), Vector<float>.Zero), max_v, min_v);

        min_v = new Vector<float>(box.Min.Z);
        max_v = new Vector<float>(box.Max.Z);
        var pVertexZ = Vector.ConditionalSelect(Vector.GreaterThan(new Vector<float>(_nz, 0), Vector<float>.Zero), max_v, min_v);

        var dot = pVertexX * new Vector<float>(_nx, 0) +
                  pVertexY * new Vector<float>(_ny, 0) +
                  pVertexZ * new Vector<float>(_nz, 0) +
                  new Vector<float>(_d, 0);

        if (Vector.LessThanAny(dot, Vector<float>.Zero))
            return false;

        if (Vector<float>.Count < 8)
        {
            min_v = new Vector<float>(box.Min.X);
            max_v = new Vector<float>(box.Max.X);
            pVertexX = Vector.ConditionalSelect(Vector.GreaterThan(new Vector<float>(_nx, 4), Vector<float>.Zero), max_v, min_v);

            min_v = new Vector<float>(box.Min.Y);
            max_v = new Vector<float>(box.Max.Y);
            pVertexY = Vector.ConditionalSelect(Vector.GreaterThan(new Vector<float>(_ny, 4), Vector<float>.Zero), max_v, min_v);

            min_v = new Vector<float>(box.Min.Z);
            max_v = new Vector<float>(box.Max.Z);
            pVertexZ = Vector.ConditionalSelect(Vector.GreaterThan(new Vector<float>(_nz, 4), Vector<float>.Zero), max_v, min_v);

            dot = pVertexX * new Vector<float>(_nx, 4) +
                  pVertexY * new Vector<float>(_ny, 4) +
                  pVertexZ * new Vector<float>(_nz, 4) +
                  new Vector<float>(_d, 4);

            if (dot[0] < 0 || dot[1] < 0)
                return false;
        }

        return true;
    }
}