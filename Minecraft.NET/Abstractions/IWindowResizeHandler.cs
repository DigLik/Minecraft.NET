namespace Minecraft.NET.Abstractions;

public interface IWindowResizeHandler
{
    void OnFramebufferResize(Vector2D<int> newSize);
}