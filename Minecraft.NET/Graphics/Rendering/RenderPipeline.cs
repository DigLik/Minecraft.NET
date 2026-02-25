using Minecraft.NET.Character;
using Minecraft.NET.Core.Blocks;
using Minecraft.NET.Core.Common;
using Minecraft.NET.Engine;
using Minecraft.NET.Services;

using System.Numerics;

namespace Minecraft.NET.Graphics.Rendering;

public class RenderPipeline(
    Player player,
    FrameContext frameContext,
    IChunkRenderer chunkRenderer,
    ChunkManager chunkManager,
    RenderSettings renderSettings,
    GL gl
) : IRenderPipeline
{
    public IChunkRenderer ChunkRenderer => chunkRenderer;

    private Shader _mainShader = null!;
    private TextureArray _blockTextures = null!;

    private int _modelLoc;
    private int _viewLoc;
    private int _projLoc;
    private int _useWireframeLoc;
    private int _wireframeColorLoc;

    public unsafe void Initialize()
    {
        chunkRenderer.Initialize();

        _blockTextures = new TextureArray(gl, BlockRegistry.TextureFiles);
        _mainShader = new Shader(gl,
            Shader.LoadFromFile("Assets/Shaders/main.vert"),
            Shader.LoadFromFile("Assets/Shaders/main.frag")
        );

        _mainShader.Use();
        _mainShader.SetInt(_mainShader.GetUniformLocation("uTextureArray"), 0);

        _modelLoc = _mainShader.GetUniformLocation("model");
        _viewLoc = _mainShader.GetUniformLocation("view");
        _projLoc = _mainShader.GetUniformLocation("projection");
        _useWireframeLoc = _mainShader.GetUniformLocation("u_UseWireframeColor");
        _wireframeColorLoc = _mainShader.GetUniformLocation("u_WireframeColor");

        gl.ClearColor(0.53f, 0.81f, 0.92f, 1.0f);
        gl.ClipControl(ClipControlOrigin.LowerLeft, ClipControlDepth.ZeroToOne);
        gl.Enable(EnableCap.DepthTest);
        gl.DepthFunc(DepthFunction.Greater);
        gl.Enable(EnableCap.CullFace);
    }

    public void OnFramebufferResize(Vector2D<int> newSize)
    {
        if (gl == null)
            return;

        gl.Viewport(newSize);
        frameContext.ViewportSize = new Vector2((uint)newSize.X, (uint)newSize.Y);
    }

    public void OnRender(double deltaTime)
    {
        if (gl == null)
            return;

        UpdateCamera();

        gl.ClearDepth(0.0f);
        gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        _mainShader.Use();
        _mainShader.SetMatrix4x4(_viewLoc, frameContext.RelativeViewMatrix);
        _mainShader.SetMatrix4x4(_projLoc, frameContext.ProjectionMatrix);

        if (renderSettings.IsWireframeEnabled)
        {
            gl.PolygonMode(GLEnum.FrontAndBack, GLEnum.Line);
            gl.Enable(EnableCap.PolygonOffsetLine);
            gl.PolygonOffset(-1.0f, -1.0f);
            _mainShader.SetBool(_useWireframeLoc, true);
            _mainShader.SetVector4(_wireframeColorLoc, new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
        }
        else
        {
            gl.PolygonMode(GLEnum.FrontAndBack, GLEnum.Fill);
            gl.Disable(EnableCap.PolygonOffsetLine);
            _mainShader.SetBool(_useWireframeLoc, false);
        }

        _blockTextures.Bind(TextureUnit.Texture0);

        var camPos = player.Position;
        float camX = (float)Math.Floor(camPos.X);
        float camY = (float)Math.Floor(camPos.Y);
        float camZ = (float)Math.Floor(camPos.Z);

        foreach (var chunk in chunkManager.GetRenderChunks())
        {
            float colX = chunk.Position.X * ChunkSize - camX;
            float colZ = chunk.Position.Y * ChunkSize - camZ;

            for (int y = 0; y < WorldHeightInChunks; y++)
            {
                var geometry = chunk.MeshGeometries[y];
                if (geometry.IndexCount == 0) continue;

                float colY = y * ChunkSize - VerticalChunkOffset * ChunkSize - camY;
                var model = Matrix4x4.CreateTranslation(new Vector3(colX, colY, colZ));

                _mainShader.SetMatrix4x4(_modelLoc, model);
                chunkRenderer.DrawChunk(geometry);
            }
        }

        if (renderSettings.IsWireframeEnabled)
        {
            gl.PolygonMode(GLEnum.FrontAndBack, GLEnum.Fill);
            gl.Disable(EnableCap.PolygonOffsetLine);
            _mainShader.SetBool(_useWireframeLoc, false);
        }
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
        _mainShader?.Dispose();
        _blockTextures?.Dispose();
        chunkRenderer.Dispose();
    }
}