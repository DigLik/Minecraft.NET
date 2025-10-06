namespace Minecraft.NET.Graphics.Scene;

public sealed class Transform
{
    public Vector3 Position { get; set; } = Vector3.Zero;
    public Quaternion Rotation { get; set; } = Quaternion.Identity;
    public Vector3 Scale { get; set; } = Vector3.One;

    public Matrix4x4 GetModelMatrix()
    {
        return Matrix4x4.CreateScale(Scale) *
               Matrix4x4.CreateFromQuaternion(Rotation) *
               Matrix4x4.CreateTranslation(Position);
    }
}