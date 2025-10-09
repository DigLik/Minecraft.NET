namespace Minecraft.NET.Graphics.Rendering.Passes;

public interface IRenderPass : IDisposable
{
    void Initialize(GL gl, uint width, uint height);
    void Execute(GL gl, SharedRenderData sharedData);
    void OnResize(uint width, uint height);
}