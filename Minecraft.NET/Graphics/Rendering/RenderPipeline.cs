using Microsoft.Extensions.DependencyInjection;
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
    IServiceProvider serviceProvider,
    IChunkRenderer chunkRenderer
) : IRenderPipeline
{
    private GL _gl = null!;
    private readonly List<IRenderPass> _renderPasses = [];

    public IChunkRenderer ChunkRenderer => chunkRenderer;
    public int VisibleSectionCount => sceneCuller.Result.VisibleSectionCount;

    public unsafe void Initialize(GL gl)
    {
        _gl = gl;

        chunkRenderer.Initialize(gl);

        var gBuffer = ActivatorUtilities.CreateInstance<GBufferPass>(serviceProvider, _gl, chunkRenderer);
        var lighting = ActivatorUtilities.CreateInstance<LightingPass>(serviceProvider, _gl, gBuffer);
        var fxaa = ActivatorUtilities.CreateInstance<FxaaPass>(serviceProvider, _gl, lighting);

        _renderPasses.Add(gBuffer);
        _renderPasses.Add(lighting);
        _renderPasses.Add(fxaa);

        _gl.ClearColor(0.53f, 0.81f, 0.92f, 1.0f);
        _gl.Enable(EnableCap.DepthTest);
        _gl.Enable(EnableCap.CullFace);
    }

    public void OnFramebufferResize(Vector2D<int> newSize)
    {
        if (_gl == null) return;
        _gl.Viewport(newSize);
        frameContext.ViewportSize = new Vector2((uint)newSize.X, (uint)newSize.Y);
        foreach (var pass in _renderPasses) pass.Initialize((uint)newSize.X, (uint)newSize.Y);
    }

    public unsafe void OnRender(double deltaTime)
    {
        if (_gl == null) return;
        performanceMonitor.BeginGpuFrame();

        var camera = player.Camera;

        frameContext.ViewportSize = new Vector2((uint)frameContext.ViewportSize.X, (uint)frameContext.ViewportSize.Y);
        frameContext.ProjectionMatrix = camera.GetProjectionMatrix(frameContext.ViewportSize.X / frameContext.ViewportSize.Y);

        var cameraOrigin = new Vector3d(Math.Floor(camera.Position.X), Math.Floor(camera.Position.Y), Math.Floor(camera.Position.Z));
        var cameraRenderPos = (Vector3)(camera.Position - cameraOrigin);

        frameContext.RelativeViewMatrix = Matrix4x4.CreateLookAt(cameraRenderPos, cameraRenderPos + camera.Front, camera.Up);
        frameContext.ViewMatrix = camera.GetViewMatrix();

        sceneCuller.Cull(frameContext.ProjectionMatrix, frameContext.RelativeViewMatrix);
        var visibleScene = sceneCuller.Result;

        if (visibleScene.VisibleSectionCount > 0)
        {
            fixed (Vector3* ptr = visibleScene.ChunkOffsets)
            {
                chunkRenderer.UpdateInstanceData(ptr, visibleScene.VisibleSectionCount);
            }
        }

        foreach (var pass in _renderPasses)
            pass.Execute();

        performanceMonitor.EndGpuFrame();
    }

    public void Dispose()
    {
        foreach (var pass in _renderPasses) pass.Dispose();
        chunkRenderer.Dispose();
    }
}