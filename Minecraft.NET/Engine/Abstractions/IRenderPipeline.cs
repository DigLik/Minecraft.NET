using Silk.NET.Maths;

namespace Minecraft.NET.Engine.Abstractions;

public interface IRenderPipeline : IDisposable
{
    void OnRender(double deltaTime);
    void OnFramebufferResize(Vector2D<int> newSize);
}