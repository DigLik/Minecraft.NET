using Minecraft.NET.Character;
using Minecraft.NET.Core.Common;
using Minecraft.NET.Graphics.Rendering.Passes;
using Minecraft.NET.Services;
using System.Runtime.InteropServices;

namespace Minecraft.NET.Graphics.Rendering;

public class RenderPipeline(
    GL gl,
    Player player,
    SceneCuller sceneCuller,
    PerformanceMonitor performanceMonitor
)
{
    private readonly List<IRenderPass> _renderPasses = [];
    private readonly SharedRenderData _sharedRenderData = new();

    public ChunkRenderer ChunkRenderer { get; private set; } = null!;
    public uint InstanceVbo { get; private set; }
    public int VisibleSectionCount { get; private set; }

    public unsafe void OnLoad()
    {
        InstanceVbo = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, InstanceVbo);
        gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(MaxVisibleSections * sizeof(Matrix4x4)), null, BufferUsageARB.StreamDraw);

        ChunkRenderer = new ChunkRenderer(gl, InstanceVbo);

        _renderPasses.Add(new GBufferPass(ChunkRenderer));
        _renderPasses.Add(new SsaoPass());
        _renderPasses.Add(new SsaoBlurPass());
        _renderPasses.Add(new LightingPass());
        _renderPasses.Add(new FxaaPass());

        gl.ClearColor(0.53f, 0.81f, 0.92f, 1.0f);
        gl.Enable(EnableCap.DepthTest);
        gl.Enable(EnableCap.CullFace);
    }

    public void OnFramebufferResize(Vector2D<int> newSize)
    {
        gl.Viewport(newSize);
        _sharedRenderData.ViewportSize = new Vector2((uint)newSize.X, (uint)newSize.Y);

        foreach (var pass in _renderPasses)
        {
            pass.Initialize(gl, (uint)newSize.X, (uint)newSize.Y);
        }

        var gBufferPass = _renderPasses.OfType<GBufferPass>().First();
        var ssaoPass = _renderPasses.OfType<SsaoPass>().First();
        var ssaoBlurPass = _renderPasses.OfType<SsaoBlurPass>().First();
        var lightingPass = _renderPasses.OfType<LightingPass>().First();

        _sharedRenderData.GBuffer = gBufferPass.GBuffer;
        _sharedRenderData.SsaoBuffer = ssaoPass.SsaoFbo;
        _sharedRenderData.SsaoBlurBuffer = ssaoBlurPass.SsaoBlurFbo;
        _sharedRenderData.PostProcessBuffer = lightingPass.PostProcessFbo;
    }

    public unsafe void OnRender(double deltaTime)
    {
        performanceMonitor.BeginGpuFrame();

        var camera = player.Camera;
        var projection = camera.GetProjectionMatrix(_sharedRenderData.ViewportSize.X / _sharedRenderData.ViewportSize.Y);

        var cameraOrigin = new Vector3d(Math.Floor(camera.Position.X), Math.Floor(camera.Position.Y), Math.Floor(camera.Position.Z));
        var cameraRenderPos = (Vector3)(camera.Position - cameraOrigin);
        var relativeViewMatrix = Matrix4x4.CreateLookAt(cameraRenderPos, cameraRenderPos + camera.Front, camera.Up);

        sceneCuller.Cull(projection, relativeViewMatrix);
        var visibleScene = sceneCuller.Result;
        VisibleSectionCount = visibleScene.VisibleSectionCount;

        _sharedRenderData.ViewMatrix = camera.GetViewMatrix();
        _sharedRenderData.RelativeViewMatrix = relativeViewMatrix;
        _sharedRenderData.ProjectionMatrix = projection;
        _sharedRenderData.VisibleGeometries = visibleScene.VisibleGeometries;
        _sharedRenderData.ModelMatrices = visibleScene.ModelMatrices;

        if (VisibleSectionCount > 0)
        {
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, InstanceVbo);
            fixed (Matrix4x4* p = CollectionsMarshal.AsSpan(visibleScene.ModelMatrices))
                gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(visibleScene.VisibleSectionCount * sizeof(Matrix4x4)), p);
        }

        foreach (var pass in _renderPasses)
            pass.Execute(gl, _sharedRenderData);

        performanceMonitor.EndGpuFrame();
    }

    public void OnClose()
    {
        foreach (var pass in _renderPasses)
            pass.Dispose();

        ChunkRenderer?.Dispose();
        gl.DeleteBuffer(InstanceVbo);
    }
}