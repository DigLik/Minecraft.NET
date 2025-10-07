namespace Minecraft.NET.Graphics;

public class Camera
{
    public Vector3 Position { get; set; }
    public Vector3 Front { get; private set; } = -Vector3.UnitZ;
    public Vector3 Up { get; private set; } = Vector3.UnitY;
    public Vector3 Right { get; private set; } = Vector3.UnitX;

    public float Pitch { get; set; }
    public float Yaw { get; set; } = -90.0f;
    public float Fov { get; set; } = 90.0f;

    public Camera(Vector3 position)
    {
        Position = position;
        UpdateVectors();
    }

    public Matrix4x4 GetViewMatrix() => Matrix4x4.CreateLookAt(Position, Position + Front, Up);

    public Matrix4x4 GetProjectionMatrix(float aspectRatio)
    {
        float farPlane = (RenderDistance + 2) * ChunkSize;
        return Matrix4x4.CreatePerspectiveFieldOfView(float.DegreesToRadians(Fov), aspectRatio, 0.1f, farPlane);
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