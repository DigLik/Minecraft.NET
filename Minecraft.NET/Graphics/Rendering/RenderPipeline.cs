using Minecraft.NET.Character;
using Minecraft.NET.Core.Common;
using Minecraft.NET.Engine;
using Minecraft.NET.Graphics.Rendering.Passes;
using Minecraft.NET.Services;

namespace Minecraft.NET.Graphics.Rendering;

public class RenderPipeline(
    Player player,
    SceneCuller sceneCuller,
    IPerformanceMonitor performanceMonitor,
    FrameContext frameContext,
    IChunkRenderer chunkRenderer,
    RenderResources resources,
    IEnumerable<IRenderPass> renderPasses,
    IGlContextAccessor glAccessor
) : IRenderPipeline
{
    private GL Gl => glAccessor.Gl;
    private readonly List<IRenderPass> _orderedPasses = [.. renderPasses.OrderBy(p => p.Priority)];

    public IChunkRenderer ChunkRenderer => chunkRenderer;

    public unsafe void Initialize()
    {
        chunkRenderer.Initialize();
        sceneCuller.Initialize();

        foreach (var pass in _orderedPasses)
            pass.Initialize(0, 0);

        Gl.ClearColor(0.53f, 0.81f, 0.92f, 1.0f);
        Gl.ClipControl(ClipControlOrigin.LowerLeft, ClipControlDepth.ZeroToOne);
        Gl.Enable(EnableCap.DepthTest);
        Gl.DepthFunc(DepthFunction.Greater);
        Gl.Enable(EnableCap.CullFace);
    }

    public void OnFramebufferResize(Vector2D<int> newSize)
    {
        if (Gl == null)
            return;

        Gl.Viewport(newSize);
        frameContext.ViewportSize = new Vector2((uint)newSize.X, (uint)newSize.Y);

        foreach (var pass in _orderedPasses)
            pass.OnResize((uint)newSize.X, (uint)newSize.Y);
    }

    public void OnRender(double deltaTime)
    {
        if (Gl == null)
            return;

        performanceMonitor.BeginGpuFrame();

        UpdateCamera();

        sceneCuller.Cull(frameContext.ProjectionMatrix, frameContext.RelativeViewMatrix);

        foreach (var pass in _orderedPasses)
            pass.Execute(resources);

        performanceMonitor.EndGpuFrame();
    }

    private void UpdateCamera()
    {
        var camera = player.Camera;

        float aspect = frameContext.ViewportSize.Y > 0
            ? frameContext.ViewportSize.X / frameContext.ViewportSize.Y
            : 1.0f;

        frameContext.ProjectionMatrix = camera.GetProjectionMatrix(aspect);

        var cameraOrigin = new Vector3d(Math.Floor(camera.Position.X), Math.Floor(camera.Position.Y), Math.Floor(camera.Position.Z));
        var cameraRenderPos = (Vector3)(camera.Position - cameraOrigin);

        frameContext.RelativeViewMatrix = Matrix4x4.CreateLookAt(cameraRenderPos, cameraRenderPos + camera.Front, camera.Up);
        frameContext.ViewMatrix = camera.GetViewMatrix();
    }

    public void Dispose()
    {
        foreach (var pass in _orderedPasses)
            pass.Dispose();

        resources.Dispose();
        chunkRenderer.Dispose();
    }
}