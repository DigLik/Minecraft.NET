namespace Minecraft.NET.Abstractions;

public interface IRenderPipeline : ILifecycleHandler, IUpdatable, IRenderable, IWindowResizeHandler
{
    int VisibleSectionCount { get; }
}