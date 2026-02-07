using Minecraft.NET.Character;
using Minecraft.NET.Engine;
using Minecraft.NET.UI.Elements;

namespace Minecraft.NET.Services;

public class GameStatsService(
    Player player,
    ChunkManager chunkManager,
    IPerformanceMonitor performanceMonitor
) : IGameStatsService
{
    public Label? FpsLabel { get; set; }
    public Label? ChunkLabel { get; set; }
    public Label? PosLabel { get; set; }

    private double _statsUpdateTimer;
    private const double StatsUpdateInterval = 0.25;

    private int _frameCount;

    public void OnUpdate(double deltaTime)
    {
        _statsUpdateTimer += deltaTime;
        if (_statsUpdateTimer >= StatsUpdateInterval)
        {
            double fps = _frameCount / _statsUpdateTimer;
            double frameTimeMs = performanceMonitor.AvgTotalTimeMs;
            double otherTime = Math.Max(0, frameTimeMs - performanceMonitor.AvgCpuTimeMs);

            FpsLabel?.SetText(
                $"FPS: {fps:F0} ({frameTimeMs:F2}ms) | CPU: {performanceMonitor.AvgCpuTimeMs:F2}ms | GPU: {performanceMonitor.AvgGpuTimeMs:F2}ms | Other: {otherTime:F2}ms"
            );

            int loaded = chunkManager.GetLoadedChunkCount();
            int meshed = chunkManager.GetMeshedSectionCount();

            ChunkLabel?.SetText($"Chunks: {loaded} (Meshed: {meshed})");

            _statsUpdateTimer = 0;
            _frameCount = 0;
        }

        if (PosLabel != null)
        {
            var pos = player.Position;
            PosLabel.SetText($"XYZ: {pos.X:F1} / {pos.Y:F1} / {pos.Z:F1}");
        }
    }

    public void OnRender(double _) => _frameCount++;
}