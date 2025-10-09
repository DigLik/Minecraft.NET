using Minecraft.NET.Abstractions;
using Silk.NET.Windowing;
using System.Text;

namespace Minecraft.NET.Services;

public class GameStatsService(
    IWindow window,
    IPlayer player,
    IChunkManager chunkManager,
    IRenderPipeline renderPipeline,
    IPerformanceMonitor performanceMonitor
) : IUpdatable, IRenderable
{
    private readonly IWindow _window = window;
    private int _frameCount;
    private double _titleUpdateTimer;
    private const double UpdateInterval = 1.0;

    private readonly StringBuilder _sb = new();

    public void OnUpdate(double deltaTime)
    {
        _titleUpdateTimer += deltaTime;
        if (_titleUpdateTimer >= UpdateInterval)
        {
            double fps = _frameCount / _titleUpdateTimer;
            var pos = player.Position;

            _sb.Clear();
            _sb.Append($"Minecraft.NET [{player.CurrentGameMode}] | FPS: {fps:F0} | ");
            _sb.Append($"CPU: {performanceMonitor.CpuTimeMs:F2}ms | GPU: {performanceMonitor.GpuTimeMs:F2}ms | ");
            _sb.Append($"Sections (Visible/Meshed): {renderPipeline.VisibleSectionCount}/{chunkManager.GetMeshedSectionCount()} | ");
            _sb.Append($"Chunks: {chunkManager.GetLoadedChunkCount()} | X: {pos.X:F1} Y: {pos.Y:F1} Z: {pos.Z:F1}");

            _window.Title = _sb.ToString();

            _titleUpdateTimer = 0;
            _frameCount = 0;
        }
    }

    public void OnRender(double deltaTime) => _frameCount++;
}