using Minecraft.NET.Character;
using Minecraft.NET.Core.Common;
using Minecraft.NET.Graphics.Rendering.Passes;
using Minecraft.NET.Services;
using System.Runtime.CompilerServices;

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
        gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(MaxVisibleSections * sizeof(Vector3)), null, BufferUsageARB.StreamDraw);

        ChunkRenderer = new ChunkRenderer(gl, InstanceVbo);

        _renderPasses.Add(new GBufferPass(ChunkRenderer));
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
        var lightingPass = _renderPasses.OfType<LightingPass>().First();

        _sharedRenderData.GBuffer = gBufferPass.GBuffer;
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

        _sharedRenderData.IndirectCommands = visibleScene.IndirectCommands;
        _sharedRenderData.ChunkOffsets = visibleScene.ChunkOffsets;
        _sharedRenderData.VisibleCount = visibleScene.VisibleSectionCount;

        if (VisibleSectionCount > 0)
        {
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, InstanceVbo);

            nuint sizeInBytes = (nuint)(visibleScene.VisibleSectionCount * sizeof(Vector3));
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(MaxVisibleSections * sizeof(Vector3)), null, BufferUsageARB.StreamDraw);

            void* ptr = gl.MapBufferRange(BufferTargetARB.ArrayBuffer, 0, sizeInBytes,
                (uint)(MapBufferAccessMask.WriteBit | MapBufferAccessMask.InvalidateBufferBit | MapBufferAccessMask.UnsynchronizedBit));

            if (ptr != null)
            {
                fixed (Vector3* source = visibleScene.ChunkOffsets)
                    Unsafe.CopyBlock(ptr, source, (uint)sizeInBytes);
                gl.UnmapBuffer(BufferTargetARB.ArrayBuffer);
            }
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