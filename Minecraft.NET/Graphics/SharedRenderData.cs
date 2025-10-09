using Minecraft.NET.Graphics.Models;
using Framebuffer = Minecraft.NET.Graphics.Rendering.Framebuffer;

namespace Minecraft.NET.Graphics;

public class SharedRenderData
{
    public Matrix4x4 ViewMatrix { get; set; }
    public Matrix4x4 RelativeViewMatrix { get; set; }
    public Matrix4x4 ProjectionMatrix { get; set; }
    public Vector2 ViewportSize { get; set; }

    public Framebuffer? GBuffer { get; set; }
    public Framebuffer? SsaoBuffer { get; set; }
    public Framebuffer? SsaoBlurBuffer { get; set; }
    public Framebuffer? PostProcessBuffer { get; set; }

    public IReadOnlyList<Mesh> VisibleMeshes { get; set; } = [];
    public IReadOnlyList<Matrix4x4> ModelMatrices { get; set; } = [];
}