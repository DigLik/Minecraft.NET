namespace Minecraft.NET.Graphics.Rendering;

public class RenderResources : IDisposable
{
    public Framebuffer? GBuffer { get; set; }

    public void Dispose() => GBuffer?.Dispose();
}