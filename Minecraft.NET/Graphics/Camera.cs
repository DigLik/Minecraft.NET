using Minecraft.NET.Core.Common;

namespace Minecraft.NET.Graphics;

public class Camera
{
    public Vector3d Position { get; set; }
    public Vector3 Front { get; private set; } = -Vector3.UnitZ;
    public Vector3 Up { get; private set; } = Vector3.UnitY;
    public Vector3 Right { get; private set; } = Vector3.UnitX;

    public float Pitch { get; set; }
    public float Yaw { get; set; } = -90.0f;
    public float Fov { get; set; } = 90.0f;

    public Camera(Vector3d position)
    {
        Position = position;
        UpdateVectors();
    }

    public Matrix4x4 GetViewMatrix()
    {
        var posF = (Vector3)Position;
        return Matrix4x4.CreateLookAt(posF, posF + Front, Up);
    }

    public Matrix4x4 GetProjectionMatrix(float aspectRatio)
    {
        float fovRad = float.DegreesToRadians(Fov);
        float f = 1.0f / MathF.Tan(fovRad * 0.5f);

        float zNear = 0.1f;
        Matrix4x4 result = default;

        result.M11 = f / aspectRatio;
        result.M22 = f;
        result.M33 = 0.0f;
        result.M34 = -1.0f;
        result.M43 = zNear;
        result.M44 = 0.0f;

        return result;
    }

    public void UpdateVectors()
    {
        Pitch = Math.Clamp(Pitch, -89.0f, 89.0f);
        Vector3 front;
        front.X = MathF.Cos(float.DegreesToRadians(Yaw)) * MathF.Cos(float.DegreesToRadians(Pitch));
        front.Y = MathF.Sin(float.DegreesToRadians(Pitch));
        front.Z = MathF.Sin(float.DegreesToRadians(Yaw)) * MathF.Cos(float.DegreesToRadians(Pitch));

        Front = Vector3.Normalize(front);
        Right = Vector3.Normalize(Vector3.Cross(Front, Vector3.UnitY));
        Up = Vector3.Normalize(Vector3.Cross(Right, Front));
    }
}