using Minecraft.NET.Core;

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

    public Vector3d Velocity { get; set; } = Vector3d.Zero;
    public bool IsOnGround { get; set; }

    public Camera(Vector3d position)
    {
        Position = position;
        UpdateVectors();
    }

    public BoundingBox GetBoundingBox()
    {
        var halfWidth = PlayerWidth / 2;
        var playerPos = Position - new Vector3d(0, PlayerEyeHeight, 0);
        var min = new Vector3((float)(playerPos.X - halfWidth), (float)playerPos.Y, (float)(playerPos.Z - halfWidth));
        var max = new Vector3((float)(playerPos.X + halfWidth), (float)(playerPos.Y + PlayerHeight), (float)(playerPos.Z + halfWidth));
        return new BoundingBox(min, max);
    }


    public Matrix4x4 GetViewMatrix()
    {
        var posF = (Vector3)Position;
        return Matrix4x4.CreateLookAt(posF, posF + Front, Up);
    }

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