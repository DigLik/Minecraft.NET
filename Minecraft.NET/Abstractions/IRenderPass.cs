using Minecraft.NET.Graphics;

namespace Minecraft.NET.Abstractions;

public interface IRenderPass : IDisposable
{
    void Initialize(GL gl, uint width, uint height);
    void Execute(GL gl, SharedRenderData sharedData);
    void OnResize(uint width, uint height);
}