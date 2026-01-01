using Minecraft.NET.Character;
using Minecraft.NET.Engine;
using Silk.NET.Windowing;
using System.Text;

namespace Minecraft.NET.Services;

public class GameStatsService(
    IWindow window,
    Player player,
    ChunkManager chunkManager,
    IRenderPipeline renderPipeline,
    IPerformanceMonitor performanceMonitor
) : IGameStatsService
{
    private readonly IWindow _window = window;
    private readonly StringBuilder _sb = new();

    private double _statsUpdateTimer;
    private const double StatsUpdateInterval = 0.25;

    private double _titleUpdateTimer;
    private const double TitleUpdateInterval = 0.05;

    private int _frameCount;

    private string _cachedFpsStats = "FPS: 0 (0.00ms) | CPU: 0.00ms | GPU: 0.00ms";
    private string _cachedWorldStats = "S: 0/0 | C: 0";

    public void OnUpdate(double deltaTime)
    {
        _statsUpdateTimer += deltaTime;
        _titleUpdateTimer += deltaTime;

        if (_statsUpdateTimer >= StatsUpdateInterval)
        {
            double fps = _frameCount / _statsUpdateTimer;
            double frameTimeMs = fps > 0 ? 1000.0 / fps : 0.0;

            double cpuMs = performanceMonitor.AvgCpuTimeMs;
            double gpuMs = performanceMonitor.AvgGpuTimeMs;

            _cachedFpsStats = $"FPS: {fps:F0} ({frameTimeMs:F1}ms) | CPU: {cpuMs:F2}ms | GPU: {gpuMs:F2}ms";

            int loaded = chunkManager.GetLoadedChunkCount();
            int meshed = chunkManager.GetMeshedSectionCount();

            _cachedWorldStats = $"S: {renderPipeline.VisibleSectionCount}/{meshed} | C: {loaded}";

            _statsUpdateTimer = 0;
            _frameCount = 0;
        }

        if (_titleUpdateTimer >= TitleUpdateInterval)
        {
            var pos = player.Position;
            var mode = player.CurrentGameMode == GameMode.Creative ? "C" : "S";

            _sb.Clear();
            _sb.Append("MC.NET [").Append(mode).Append("] | ");
            _sb.Append(_cachedFpsStats).Append(" | ");
            _sb.Append(_cachedWorldStats).Append(" | ");
            _sb.Append($"XYZ: {pos.X:F1} / {pos.Y:F1} / {pos.Z:F1}");

            _window.Title = _sb.ToString();
            _titleUpdateTimer = 0;
        }
    }

    public void OnRender(double _) => _frameCount++;
}