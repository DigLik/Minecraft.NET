using Minecraft.NET.Core.Blocks;
using Minecraft.NET.Engine;
using Minecraft.NET.Services;

namespace Minecraft.NET.Graphics.Rendering.Passes;

public class GBufferPass(
    IGlContextAccessor glAccessor,
    IChunkRenderer chunkRenderer,
    FrameContext frameContext,
    SceneCuller sceneCuller,
    RenderSettings renderSettings,
    RenderResources resources
    ) : IRenderPass
{
    public int Priority => 100;
    public string Name => "G-Buffer";
    private GL Gl => glAccessor.Gl;

    private Shader _gBufferShader = null!;
    private TextureArray _blockTextures = null!;

    private int _gBufferViewLocation;
    private int _gBufferProjectionLocation;
    private int _uUseWireframeColorLoc;
    private int _uWireframeColorLoc;
    private int _uFogStartLoc;
    private int _uFogEndLoc;
    private int _uFogColorLoc;

    public unsafe void Initialize(uint width, uint height)
    {
        if (_gBufferShader == null)
        {
            _blockTextures = new TextureArray(Gl, BlockRegistry.TextureFiles);
            _gBufferShader = new Shader(Gl,
                Shader.LoadFromFile("Assets/Shaders/g_buffer.vert"),
                Shader.LoadFromFile("Assets/Shaders/g_buffer.frag")
            );
            _gBufferShader.Use();

            _gBufferShader.SetInt(_gBufferShader.GetUniformLocation("uTextureArray"), 0);

            _uFogStartLoc = _gBufferShader.GetUniformLocation("u_fogStart");
            _uFogEndLoc = _gBufferShader.GetUniformLocation("u_fogEnd");
            _uFogColorLoc = _gBufferShader.GetUniformLocation("u_fogColor");
            _gBufferViewLocation = _gBufferShader.GetUniformLocation("view");
            _gBufferProjectionLocation = _gBufferShader.GetUniformLocation("projection");
            _uUseWireframeColorLoc = _gBufferShader.GetUniformLocation("u_UseWireframeColor");
            _uWireframeColorLoc = _gBufferShader.GetUniformLocation("u_WireframeColor");
        }
    }

    public void OnResize(uint width, uint height)
    {
        resources.GBuffer?.Dispose();
        resources.GBuffer = new Framebuffer(Gl, width, height);
    }

    public unsafe void Execute(RenderResources renderResources)
    {
        var gBuffer = renderResources.GBuffer;
        if (gBuffer == null)
            return;

        gBuffer.Bind();
        Gl.ClearDepth(0.0f);
        Gl.ClearColor(0.0f, 0.0f, 0.0f, 0.0f);
        Gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        var visibleScene = sceneCuller.Result;
        if (visibleScene.MaxPossibleCount > 0)
        {
            _gBufferShader.Use();
            _gBufferShader.SetMatrix4x4(_gBufferViewLocation, frameContext.RelativeViewMatrix);
            _gBufferShader.SetMatrix4x4(_gBufferProjectionLocation, frameContext.ProjectionMatrix);

            _gBufferShader.SetFloat(_uFogStartLoc, RenderDistance * ChunkSize * 0.5f);
            _gBufferShader.SetFloat(_uFogEndLoc, RenderDistance * ChunkSize * 0.95f);
            _gBufferShader.SetVector3(_uFogColorLoc, new Vector3(0.53f, 0.81f, 0.92f));

            if (renderSettings.IsWireframeEnabled)
            {
                Gl.PolygonMode(GLEnum.FrontAndBack, GLEnum.Line);
                Gl.Enable(EnableCap.PolygonOffsetLine);
                Gl.PolygonOffset(-1.0f, -1.0f);
                _gBufferShader.SetBool(_uUseWireframeColorLoc, true);
                _gBufferShader.SetVector4(_uWireframeColorLoc, new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
            }
            else
            {
                Gl.PolygonMode(GLEnum.FrontAndBack, GLEnum.Fill);
                Gl.Disable(EnableCap.PolygonOffsetLine);
                _gBufferShader.SetBool(_uUseWireframeColorLoc, false);
            }

            _blockTextures.Bind(TextureUnit.Texture0);

            chunkRenderer.Bind();
            chunkRenderer.DrawGPUIndirectCount(
                visibleScene.IndirectBufferHandle,
                visibleScene.InstanceBufferHandle,
                visibleScene.CountBufferHandle,
                visibleScene.MaxPossibleCount
            );

            if (renderSettings.IsWireframeEnabled)
            {
                Gl.PolygonMode(GLEnum.FrontAndBack, GLEnum.Fill);
                Gl.Disable(EnableCap.PolygonOffsetLine);
                _gBufferShader.SetBool(_uUseWireframeColorLoc, false);
            }
        }

        gBuffer.Unbind();
    }

    public void Dispose()
    {
        _gBufferShader?.Dispose();
        _blockTextures?.Dispose();
    }
}