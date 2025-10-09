using Minecraft.NET.Abstractions;
using System.Diagnostics;

namespace Minecraft.NET.Services;

public class PerformanceMonitor(GL gl) : IPerformanceMonitor
{
    private readonly Stopwatch _cpuStopwatch = new();

    private readonly uint[] _queryIds = new uint[2];
    private int _queryFrameIndex = 0;
    private bool _queriesInitialized = false;

    public double CpuTimeMs { get; private set; }
    public double GpuTimeMs { get; private set; }

    public void OnLoad()
    {
        gl.GenQueries(2, _queryIds);
        _queriesInitialized = true;
    }

    public void BeginCpuFrame()
    {
        _cpuStopwatch.Restart();
    }

    public void EndCpuFrame()
    {
        _cpuStopwatch.Stop();
        CpuTimeMs = _cpuStopwatch.Elapsed.TotalMilliseconds;
    }

    public unsafe void BeginGpuFrame()
    {
        if (!_queriesInitialized) return;

        int previousFrame = (_queryFrameIndex + 1) % 2;

        gl.GetQueryObject(_queryIds[previousFrame], GLEnum.QueryResult, out ulong timeElapsed);
        GpuTimeMs = timeElapsed / 1_000_000.0; // ns => ms

        gl.BeginQuery(QueryTarget.TimeElapsed, _queryIds[_queryFrameIndex]);
    }

    public void EndGpuFrame()
    {
        if (!_queriesInitialized) return;

        gl.EndQuery(QueryTarget.TimeElapsed);
        _queryFrameIndex = (_queryFrameIndex + 1) % 2;
    }

    public void OnClose()
    {
        if (_queriesInitialized)
        {
            gl.DeleteQueries(2, _queryIds);
            _queriesInitialized = false;
        }
    }

    public void Dispose() => OnClose();
}