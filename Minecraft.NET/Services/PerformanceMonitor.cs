using System.Diagnostics;

namespace Minecraft.NET.Services;

public class PerformanceMonitor(GL gl) : IDisposable
{
    private readonly Stopwatch _cpuStopwatch = new();

    private const int NumFramesInFlight = 3;
    private readonly uint[] _queryIds = new uint[NumFramesInFlight];

    private int _queryFrameIndex = 0;
    private bool _queriesInitialized = false;
    public double CpuTimeMs { get; private set; }
    public double GpuTimeMs { get; private set; }

    public void OnLoad()
    {
        gl.GenQueries(NumFramesInFlight, _queryIds);
        _queriesInitialized = true;
    }

    public void BeginCpuFrame() => _cpuStopwatch.Restart();

    public void EndCpuFrame()
    {
        _cpuStopwatch.Stop();
        CpuTimeMs = _cpuStopwatch.Elapsed.TotalMilliseconds;
    }

    public unsafe void BeginGpuFrame()
    {
        if (!_queriesInitialized) return;

        int queryToRead = _queryFrameIndex;

        gl.GetQueryObject(
            _queryIds[queryToRead],
            GLEnum.QueryResultAvailable,
            out int available
        );
        
        if (available == (int)GLEnum.True)
        {
            gl.GetQueryObject(_queryIds[queryToRead], GLEnum.QueryResult, out ulong timeElapsed);
            GpuTimeMs = timeElapsed / 1_000_000.0; // ns => ms
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