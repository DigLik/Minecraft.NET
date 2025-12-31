using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using Plane = System.Numerics.Plane;

namespace Minecraft.NET.Graphics;

public readonly record struct BoundingBox(Vector3 Min, Vector3 Max);
public readonly record struct BoundingSphere(Vector3 Center, float Radius);

public unsafe class Frustum
{
    private Vector256<float> _nx, _ny, _nz, _d;

    public Vector256<float> YIncrement;
    public Vector256<float> SectionExtentProjection;
    public Vector256<float> ColumnExtentProjection;

    public void Update(Matrix4x4 vpMatrix)
    {
        Span<Plane> planes = stackalloc Plane[8];
        planes[0] = Plane.Normalize(new Plane(
            vpMatrix.M14 + vpMatrix.M11, vpMatrix.M24 + vpMatrix.M21,
            vpMatrix.M34 + vpMatrix.M31, vpMatrix.M44 + vpMatrix.M41));
        planes[1] = Plane.Normalize(new Plane(
            vpMatrix.M14 - vpMatrix.M11, vpMatrix.M24 - vpMatrix.M21,
            vpMatrix.M34 - vpMatrix.M31, vpMatrix.M44 - vpMatrix.M41));
        planes[2] = Plane.Normalize(new Plane(
            vpMatrix.M14 + vpMatrix.M12, vpMatrix.M24 + vpMatrix.M22,
            vpMatrix.M34 + vpMatrix.M32, vpMatrix.M44 + vpMatrix.M42));
        planes[3] = Plane.Normalize(new Plane(
            vpMatrix.M14 - vpMatrix.M12, vpMatrix.M24 - vpMatrix.M22,
            vpMatrix.M34 - vpMatrix.M32, vpMatrix.M44 - vpMatrix.M42));
        planes[4] = Plane.Normalize(new Plane(
            vpMatrix.M13, vpMatrix.M23, vpMatrix.M33, vpMatrix.M43));
        planes[5] = Plane.Normalize(new Plane(
            vpMatrix.M14 - vpMatrix.M13, vpMatrix.M24 - vpMatrix.M23,
            vpMatrix.M34 - vpMatrix.M33, vpMatrix.M44 - vpMatrix.M43));
        planes[6] = planes[0];
        planes[7] = planes[0];

        float* nxPtr = stackalloc float[8];
        float* nyPtr = stackalloc float[8];
        float* nzPtr = stackalloc float[8];
        float* dPtr = stackalloc float[8];

        for (int i = 0; i < 8; i++)
        {
            nxPtr[i] = planes[i].Normal.X;
            nyPtr[i] = planes[i].Normal.Y;
            nzPtr[i] = planes[i].Normal.Z;
            dPtr[i] = planes[i].D;
        }

        _nx = Vector256.Load(nxPtr);
        _ny = Vector256.Load(nyPtr);
        _nz = Vector256.Load(nzPtr);
        _d = Vector256.Load(dPtr);

        YIncrement = _ny * 16.0f;

        var absNx = Vector256.Abs(_nx);
        var absNy = Vector256.Abs(_ny);
        var absNz = Vector256.Abs(_nz);

        var extent8 = Vector256.Create(8.0f);
        SectionExtentProjection = absNx * extent8 + absNy * extent8 + absNz * extent8;

        var extentHeight = Vector256.Create(WorldHeightInBlocks / 2.0f);
        ColumnExtentProjection = absNx * extent8 + absNy * extentHeight + absNz * extent8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector256<float> GetDistances(float x, float y, float z)
    {
        var vx = Vector256.Create(x);
        var vy = Vector256.Create(y);
        var vz = Vector256.Create(z);
        return vx * _nx + vy * _ny + vz * _nz + _d;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IntersectsColumn(float x, float y, float z)
    {
        var cx = Vector256.Create(x);
        var cy = Vector256.Create(y);
        var cz = Vector256.Create(z);

        var d = cx * _nx + cy * _ny + cz * _nz + _d;

        return !Vector256.LessThanAny(d + ColumnExtentProjection, Vector256<float>.Zero);
    }
}