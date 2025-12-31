namespace Minecraft.NET.Graphics.Rendering.Passes;

public class GBufferPass(ChunkRenderer chunkRenderer) : IRenderPass
{
    private GL _gl = null!;
    private Shader _gBufferShader = null!;
    private Texture _blockTextureAtlas = null!;
    private int _gBufferViewLocation;
    private int _gBufferProjectionLocation;

    public Framebuffer GBuffer { get; private set; } = null!;

    private readonly DrawElementsIndirectCommand[] _commands = new DrawElementsIndirectCommand[MaxVisibleSections];

    public unsafe void Initialize(GL gl, uint width, uint height)
    {
        _gl = gl;
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
        GBuffer = new Framebuffer(_gl, width, height);
    }

    public unsafe void Execute(GL gl, SharedRenderData sharedData)
    {
        GBuffer.Bind();
        gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        var visibleCount = sharedData.VisibleCount;

        if (visibleCount > 0)
        {
            _gBufferShader.Use();
            _gBufferShader.SetMatrix4x4(_gBufferViewLocation, sharedData.RelativeViewMatrix);
            _gBufferShader.SetMatrix4x4(_gBufferProjectionLocation, sharedData.ProjectionMatrix);

            _blockTextureAtlas.Bind(TextureUnit.Texture0);

            fixed (DrawElementsIndirectCommand* pCmd = sharedData.IndirectCommands)
                chunkRenderer.UploadIndirectCommands(pCmd, visibleCount);

            chunkRenderer.Bind();
            chunkRenderer.Draw(visibleCount);
        }
        GBuffer.Unbind();
    }

    public void Dispose()
    {
        _gBufferShader?.Dispose();
        _blockTextureAtlas?.Dispose();
        GBuffer?.Dispose();
        GC.SuppressFinalize(this);
    }
}