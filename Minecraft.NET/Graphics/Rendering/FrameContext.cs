namespace Minecraft.NET.Graphics.Rendering;

public class FrameContext
{
    public Matrix4x4 ViewMatrix { get; set; }
    public Matrix4x4 RelativeViewMatrix { get; set; }
    public Matrix4x4 ProjectionMatrix { get; set; }
    public Vector2 ViewportSize { get; set; }
}