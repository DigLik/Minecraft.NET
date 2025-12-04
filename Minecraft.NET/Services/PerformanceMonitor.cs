using System.Diagnostics;

namespace Minecraft.NET.Services;

public class PerformanceMonitor(GL gl) : IDisposable
{
    private readonly Stopwatch _cpuStopwatch = new();

    private const int NumFramesInFlight = 3;
    private readonly uint[] _queryIds = new uint[NumFramesInFlight];
    private int _queryFrameIndex = 0;
    private bool _queriesInitialized = false;

    public double AvgCpuTimeMs { get; private set; }
    public double AvgGpuTimeMs { get; private set; }

    private const double SmoothingFactor = 0.95;

    public void OnLoad()
    {
        gl.GenQueries(NumFramesInFlight, _queryIds);
        for (int i = 0; i < NumFramesInFlight; i++)
        {
            gl.BeginQuery(QueryTarget.TimeElapsed, _queryIds[i]);
            gl.EndQuery(QueryTarget.TimeElapsed);
        }
        _queriesInitialized = true;
    }

    public void BeginCpuFrame()
    {
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
        gl.GetQueryObject(_queryIds[queryToRead], GLEnum.QueryResultAvailable, out int available);

        if (available == 1)
        {
            gl.GetQueryObject(_queryIds[queryToRead], GLEnum.QueryResult, out ulong timeElapsedNs);
            double currentGpuMs = timeElapsedNs / 1_000_000.0;

            if (currentGpuMs > 0 && currentGpuMs < 1000)
                AvgGpuTimeMs = (AvgGpuTimeMs * SmoothingFactor) + (currentGpuMs * (1.0 - SmoothingFactor));
        }

        int queryToWrite = (_queryFrameIndex + 1) % NumFramesInFlight;
        gl.BeginQuery(QueryTarget.TimeElapsed, _queryIds[queryToWrite]);
    }

    public void EndGpuFrame()
    {
        if (!_queriesInitialized) return;

        int queryToWrite = (_queryFrameIndex + 1) % NumFramesInFlight;
        gl.EndQuery(QueryTarget.TimeElapsed);

        _queryFrameIndex = queryToWrite;
    }

    public void OnClose()
    {
        if (_queriesInitialized)
        {
            gl.DeleteQueries(NumFramesInFlight, _queryIds);
            _queriesInitialized = false;
        }
    }

    public void Dispose() => OnClose();
}