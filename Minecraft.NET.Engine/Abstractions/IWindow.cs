using Minecraft.NET.Utils.Math;

namespace Minecraft.NET.Engine.Abstractions;

public interface IWindow : IDisposable
{
    Vector2Int Size { get; }
    Vector2Int FramebufferSize { get; }
    string Title { get; set; }
    bool IsClosing { get; }

    unsafe void* Handle { get; }
    nint Win32Handle { get; }

    event Action? Load;
    event Action<double>? Update;
    event Action<double>? Render;
    event Action<Vector2Int>? FramebufferResize;
    event Action? Closing;

    void Run();
    void Close();
}