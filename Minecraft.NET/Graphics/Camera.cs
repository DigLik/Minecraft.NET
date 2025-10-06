using Silk.NET.Maths;
using System.Numerics;

namespace Minecraft.NET.Graphics;

public class Camera
{
    public Vector3 Position { get; set; } = new(0.0f, 0.0f, 3.0f);
    public Vector3 Front { get; private set; } = -Vector3.UnitZ;
    public Vector3 Up { get; private set; } = Vector3.UnitY;
    public Vector3 Right { get; private set; } = Vector3.UnitX;

    private float _pitch;
    private float _yaw = -90.0f;
    private float _fov = 90.0f;

    public float Pitch
    {
        get => _pitch;
        set => _pitch = Math.Clamp(value, -89.0f, 89.0f);
    }

    public float Yaw
    {
        get => _yaw;
        set => _yaw = value;
    }

    public float FieldOfView
    {
        get => _fov;
        set => _fov = Math.Clamp(value, 1.0f, 90.0f);
    }

    public Camera()
    {
        UpdateVectors();
    }

    public Matrix4x4 GetViewMatrix()
        => Matrix4x4.CreateLookAt(Position, Position + Front, Up);

    public Matrix4x4 GetProjectionMatrix(float aspectRatio)
        => Matrix4x4.CreatePerspectiveFieldOfView(float.DegreesToRadians(FieldOfView), aspectRatio, 0.1f, 100.0f);

    public void UpdateVectors()
    {
        Vector3 front;
        front.X = MathF.Cos(float.DegreesToRadians(Yaw)) * MathF.Cos(float.DegreesToRadians(Pitch));
        front.Y = MathF.Sin(float.DegreesToRadians(Pitch));
        front.Z = MathF.Sin(float.DegreesToRadians(Yaw)) * MathF.Cos(float.DegreesToRadians(Pitch));

        Front = Vector3.Normalize(front);
        Right = Vector3.Normalize(Vector3.Cross(Front, Vector3.UnitY));
        Up = Vector3.Normalize(Vector3.Cross(Right, Front));
    }
}