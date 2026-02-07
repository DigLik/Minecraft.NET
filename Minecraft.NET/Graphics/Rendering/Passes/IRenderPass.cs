namespace Minecraft.NET.Graphics.Rendering.Passes;

public interface IRenderPass : IDisposable
{
    int Priority { get; }
    string Name { get; }

    void Initialize(uint width, uint height);
    void Execute(RenderResources resources);
    void OnResize(uint width, uint height);
}