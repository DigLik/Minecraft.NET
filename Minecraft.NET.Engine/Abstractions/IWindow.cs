using Minecraft.NET.Utils.Math;

namespace Minecraft.NET.Engine.Abstractions;

public interface IWindow : IDisposable
{
    Vector2<int> Size { get; }
    Vector2<int> FramebufferSize { get; }
    string Title { get; set; }
    bool IsClosing { get; }

    unsafe void* Handle { get; }
    nint Win32Handle { get; }

    event Action? Load;
    event Action<double>? Update;
    event Action<double>? Render;
    event Action<Vector2<int>>? FramebufferResize;
    event Action? Closing;

    void Run();
    void Close();
}