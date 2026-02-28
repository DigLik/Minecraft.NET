using Minecraft.NET.Engine.Abstractions;
using Minecraft.NET.Utils.Math;

namespace Minecraft.NET;

public class DummyRenderPipeline : IRenderPipeline
{
    private bool _isDisposed;

    public void OnRender(double deltaTime)
    {
    }

    public void OnFramebufferResize(Vector2<int> newSize)
    {
        Console.WriteLine($"[Render] Framebuffer resized to: {newSize.X}x{newSize.Y}");
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        Console.WriteLine("[Render] Disposed.");
    }
}