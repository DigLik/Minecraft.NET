using Minecraft.NET.Shared.Structs;
using System.Diagnostics;

namespace Minecraft.NET.Diagnostics;

public sealed class PerformanceMonitor(uint historyLength, float updateInterval = 1.0f) : IDisposable
{
    private readonly LoopedArray<float> _frameTimeMs = new(historyLength);
    private readonly Stopwatch _stopwatch = new();
    private float _timeAccumulator = 0.0f;
    private int _frameCount = 0;
    private bool _disposed = false;

    public void StartFrame() => _stopwatch.Restart();

    public bool EndFrame(double delta)
    {
        _stopwatch.Stop();

        _frameTimeMs.Add((float)_stopwatch.Elapsed.TotalMilliseconds);

        _timeAccumulator += (float)delta;
        _frameCount++;

        if (_timeAccumulator >= updateInterval)
            return true;

        return false;
    }

    public string GetTitleAndReset()
    {
        if (_timeAccumulator == 0) return "";

        float fps = _frameCount / _timeAccumulator;
        float avgCpuTimeMs = _frameTimeMs.Average;

        _timeAccumulator = 0.0f;
        _frameCount = 0;

        return $"FPS: {fps:F2} | CPU Time (Avg: {_frameTimeMs.Capacity} frames): {avgCpuTimeMs:F3} ms";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _frameTimeMs.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}