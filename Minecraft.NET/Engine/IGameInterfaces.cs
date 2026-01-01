using Minecraft.NET.Graphics.Models;
using Minecraft.NET.Graphics.Rendering;
using Silk.NET.Input;

namespace Minecraft.NET.Engine;

public interface IInputManager : IDisposable
{
    void Initialize(IInputContext inputContext);
    void OnUpdate(double deltaTime);
    bool IsKeyPressed(Key key);
    bool IsMouseButtonPressed(MouseButton button);
}

public interface IRenderPipeline : IDisposable
{
    void Initialize(GL gl);
    void OnRender(double deltaTime);
    void OnFramebufferResize(Vector2D<int> newSize);

    IChunkRenderer ChunkRenderer { get; }
    int VisibleSectionCount { get; }
}

public interface IChunkRenderer : IDisposable
{
    void Initialize(GL gl);

    ChunkMeshGeometry UploadChunkMesh(MeshData meshData);
    void FreeChunkMesh(ChunkMeshGeometry geometry);

    unsafe void UploadIndirectCommands(DrawElementsIndirectCommand* commands, int count);
    void Bind();
    void Draw(int commandCount);

    unsafe void UpdateInstanceData(Vector3* offsets, int count);
}

public interface IPerformanceMonitor : IDisposable
{
    void Initialize(GL gl);
    void BeginCpuFrame();
    void EndCpuFrame();
    void BeginGpuFrame();
    void EndGpuFrame();

    double AvgCpuTimeMs { get; }
    double AvgGpuTimeMs { get; }
}

public interface IGameStatsService
{
    void OnUpdate(double deltaTime);
    void OnRender(double deltaTime);
}