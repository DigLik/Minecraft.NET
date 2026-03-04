namespace Minecraft.NET.Engine.Abstractions.Graphics;

public interface IMesh : IDisposable
{
    uint IndexCount { get; }
    bool IsReady { get; }
}