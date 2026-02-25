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

    void DrawChunk(ChunkMeshGeometry geometry);
}