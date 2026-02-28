using Minecraft.NET.Utils.Math;

namespace Minecraft.NET.Engine.Abstractions;

public interface IRenderPipeline : IDisposable
{
    void OnRender(double deltaTime);
    void OnFramebufferResize(Vector2<int> newSize);
}