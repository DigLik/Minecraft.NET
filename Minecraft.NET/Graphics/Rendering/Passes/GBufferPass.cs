using Minecraft.NET.Core.Blocks;
using Minecraft.NET.Engine;
using Minecraft.NET.Services;

namespace Minecraft.NET.Graphics.Rendering.Passes;

public class GBufferPass(
    GL gl,
    IChunkRenderer chunkRenderer,
    FrameContext frameContext,
    SceneCuller sceneCuller,
    RenderSettings renderSettings,
    RenderResources resources) : IRenderPass
{
    public int Priority => 100;
    public string Name => "G-Buffer";

    private Shader _gBufferShader = null!;
    private TextureArray _blockTextures = null!;
    private int _gBufferViewLocation;
    private int _gBufferProjectionLocation;
    private int _uUseWireframeColorLoc;
    private int _uWireframeColorLoc;

    public void Initialize(uint width, uint height)
    {
        if (_gBufferShader == null)
        {
            _blockTextures = new TextureArray(gl, BlockRegistry.TextureFiles);
            _gBufferShader = new Shader(gl,
                Shader.LoadFromFile("Assets/Shaders/g_buffer.vert"),
                Shader.LoadFromFile("Assets/Shaders/g_buffer.frag")
            );
            _gBufferShader.Use();

            _gBufferShader.SetInt(_gBufferShader.GetUniformLocation("uTextureArray"), 0);

            _gBufferViewLocation = _gBufferShader.GetUniformLocation("view");
            _gBufferProjectionLocation = _gBufferShader.GetUniformLocation("projection");
            _uUseWireframeColorLoc = _gBufferShader.GetUniformLocation("u_UseWireframeColor");
            _uWireframeColorLoc = _gBufferShader.GetUniformLocation("u_WireframeColor");
        }
    }

    public void OnResize(uint width, uint height)
    {
        resources.GBuffer?.Dispose();
        resources.GBuffer = new Framebuffer(gl, width, height);
    }

    public void Execute(RenderResources renderResources)
    {
        var gBuffer = renderResources.GBuffer;
        if (gBuffer == null) return;

        gBuffer.Bind();
        gl.ClearDepth(0.0f);
        gl.ClearColor(0.0f, 0.0f, 0.0f, 0.0f);
        gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        var visibleScene = sceneCuller.Result;
        if (visibleScene.MaxPossibleCount > 0)
        {
            _gBufferShader.Use();
            _gBufferShader.SetMatrix4x4(_gBufferViewLocation, frameContext.RelativeViewMatrix);
            _gBufferShader.SetMatrix4x4(_gBufferProjectionLocation, frameContext.ProjectionMatrix);

            if (renderSettings.IsWireframeEnabled)
            {
                gl.PolygonMode(GLEnum.FrontAndBack, GLEnum.Line);
                gl.Enable(EnableCap.PolygonOffsetLine);
                gl.PolygonOffset(-1.0f, -1.0f);
                _gBufferShader.SetBool(_uUseWireframeColorLoc, true);
                _gBufferShader.SetVector4(_uWireframeColorLoc, new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
            }
            else
            {
                gl.PolygonMode(GLEnum.FrontAndBack, GLEnum.Fill);
                gl.Disable(EnableCap.PolygonOffsetLine);
                _gBufferShader.SetBool(_uUseWireframeColorLoc, false);
            }

            _blockTextures.Bind(TextureUnit.Texture0);

            chunkRenderer.Bind();
            chunkRenderer.DrawGpuIndirectCount(
                visibleScene.IndirectBufferHandle,
                visibleScene.InstanceBufferHandle,
                visibleScene.CountBufferHandle,
                visibleScene.MaxPossibleCount
            );
            if (renderSettings.IsWireframeEnabled)
            {
                gl.PolygonMode(GLEnum.FrontAndBack, GLEnum.Fill);
                gl.Disable(EnableCap.PolygonOffsetLine);
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