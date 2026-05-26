namespace MinecraftPT.Engine.Abstractions.Graphics;

public interface IMesh : IDisposable
{
    uint IndexCount { get; }
    bool IsReady { get; }
}