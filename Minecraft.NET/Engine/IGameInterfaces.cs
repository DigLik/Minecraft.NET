using Minecraft.NET.Graphics.Models;
using Minecraft.NET.Graphics.Rendering;

namespace Minecraft.NET.Engine;

public interface IInputManager
{
    Vector2 MousePosition { get; }
    bool IsMouseCaptured { get; }

    void OnUpdate(double deltaTime);

    bool IsKeyPressed(Keys key);
    bool IsMouseButtonPressed(MouseButton button);

    void ToggleMouseCapture();
    void CloseWindow();
}

public interface IRenderPipeline : IDisposable
{
    void Initialize();
    void OnRender(double deltaTime);
    void OnFramebufferResize(Vector2D<int> newSize);

    IChunkRenderer ChunkRenderer { get; }
}

public interface IChunkRenderer : IDisposable
{
    void Initialize();

    ChunkMeshGeometry UploadChunkMesh(MeshData meshData);
    void FreeChunkMesh(ChunkMeshGeometry geometry);

    void Bind();

    void DrawGpuIndirectCount(uint indirectBuffer, uint instanceBuffer, uint countBuffer, int maxDrawCount);
}

public interface IPerformanceMonitor : IDisposable
{
    void Initialize();
    void BeginCpuFrame();
    void EndCpuFrame();
    void BeginGpuFrame();
    void EndGpuFrame();
    double AvgCpuTimeMs { get; }
    double AvgGpuTimeMs { get; }
    double AvgTotalTimeMs { get; }
}

public interface IGameStatsService
{
    void OnUpdate(double deltaTime);
    void OnRender(double deltaTime);
}