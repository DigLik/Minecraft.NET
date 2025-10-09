namespace Minecraft.NET.Abstractions;

public interface ILifecycleHandler : IDisposable
{
    void OnLoad();
    void OnClose();
}