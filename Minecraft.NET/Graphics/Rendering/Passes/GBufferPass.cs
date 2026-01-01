using Minecraft.NET.Engine;
using Minecraft.NET.Services;

namespace Minecraft.NET.Graphics.Rendering.Passes;

public class GBufferPass(
    GL gl,
    IChunkRenderer chunkRenderer,
    FrameContext frameContext,
    SceneCuller sceneCuller
    ) : IRenderPass
{
    private Shader _gBufferShader = null!;
    private Texture _blockTextureAtlas = null!;
    private int _gBufferViewLocation;
    private int _gBufferProjectionLocation;

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

            _gBufferViewLocation = _gBufferShader.GetUniformLocation("view");
            _gBufferProjectionLocation = _gBufferShader.GetUniformLocation("projection");
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
        GBuffer.Bind();
        gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        var visibleScene = sceneCuller.Result;

        if (visibleScene.VisibleSectionCount > 0)
        {
            _gBufferShader.Use();
            _gBufferShader.SetMatrix4x4(_gBufferViewLocation, frameContext.RelativeViewMatrix);
            _gBufferShader.SetMatrix4x4(_gBufferProjectionLocation, frameContext.ProjectionMatrix);

            _blockTextureAtlas.Bind(TextureUnit.Texture0);

            fixed (DrawElementsIndirectCommand* pCmd = visibleScene.IndirectCommands)
                chunkRenderer.UploadIndirectCommands(pCmd, visibleScene.VisibleSectionCount);

            chunkRenderer.Bind();
            chunkRenderer.Draw(visibleScene.VisibleSectionCount);
        }
        GBuffer.Unbind();
    }

    public void Dispose()
    {
        _gBufferShader?.Dispose();
        _blockTextureAtlas?.Dispose();
        GBuffer?.Dispose();
    }
}