namespace Minecraft.NET.Abstractions;

public interface IPerformanceMonitor : ILifecycleHandler
{
    double CpuTimeMs { get; }
    double GpuTimeMs { get; }

    void BeginCpuFrame();
    void EndCpuFrame();
    void BeginGpuFrame();
    void EndGpuFrame();
}