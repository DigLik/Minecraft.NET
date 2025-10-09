using Minecraft.NET.Abstractions;

namespace Minecraft.NET.Graphics.Rendering.Passes;

public class GBufferPass : IRenderPass
{
    private GL _gl = null!;
    private Shader _gBufferShader = null!;
    private Texture _blockTextureAtlas = null!;
    private int _gBufferInverseViewLocation;
    public Framebuffer GBuffer { get; private set; } = null!;

    public unsafe void Initialize(GL gl, uint width, uint height)
    {
        _gl = gl;
        _blockTextureAtlas = new Texture(gl, "Assets/Textures/atlas.png");

        _gBufferShader = new Shader(gl, Shader.LoadFromFile("Assets/Shaders/g_buffer.vert"), Shader.LoadFromFile("Assets/Shaders/g_buffer.frag"));
        _gBufferShader.Use();
        _gBufferShader.SetInt(_gBufferShader.GetUniformLocation("uTexture"), 0);
        _gBufferShader.SetVector2(_gBufferShader.GetUniformLocation("uTileAtlasSize"), new Vector2(Constants.AtlasWidth, Constants.AtlasHeight));
        _gBufferShader.SetFloat(_gBufferShader.GetUniformLocation("uTileSize"), Constants.TileSize);
        _gBufferShader.SetFloat(_gBufferShader.GetUniformLocation("uPixelPadding"), 0.1f);
        _gBufferInverseViewLocation = _gBufferShader.GetUniformLocation("inverseView");

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

        if (sharedData.VisibleMeshes.Count > 0)
        {
            Matrix4x4.Invert(sharedData.ViewMatrix, out var inverseView);

            _gBufferShader.Use();
            _gBufferShader.SetMatrix4x4(_gBufferShader.GetUniformLocation("view"), sharedData.RelativeViewMatrix);
            _gBufferShader.SetMatrix4x4(_gBufferShader.GetUniformLocation("projection"), sharedData.ProjectionMatrix);
            _gBufferShader.SetMatrix4x4(_gBufferInverseViewLocation, inverseView);

            _blockTextureAtlas.Bind(TextureUnit.Texture0);

            for (int i = 0; i < sharedData.VisibleMeshes.Count; i++)
            {
                var mesh = sharedData.VisibleMeshes[i];
                mesh!.Bind();
                gl.DrawElementsInstancedBaseInstance(PrimitiveType.Triangles, (uint)mesh.IndexCount, DrawElementsType.UnsignedInt, null, 1, (uint)i);
            }
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