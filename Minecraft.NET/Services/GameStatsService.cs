using Minecraft.NET.Character;
using Minecraft.NET.Graphics.Rendering;
using Silk.NET.Windowing;
using System.Text;

namespace Minecraft.NET.Services;

public class GameStatsService(
    IWindow window,
    Player player,
    ChunkManager chunkManager,
    RenderPipeline renderPipeline,
    PerformanceMonitor performanceMonitor
)
{
    private readonly IWindow _window = window;
    private readonly StringBuilder _sb = new();

    private double _statsUpdateTimer;
    private const double StatsUpdateInterval = 0.5;

    private double _titleUpdateTimer;
    private const double TitleUpdateInterval = 0.05;

    private int _frameCount;

    private string _cachedFpsStats = "FPS: 0 | CPU: 0% | GPU: 0%";
    private string _cachedWorldStats = "Sections: 0/0 | Chunks: 0";

    public void OnUpdate(double deltaTime)
    {
        _statsUpdateTimer += deltaTime;
        _titleUpdateTimer += deltaTime;

        if (_statsUpdateTimer >= StatsUpdateInterval)
        {
            double fps = _frameCount / _statsUpdateTimer;
            double frameTimeMs = fps > 0 ? 1000.0 / fps : 1000.0;

            double cpuPercent = (performanceMonitor.AvgCpuTimeMs / frameTimeMs) * 100.0;
            double gpuPercent = (performanceMonitor.AvgGpuTimeMs / frameTimeMs) * 100.0;

            //cpuPercent = Math.Min(cpuPercent, 100.0);
            //gpuPercent = Math.Min(gpuPercent, 100.0);

            _cachedFpsStats = $"FPS: {fps:F0} | CPU: {cpuPercent:F1}% | GPU: {gpuPercent:F1}%";

            int loaded = chunkManager.GetLoadedChunkCount();
            int meshed = chunkManager.GetMeshedSectionCount();
            _cachedWorldStats = $"Sections: {renderPipeline.VisibleSectionCount}/{meshed} | Chunks: {loaded}";

            _statsUpdateTimer = 0;
            _frameCount = 0;
        }

        if (_titleUpdateTimer >= TitleUpdateInterval)
        {
            var pos = player.Position;
            var mode = player.CurrentGameMode;

            _sb.Clear();
            _sb.Append("Minecraft.NET [").Append(mode).Append("] | ");
            _sb.Append(_cachedFpsStats).Append(" | ");
            _sb.Append(_cachedWorldStats).Append(" | ");

            _sb.Append($"X: {pos.X:F1} Y: {pos.Y:F1} Z: {pos.Z:F1}");

            _window.Title = _sb.ToString();
            _titleUpdateTimer = 0;
        }
    }

    public void OnRender(double _) => _frameCount++;
}