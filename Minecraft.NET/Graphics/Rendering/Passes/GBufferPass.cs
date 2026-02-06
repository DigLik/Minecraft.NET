using Minecraft.NET.Engine;
using Minecraft.NET.Services;

namespace Minecraft.NET.Graphics.Rendering.Passes;

public class GBufferPass(
    GL gl,
    IChunkRenderer chunkRenderer,
    FrameContext frameContext,
    SceneCuller sceneCuller,
    RenderSettings renderSettings
    ) : IRenderPass
{
    private Shader _gBufferShader = null!;
    private Texture _blockTextureAtlas = null!;
    private int _gBufferViewLocation;
    private int _gBufferProjectionLocation;
    private int _uUseWireframeColorLoc;
    private int _uWireframeColorLoc;
    private int _uFogStartLoc;
    private int _uFogEndLoc;
    private int _uFogColorLoc;

    public Framebuffer GBuffer { get; private set; } = null!;

    public unsafe void Initialize(uint width, uint height)
    {
        if (_gBufferShader == null)
        {
            _blockTextureAtlas = new Texture(gl, "Assets/Textures/atlas.png");
            _gBufferShader = new Shader(gl,
                Shader.LoadFromFile("Assets/Shaders/g_buffer.vert"),
                Shader.LoadFromFile("Assets/Shaders/g_buffer.frag")
            );
            _gBufferShader.Use();

            _gBufferShader.SetInt(_gBufferShader.GetUniformLocation("uTexture"), 0);
            _gBufferShader.SetVector2(_gBufferShader.GetUniformLocation("uTileAtlasSize"), new Vector2(AtlasWidth, AtlasHeight));
            _gBufferShader.SetFloat(_gBufferShader.GetUniformLocation("uTileSize"), TileSize);
            _gBufferShader.SetFloat(_gBufferShader.GetUniformLocation("uPixelPadding"), 0.1f);

            _uFogStartLoc = _gBufferShader.GetUniformLocation("u_fogStart");
            _uFogEndLoc = _gBufferShader.GetUniformLocation("u_fogEnd");
            _uFogColorLoc = _gBufferShader.GetUniformLocation("u_fogColor");

            _gBufferViewLocation = _gBufferShader.GetUniformLocation("view");
            _gBufferProjectionLocation = _gBufferShader.GetUniformLocation("projection");
            _uUseWireframeColorLoc = _gBufferShader.GetUniformLocation("u_UseWireframeColor");
            _uWireframeColorLoc = _gBufferShader.GetUniformLocation("u_WireframeColor");
        }
        OnResize(width, height);
    }

    public void OnResize(uint width, uint height)
    {
        GBuffer?.Dispose();
        GBuffer = new Framebuffer(gl, width, height);
    }

    public unsafe void Execute()
    {
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        gl.ClearDepth(0.0f);
        gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

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

            _blockTextureAtlas.Bind(TextureUnit.Texture0);

            chunkRenderer.Bind();

            chunkRenderer.DrawGPUIndirectCount(
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
        GBuffer.Unbind();
        gl.ClearDepth(1.0f);
    }

    public void Dispose()
    {
        _gBufferShader?.Dispose();
        _blockTextureAtlas?.Dispose();
        GBuffer?.Dispose();
    }
}