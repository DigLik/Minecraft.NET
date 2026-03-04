using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Minecraft.NET.Utils.Math;

/// <summary>
/// Представляет матрицу 4x4. Хранится построчно (Row-Major).
/// </summary>
/// <typeparam name="T">Неуправляемый числовой тип.</typeparam>
[StructLayout(LayoutKind.Sequential)]
[SkipLocalsInit]
public struct Matrix4x4<T>(
    T m11, T m12, T m13, T m14,
    T m21, T m22, T m23, T m24,
    T m31, T m32, T m33, T m34,
    T m41, T m42, T m43, T m44) : IEquatable<Matrix4x4<T>>
    where T : unmanaged, INumber<T>
{
    public T M11 = m11, M12 = m12, M13 = m13, M14 = m14;
    public T M21 = m21, M22 = m22, M23 = m23, M24 = m24;
    public T M31 = m31, M32 = m32, M33 = m33, M34 = m34;
    public T M41 = m41, M42 = m42, M43 = m43, M44 = m44;

    /// <summary>Единичная матрица (Identity).</summary>
    public static readonly Matrix4x4<T> Identity = new(
        T.One, T.Zero, T.Zero, T.Zero,
        T.Zero, T.One, T.Zero, T.Zero,
        T.Zero, T.Zero, T.One, T.Zero,
        T.Zero, T.Zero, T.Zero, T.One
    );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void operator +=(in Matrix4x4<T> right)
    {
        if (Vector256.IsHardwareAccelerated && Vector256<T>.Count == 8)
        {
            (Vector256.LoadUnsafe(ref M11) + Vector256.LoadUnsafe(ref Unsafe.AsRef(in right.M11))).StoreUnsafe(ref M11);
            (Vector256.LoadUnsafe(ref M31) + Vector256.LoadUnsafe(ref Unsafe.AsRef(in right.M31))).StoreUnsafe(ref M31);
            return;
        }
        if (Vector256.IsHardwareAccelerated && Vector256<T>.Count == 4)
        {
            (Vector256.LoadUnsafe(ref M11) + Vector256.LoadUnsafe(ref Unsafe.AsRef(in right.M11))).StoreUnsafe(ref M11);
            (Vector256.LoadUnsafe(ref M21) + Vector256.LoadUnsafe(ref Unsafe.AsRef(in right.M21))).StoreUnsafe(ref M21);
            (Vector256.LoadUnsafe(ref M31) + Vector256.LoadUnsafe(ref Unsafe.AsRef(in right.M31))).StoreUnsafe(ref M31);
            (Vector256.LoadUnsafe(ref M41) + Vector256.LoadUnsafe(ref Unsafe.AsRef(in right.M41))).StoreUnsafe(ref M41);
            return;
        }

        M11 += right.M11; M12 += right.M12; M13 += right.M13; M14 += right.M14;
        M21 += right.M21; M22 += right.M22; M23 += right.M23; M24 += right.M24;
        M31 += right.M31; M32 += right.M32; M33 += right.M33; M34 += right.M34;
        M41 += right.M41; M42 += right.M42; M43 += right.M43; M44 += right.M44;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix4x4<T> operator *(in Matrix4x4<T> left, in Matrix4x4<T> right)
    {
        Matrix4x4<T> result = default;

        if (Vector256.IsHardwareAccelerated && Vector256<T>.Count == 4)
        {
            for (int i = 0; i < 4; i++)
            {
                ref T leftRow = ref Unsafe.Add(ref Unsafe.AsRef(in left.M11), i * 4);
                var resRow = Vector256.LoadUnsafe(ref Unsafe.AsRef(in right.M11)) * Vector256.Create(Unsafe.Add(ref leftRow, 0));
                resRow += Vector256.LoadUnsafe(ref Unsafe.AsRef(in right.M21)) * Vector256.Create(Unsafe.Add(ref leftRow, 1));
                resRow += Vector256.LoadUnsafe(ref Unsafe.AsRef(in right.M31)) * Vector256.Create(Unsafe.Add(ref leftRow, 2));
                resRow += Vector256.LoadUnsafe(ref Unsafe.AsRef(in right.M41)) * Vector256.Create(Unsafe.Add(ref leftRow, 3));
                resRow.StoreUnsafe(ref Unsafe.Add(ref result.M11, i * 4));
            }
            return result;
        }

        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 4)
        {
            for (int i = 0; i < 4; i++)
            {
                ref T leftRow = ref Unsafe.Add(ref Unsafe.AsRef(in left.M11), i * 4);
                var resRow = Vector128.LoadUnsafe(ref Unsafe.AsRef(in right.M11)) * Vector128.Create(Unsafe.Add(ref leftRow, 0));
                resRow += Vector128.LoadUnsafe(ref Unsafe.AsRef(in right.M21)) * Vector128.Create(Unsafe.Add(ref leftRow, 1));
                resRow += Vector128.LoadUnsafe(ref Unsafe.AsRef(in right.M31)) * Vector128.Create(Unsafe.Add(ref leftRow, 2));
                resRow += Vector128.LoadUnsafe(ref Unsafe.AsRef(in right.M41)) * Vector128.Create(Unsafe.Add(ref leftRow, 3));
                resRow.StoreUnsafe(ref Unsafe.Add(ref result.M11, i * 4));
            }
            return result;
        }

        result.M11 = (left.M11 * right.M11) + (left.M12 * right.M21) + (left.M13 * right.M31) + (left.M14 * right.M41);
        result.M12 = (left.M11 * right.M12) + (left.M12 * right.M22) + (left.M13 * right.M32) + (left.M14 * right.M42);
        result.M13 = (left.M11 * right.M13) + (left.M12 * right.M23) + (left.M13 * right.M33) + (left.M14 * right.M43);
        result.M14 = (left.M11 * right.M14) + (left.M12 * right.M24) + (left.M13 * right.M34) + (left.M14 * right.M44);

        result.M21 = (left.M21 * right.M11) + (left.M22 * right.M21) + (left.M23 * right.M31) + (left.M24 * right.M41);
        result.M22 = (left.M21 * right.M12) + (left.M22 * right.M22) + (left.M23 * right.M32) + (left.M24 * right.M42);
        result.M23 = (left.M21 * right.M13) + (left.M22 * right.M23) + (left.M23 * right.M33) + (left.M24 * right.M43);
        result.M24 = (left.M21 * right.M14) + (left.M22 * right.M24) + (left.M23 * right.M34) + (left.M24 * right.M44);

        result.M31 = (left.M31 * right.M11) + (left.M32 * right.M21) + (left.M33 * right.M31) + (left.M34 * right.M41);
        result.M32 = (left.M31 * right.M12) + (left.M32 * right.M22) + (left.M33 * right.M32) + (left.M34 * right.M42);
        result.M33 = (left.M31 * right.M13) + (left.M32 * right.M23) + (left.M33 * right.M33) + (left.M34 * right.M43);
        result.M34 = (left.M31 * right.M14) + (left.M32 * right.M24) + (left.M33 * right.M34) + (left.M34 * right.M44);

        result.M41 = (left.M41 * right.M11) + (left.M42 * right.M21) + (left.M43 * right.M31) + (left.M44 * right.M41);
        result.M42 = (left.M41 * right.M12) + (left.M42 * right.M22) + (left.M43 * right.M32) + (left.M44 * right.M42);
        result.M43 = (left.M41 * right.M13) + (left.M42 * right.M23) + (left.M43 * right.M33) + (left.M44 * right.M43);
        result.M44 = (left.M41 * right.M14) + (left.M42 * right.M24) + (left.M43 * right.M34) + (left.M44 * right.M44);

        return result;
    }

    /// <summary>Создает матрицу вида камеры (LookAt).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix4x4<T> CreateLookAt<To>(in Vector3<T> cameraPosition, in Vector3<T> cameraTarget, in Vector3<T> cameraUpVector)
        where To : unmanaged, INumber<To>, IRootFunctions<To>
    {
        var dirTo = cameraTarget - cameraPosition;
        var zDir = dirTo.Normalize<To>();
        var zAxis = new Vector3<T>(T.CreateTruncating(zDir.X), T.CreateTruncating(zDir.Y), T.CreateTruncating(zDir.Z));
        var xDir = Vector3<T>.Cross(zAxis, cameraUpVector).Normalize<To>();
        var xAxis = new Vector3<T>(T.CreateTruncating(xDir.X), T.CreateTruncating(xDir.Y), T.CreateTruncating(xDir.Z));
        var yAxis = Vector3<T>.Cross(xAxis, zAxis);
        return new Matrix4x4<T>(
            xAxis.X, yAxis.X, zAxis.X, T.Zero,
            xAxis.Y, yAxis.Y, zAxis.Y, T.Zero,
            xAxis.Z, yAxis.Z, zAxis.Z, T.Zero,
            -Vector3<T>.Dot(xAxis, cameraPosition),
            -Vector3<T>.Dot(yAxis, cameraPosition),
            -Vector3<T>.Dot(zAxis, cameraPosition),
            T.One
        );
    }

    /// <summary>Создает матрицу перспективной проекции.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix4x4<T> CreatePerspectiveFieldOfView<To>(T fieldOfView, T aspectRatio, T nearPlaneDistance, T farPlaneDistance)
        where To : unmanaged, INumber<To>, ITrigonometricFunctions<To>
    {
        To halfFov = To.CreateTruncating(fieldOfView) / To.CreateChecked(2);
        T yScale = T.CreateTruncating(To.One / To.Tan(halfFov));
        T xScale = yScale / aspectRatio;

        Matrix4x4<T> result = default;
        result.M11 = xScale;
        result.M22 = yScale;
        result.M33 = farPlaneDistance / (farPlaneDistance - nearPlaneDistance);
        result.M34 = T.One;
        result.M43 = -(nearPlaneDistance * farPlaneDistance) / (farPlaneDistance - nearPlaneDistance);

        return result;
    }

    /// <summary>Инвертирует матрицу.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe bool Invert(in Matrix4x4<T> matrix, out Matrix4x4<T> result)
    {
        if (Vector256.IsHardwareAccelerated && Vector256<T>.Count == 4)
        {
            ref T m = ref Unsafe.AsRef(in matrix.M11);
            var v0 = Vector256.LoadUnsafe(ref m, 0);
            var v1 = Vector256.LoadUnsafe(ref m, 4);
            var v2 = Vector256.LoadUnsafe(ref m, 8);
            var v3 = Vector256.LoadUnsafe(ref m, 12);

            T m11_256 = v0.GetElement(0); T m12_256 = v0.GetElement(1); T m13_256 = v0.GetElement(2); T m14_256 = v0.GetElement(3);
            T m21_256 = v1.GetElement(0); T m22_256 = v1.GetElement(1); T m23_256 = v1.GetElement(2); T m24_256 = v1.GetElement(3);
            T m31_256 = v2.GetElement(0); T m32_256 = v2.GetElement(1); T m33_256 = v2.GetElement(2); T m34_256 = v2.GetElement(3);
            T m41_256 = v3.GetElement(0); T m42_256 = v3.GetElement(1); T m43_256 = v3.GetElement(2); T m44_256 = v3.GetElement(3);

            T b00 = m11_256 * m22_256 - m12_256 * m21_256;
            T b01 = m11_256 * m23_256 - m13_256 * m21_256;
            T b02 = m11_256 * m24_256 - m14_256 * m21_256;
            T b03 = m12_256 * m23_256 - m13_256 * m22_256;
            T b04 = m12_256 * m24_256 - m14_256 * m22_256;
            T b05 = m13_256 * m24_256 - m14_256 * m23_256;
            T b06 = m31_256 * m42_256 - m32_256 * m41_256;
            T b07 = m31_256 * m43_256 - m33_256 * m41_256;
            T b08 = m31_256 * m44_256 - m34_256 * m41_256;
            T b09 = m32_256 * m43_256 - m33_256 * m42_256;
            T b10 = m32_256 * m44_256 - m34_256 * m42_256;
            T b11 = m33_256 * m44_256 - m34_256 * m43_256;

            T det = b00 * b11 - b01 * b10 + b02 * b09 + b03 * b08 - b04 * b07 + b05 * b06;
            if (det == T.Zero)
            {
                result = default;
                return false;
            }

            T invDet = T.One / det;
            var vInvDet = Vector256.Create(invDet);

            T* t0 = stackalloc T[4];
            T* t1 = stackalloc T[4];
            T* t2 = stackalloc T[4];
            T* t3 = stackalloc T[4];
            T* t4 = stackalloc T[4];
            T* t5 = stackalloc T[4];

            t0[0] = m22_256; t0[1] = m13_256; t0[2] = m42_256; t0[3] = m33_256;
            t1[0] = b11; t1[1] = b10; t1[2] = b05; t1[3] = b04;
            t2[0] = m23_256; t2[1] = m12_256; t2[2] = m43_256; t2[3] = m32_256;
            t3[0] = b10; t3[1] = b11; t3[2] = b04; t3[3] = b05;
            t4[0] = m24_256; t4[1] = -m14_256; t4[2] = m44_256; t4[3] = -m34_256;
            t5[0] = b09; t5[1] = b09; t5[2] = b03; t5[3] = b03;

            var r0 = (Vector256.LoadUnsafe(ref *t0) * Vector256.LoadUnsafe(ref *t1)
                    - Vector256.LoadUnsafe(ref *t2) * Vector256.LoadUnsafe(ref *t3)
                    + Vector256.LoadUnsafe(ref *t4) * Vector256.LoadUnsafe(ref *t5)) * vInvDet;

            t0[0] = m23_256; t0[1] = m11_256; t0[2] = m43_256; t0[3] = m31_256;
            t1[0] = b08; t1[1] = b11; t1[2] = b02; t1[3] = b05;
            t2[0] = m21_256; t2[1] = m13_256; t2[2] = m41_256; t2[3] = m33_256;
            t3[0] = b11; t3[1] = b08; t3[2] = b05; t3[3] = b02;
            t4[0] = -m24_256; t4[1] = m14_256; t4[2] = -m44_256; t4[3] = m34_256;
            t5[0] = b07; t5[1] = b07; t5[2] = b01; t5[3] = b01;

            var r1 = (Vector256.LoadUnsafe(ref *t0) * Vector256.LoadUnsafe(ref *t1)
                    - Vector256.LoadUnsafe(ref *t2) * Vector256.LoadUnsafe(ref *t3)
                    + Vector256.LoadUnsafe(ref *t4) * Vector256.LoadUnsafe(ref *t5)) * vInvDet;

            t0[0] = m21_256; t0[1] = m12_256; t0[2] = m41_256; t0[3] = m32_256;
            t1[0] = b10; t1[1] = b08; t1[2] = b04; t1[3] = b02;
            t2[0] = m22_256; t2[1] = m11_256; t2[2] = m42_256; t2[3] = m31_256;
            t3[0] = b08; t3[1] = b10; t3[2] = b02; t3[3] = b04;
            t4[0] = m24_256; t4[1] = -m14_256; t4[2] = m44_256; t4[3] = -m34_256;
            t5[0] = b06; t5[1] = b06; t5[2] = b00; t5[3] = b00;

            var r2 = (Vector256.LoadUnsafe(ref *t0) * Vector256.LoadUnsafe(ref *t1)
                    - Vector256.LoadUnsafe(ref *t2) * Vector256.LoadUnsafe(ref *t3)
                    + Vector256.LoadUnsafe(ref *t4) * Vector256.LoadUnsafe(ref *t5)) * vInvDet;

            t0[0] = m22_256; t0[1] = m11_256; t0[2] = m42_256; t0[3] = m31_256;
            t1[0] = b07; t1[1] = b09; t1[2] = b01; t1[3] = b03;
            t2[0] = m21_256; t2[1] = m12_256; t2[2] = m41_256; t2[3] = m32_256;
            t3[0] = b09; t3[1] = b07; t3[2] = b03; t3[3] = b01;
            t4[0] = -m23_256; t4[1] = m13_256; t4[2] = -m43_256; t4[3] = m33_256;
            t5[0] = b06; t5[1] = b06; t5[2] = b00; t5[3] = b00;

            var r3 = (Vector256.LoadUnsafe(ref *t0) * Vector256.LoadUnsafe(ref *t1)
                    - Vector256.LoadUnsafe(ref *t2) * Vector256.LoadUnsafe(ref *t3)
                    + Vector256.LoadUnsafe(ref *t4) * Vector256.LoadUnsafe(ref *t5)) * vInvDet;

            result = default;
            ref T resRef = ref Unsafe.AsRef(in result.M11);
            r0.StoreUnsafe(ref resRef, 0);
            r1.StoreUnsafe(ref resRef, 4);
            r2.StoreUnsafe(ref resRef, 8);
            r3.StoreUnsafe(ref resRef, 12);

            return true;
        }

        if (Vector128.IsHardwareAccelerated && Vector128<T>.Count == 4)
        {
            ref T m = ref Unsafe.AsRef(in matrix.M11);
            var v0 = Vector128.LoadUnsafe(ref m, 0);
            var v1 = Vector128.LoadUnsafe(ref m, 4);
            var v2 = Vector128.LoadUnsafe(ref m, 8);
            var v3 = Vector128.LoadUnsafe(ref m, 12);

            T m11_128 = v0.GetElement(0); T m12_128 = v0.GetElement(1); T m13_128 = v0.GetElement(2); T m14_128 = v0.GetElement(3);
            T m21_128 = v1.GetElement(0); T m22_128 = v1.GetElement(1); T m23_128 = v1.GetElement(2); T m24_128 = v1.GetElement(3);
            T m31_128 = v2.GetElement(0); T m32_128 = v2.GetElement(1); T m33_128 = v2.GetElement(2); T m34_128 = v2.GetElement(3);
            T m41_128 = v3.GetElement(0); T m42_128 = v3.GetElement(1); T m43_128 = v3.GetElement(2); T m44_128 = v3.GetElement(3);

            T b00_128 = m11_128 * m22_128 - m12_128 * m21_128;
            T b01_128 = m11_128 * m23_128 - m13_128 * m21_128;
            T b02_128 = m11_128 * m24_128 - m14_128 * m21_128;
            T b03_128 = m12_128 * m23_128 - m13_128 * m22_128;
            T b04_128 = m12_128 * m24_128 - m14_128 * m22_128;
            T b05_128 = m13_128 * m24_128 - m14_128 * m23_128;
            T b06_128 = m31_128 * m42_128 - m32_128 * m41_128;
            T b07_128 = m31_128 * m43_128 - m33_128 * m41_128;
            T b08_128 = m31_128 * m44_128 - m34_128 * m41_128;
            T b09_128 = m32_128 * m43_128 - m33_128 * m42_128;
            T b10_128 = m32_128 * m44_128 - m34_128 * m42_128;
            T b11_128 = m33_128 * m44_128 - m34_128 * m43_128;

            T det_128 = b00_128 * b11_128 - b01_128 * b10_128 + b02_128 * b09_128 + b03_128 * b08_128 - b04_128 * b07_128 + b05_128 * b06_128;
            if (det_128 == T.Zero)
            {
                result = default;
                return false;
            }

            T invDet_128 = T.One / det_128;
            var vInvDet_128 = Vector128.Create(invDet_128);

            T* t0_128 = stackalloc T[4];
            T* t1_128 = stackalloc T[4];
            T* t2_128 = stackalloc T[4];
            T* t3_128 = stackalloc T[4];
            T* t4_128 = stackalloc T[4];
            T* t5_128 = stackalloc T[4];

            t0_128[0] = m22_128; t0_128[1] = m13_128; t0_128[2] = m42_128; t0_128[3] = m33_128;
            t1_128[0] = b11_128; t1_128[1] = b10_128; t1_128[2] = b05_128; t1_128[3] = b04_128;
            t2_128[0] = m23_128; t2_128[1] = m12_128; t2_128[2] = m43_128; t2_128[3] = m32_128;
            t3_128[0] = b10_128; t3_128[1] = b11_128; t3_128[2] = b04_128; t3_128[3] = b05_128;
            t4_128[0] = m24_128; t4_128[1] = -m14_128; t4_128[2] = m44_128; t4_128[3] = -m34_128;
            t5_128[0] = b09_128; t5_128[1] = b09_128; t5_128[2] = b03_128; t5_128[3] = b03_128;

            var r0_128 = (Vector128.LoadUnsafe(ref *t0_128) * Vector128.LoadUnsafe(ref *t1_128)
                    - Vector128.LoadUnsafe(ref *t2_128) * Vector128.LoadUnsafe(ref *t3_128)
                    + Vector128.LoadUnsafe(ref *t4_128) * Vector128.LoadUnsafe(ref *t5_128)) * vInvDet_128;

            t0_128[0] = m23_128; t0_128[1] = m11_128; t0_128[2] = m43_128; t0_128[3] = m31_128;
            t1_128[0] = b08_128; t1_128[1] = b11_128; t1_128[2] = b02_128; t1_128[3] = b05_128;
            t2_128[0] = m21_128; t2_128[1] = m13_128; t2_128[2] = m41_128; t2_128[3] = m33_128;
            t3_128[0] = b11_128; t3_128[1] = b08_128; t3_128[2] = b05_128; t3_128[3] = b02_128;
            t4_128[0] = -m24_128; t4_128[1] = m14_128; t4_128[2] = -m44_128; t4_128[3] = m34_128;
            t5_128[0] = b07_128; t5_128[1] = b07_128; t5_128[2] = b01_128; t5_128[3] = b01_128;

            var r1_128 = (Vector128.LoadUnsafe(ref *t0_128) * Vector128.LoadUnsafe(ref *t1_128)
                    - Vector128.LoadUnsafe(ref *t2_128) * Vector128.LoadUnsafe(ref *t3_128)
                    + Vector128.LoadUnsafe(ref *t4_128) * Vector128.LoadUnsafe(ref *t5_128)) * vInvDet_128;

            t0_128[0] = m21_128; t0_128[1] = m12_128; t0_128[2] = m41_128; t0_128[3] = m32_128;
            t1_128[0] = b10_128; t1_128[1] = b08_128; t1_128[2] = b04_128; t1_128[3] = b02_128;
            t2_128[0] = m22_128; t2_128[1] = m11_128; t2_128[2] = m42_128; t2_128[3] = m31_128;
            t3_128[0] = b08_128; t3_128[1] = b10_128; t3_128[2] = b02_128; t3_128[3] = b04_128;
            t4_128[0] = m24_128; t4_128[1] = -m14_128; t4_128[2] = m44_128; t4_128[3] = -m34_128;
            t5_128[0] = b06_128; t5_128[1] = b06_128; t5_128[2] = b00_128; t5_128[3] = b00_128;

            var r2_128 = (Vector128.LoadUnsafe(ref *t0_128) * Vector128.LoadUnsafe(ref *t1_128)
                    - Vector128.LoadUnsafe(ref *t2_128) * Vector128.LoadUnsafe(ref *t3_128)
                    + Vector128.LoadUnsafe(ref *t4_128) * Vector128.LoadUnsafe(ref *t5_128)) * vInvDet_128;

            t0_128[0] = m22_128; t0_128[1] = m11_128; t0_128[2] = m42_128; t0_128[3] = m31_128;
            t1_128[0] = b07_128; t1_128[1] = b09_128; t1_128[2] = b01_128; t1_128[3] = b03_128;
            t2_128[0] = m21_128; t2_128[1] = m12_128; t2_128[2] = m41_128; t2_128[3] = m32_128;
            t3_128[0] = b09_128; t3_128[1] = b07_128; t3_128[2] = b03_128; t3_128[3] = b01_128;
            t4_128[0] = -m23_128; t4_128[1] = m13_128; t4_128[2] = -m43_128; t4_128[3] = m33_128;
            t5_128[0] = b06_128; t5_128[1] = b06_128; t5_128[2] = b00_128; t5_128[3] = b00_128;

            var r3_128 = (Vector128.LoadUnsafe(ref *t0_128) * Vector128.LoadUnsafe(ref *t1_128)
                    - Vector128.LoadUnsafe(ref *t2_128) * Vector128.LoadUnsafe(ref *t3_128)
                    + Vector128.LoadUnsafe(ref *t4_128) * Vector128.LoadUnsafe(ref *t5_128)) * vInvDet_128;

            result = default;
            ref T resRef = ref Unsafe.AsRef(in result.M11);
            r0_128.StoreUnsafe(ref resRef, 0);
            r1_128.StoreUnsafe(ref resRef, 4);
            r2_128.StoreUnsafe(ref resRef, 8);
            r3_128.StoreUnsafe(ref resRef, 12);

            return true;
        }

        T m11 = matrix.M11, m12 = matrix.M12, m13 = matrix.M13, m14 = matrix.M14;
        T m21 = matrix.M21, m22 = matrix.M22, m23 = matrix.M23, m24 = matrix.M24;
        T m31 = matrix.M31, m32 = matrix.M32, m33 = matrix.M33, m34 = matrix.M34;
        T m41 = matrix.M41, m42 = matrix.M42, m43 = matrix.M43, m44 = matrix.M44;

        T s_b00 = m11 * m22 - m12 * m21;
        T s_b01 = m11 * m23 - m13 * m21;
        T s_b02 = m11 * m24 - m14 * m21;
        T s_b03 = m12 * m23 - m13 * m22;
        T s_b04 = m12 * m24 - m14 * m22;
        T s_b05 = m13 * m24 - m14 * m23;
        T s_b06 = m31 * m42 - m32 * m41;
        T s_b07 = m31 * m43 - m33 * m41;
        T s_b08 = m31 * m44 - m34 * m41;
        T s_b09 = m32 * m43 - m33 * m42;
        T s_b10 = m32 * m44 - m34 * m42;
        T s_b11 = m33 * m44 - m34 * m43;

        T s_det = s_b00 * s_b11 - s_b01 * s_b10 + s_b02 * s_b09 + s_b03 * s_b08 - s_b04 * s_b07 + s_b05 * s_b06;
        if (s_det == T.Zero)
        {
            result = default;
            return false;
        }

        T s_invDet = T.One / s_det;

        result = default;
        result.M11 = (m22 * s_b11 - m23 * s_b10 + m24 * s_b09) * s_invDet;
        result.M12 = (-m12 * s_b11 + m13 * s_b10 - m14 * s_b09) * s_invDet;
        result.M13 = (m42 * s_b05 - m43 * s_b04 + m44 * s_b03) * s_invDet;
        result.M14 = (-m32 * s_b05 + m33 * s_b04 - m34 * s_b03) * s_invDet;

        result.M21 = (-m21 * s_b11 + m23 * s_b08 - m24 * s_b07) * s_invDet;
        result.M22 = (m11 * s_b11 - m13 * s_b08 + m14 * s_b07) * s_invDet;
        result.M23 = (-m41 * s_b05 + m43 * s_b02 - m44 * s_b01) * s_invDet;
        result.M24 = (m31 * s_b05 - m33 * s_b02 + m34 * s_b01) * s_invDet;

        result.M31 = (m21 * s_b10 - m22 * s_b08 + m24 * s_b06) * s_invDet;
        result.M32 = (-m11 * s_b10 + m12 * s_b08 - m14 * s_b06) * s_invDet;
        result.M33 = (m41 * s_b04 - m42 * s_b02 + m44 * s_b00) * s_invDet;
        result.M34 = (-m31 * s_b04 + m32 * s_b02 - m34 * s_b00) * s_invDet;

        result.M41 = (-m21 * s_b09 + m22 * s_b07 - m23 * s_b06) * s_invDet;
        result.M42 = (m11 * s_b09 - m12 * s_b07 + m13 * s_b06) * s_invDet;
        result.M43 = (-m41 * s_b03 + m42 * s_b01 - m43 * s_b00) * s_invDet;
        result.M44 = (m31 * s_b03 - m32 * s_b01 + m33 * s_b00) * s_invDet;

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(Matrix4x4<T> other)
    {
        if (Vector256.IsHardwareAccelerated && Vector256<T>.Count == 8)
        {
            var eq1 = Vector256.Equals(Vector256.LoadUnsafe(ref Unsafe.AsRef(in M11)), Vector256.LoadUnsafe(ref other.M11));
            var eq2 = Vector256.Equals(Vector256.LoadUnsafe(ref Unsafe.AsRef(in M31)), Vector256.LoadUnsafe(ref other.M31));
            return Vector256.ExtractMostSignificantBits(eq1) == 255 && Vector256.ExtractMostSignificantBits(eq2) == 255;
        }

        return M11 == other.M11 && M12 == other.M12 && M13 == other.M13 && M14 == other.M14 &&
               M21 == other.M21 && M22 == other.M22 && M23 == other.M23 && M24 == other.M24 &&
               M31 == other.M31 && M32 == other.M32 && M33 == other.M33 && M34 == other.M34 &&
               M41 == other.M41 && M42 == other.M42 && M43 == other.M43 && M44 == other.M44;
    }

    public override readonly bool Equals(object? obj) => obj is Matrix4x4<T> other && Equals(other);
    public override readonly int GetHashCode() => HashCode.Combine(M11, M22, M33, M44);
    public static bool operator ==(Matrix4x4<T> left, Matrix4x4<T> right) => left.Equals(right);
    public static bool operator !=(Matrix4x4<T> left, Matrix4x4<T> right) => !(left == right);
}