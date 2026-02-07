using Minecraft.NET.Engine;
using Minecraft.NET.Graphics.Rendering;
using System.Diagnostics;

namespace Minecraft.NET.Services;

public class PerformanceMonitor(IGlContextAccessor glAccessor) : IPerformanceMonitor
{
    private GL Gl => glAccessor.Gl;
    private readonly Stopwatch _cpuStopwatch = new();
    private readonly Stopwatch _totalStopwatch = new();

    private const int NumFramesInFlight = 3;
    private readonly uint[] _queryIds = new uint[NumFramesInFlight];
    private int _queryFrameIndex = 0;
    private bool _queriesInitialized = false;
    private const double SmoothingFactor = 0.95;

    public double AvgCpuTimeMs { get; private set; }
    public double AvgGpuTimeMs { get; private set; }
    public double AvgTotalTimeMs { get; private set; }

    public void Initialize()
    {
        Gl.GenQueries(NumFramesInFlight, _queryIds);
        for (int i = 0; i < NumFramesInFlight; i++)
        {
            Gl.BeginQuery(QueryTarget.TimeElapsed, _queryIds[i]);
            Gl.EndQuery(QueryTarget.TimeElapsed);
        }
        _queriesInitialized = true;
        _totalStopwatch.Start();
    }

    public void BeginCpuFrame()
    {
        double currentTotal = _totalStopwatch.Elapsed.TotalMilliseconds;
        _totalStopwatch.Restart();
        AvgTotalTimeMs = (AvgTotalTimeMs * SmoothingFactor) + (currentTotal * (1.0 - SmoothingFactor));
        _cpuStopwatch.Restart();
    }

    public void EndCpuFrame()
    {
        _cpuStopwatch.Stop();
        double currentCpuTime = _cpuStopwatch.Elapsed.TotalMilliseconds;
        AvgCpuTimeMs = (AvgCpuTimeMs * SmoothingFactor) + (currentCpuTime * (1.0 - SmoothingFactor));
    }

    public unsafe void BeginGpuFrame()
    {
        if (!_queriesInitialized) return;
        int queryToRead = _queryFrameIndex;
        Gl.GetQueryObject(_queryIds[queryToRead], GLEnum.QueryResultAvailable, out int available);

        if (available == 1)
        {
            Gl.GetQueryObject(_queryIds[queryToRead], GLEnum.QueryResult, out ulong timeElapsedNs);
            double currentGpuMs = timeElapsedNs / 1_000_000.0;

            if (currentGpuMs > 0 && currentGpuMs < 1000)
                AvgGpuTimeMs = (AvgGpuTimeMs * SmoothingFactor) + (currentGpuMs * (1.0 - SmoothingFactor));
        }

        int queryToWrite = (_queryFrameIndex + 1) % NumFramesInFlight;
        Gl.BeginQuery(QueryTarget.TimeElapsed, _queryIds[queryToWrite]);
    }

    public void EndGpuFrame()
    {
        if (!_queriesInitialized) return;
        int queryToWrite = (_queryFrameIndex + 1) % NumFramesInFlight;
        Gl.EndQuery(QueryTarget.TimeElapsed);
        _queryFrameIndex = queryToWrite;
    }

    public void Dispose()
    {
        if (_queriesInitialized && Gl != null)
        {
            Gl.DeleteQueries(NumFramesInFlight, _queryIds);
            _queriesInitialized = false;
        }
    }
}