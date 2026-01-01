namespace Minecraft.NET.Graphics.Rendering.Passes;

public interface IRenderPass : IDisposable
{
    void Initialize(uint width, uint height);
    void Execute();
    void OnResize(uint width, uint height);
}