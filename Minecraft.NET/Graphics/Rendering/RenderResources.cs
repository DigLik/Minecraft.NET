namespace Minecraft.NET.Graphics.Rendering;

public class RenderResources : IDisposable
{
    public Framebuffer? GBuffer { get; set; }
    public Framebuffer? PostProcessFbo { get; set; }

    public Framebuffer? SmaaEdges { get; set; }
    public Framebuffer? SmaaBlend { get; set; }

    public void Dispose()
    {
        GBuffer?.Dispose();
        PostProcessFbo?.Dispose();
        SmaaEdges?.Dispose();
        SmaaBlend?.Dispose();
    }
}