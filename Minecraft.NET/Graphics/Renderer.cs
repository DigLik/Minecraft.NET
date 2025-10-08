using Minecraft.NET.Core;
using Silk.NET.Maths;
using System.Runtime.InteropServices;

namespace Minecraft.NET.Graphics;

public sealed class Renderer
{
    private GL _gl = null!;
    public Texture BlockTextureAtlas { get; private set; } = null!;

    private readonly Frustum _frustum = new();

    private Framebuffer _gBuffer = null!;
    private Shader _gBufferShader = null!;

    private Framebuffer _ssaoFbo = null!;
    private Framebuffer _ssaoBlurFbo = null!;
    private Shader _ssaoShader = null!;
    private Shader _ssaoBlurShader = null!;
    private uint _ssaoNoiseTexture;
    private readonly List<Vector3> _ssaoKernel = [];

    private Shader _lightingShader = null!;

    private uint _quadVao, _quadVbo;

    private Vector2D<int> _viewportSize;

    private static string LoadShaderSource(string path)
        => new([.. File.ReadAllText(path).Where(c => c < 128)]);

    public unsafe void Load(GL gl)
    {
        _gl = gl;

        BlockTextureAtlas = new Texture(_gl, "Assets/Textures/atlas.png");

        string gBufferVert = LoadShaderSource("Assets/Shaders/g_buffer.vert");
        string gBufferFrag = LoadShaderSource("Assets/Shaders/g_buffer.frag");
        _gBufferShader = new Shader(_gl, gBufferVert, gBufferFrag);

        _gBufferShader.Use();
        _gBufferShader.SetInt(_gBufferShader.GetUniformLocation("uTexture"), 0);
        _gBufferShader.SetVector2(_gBufferShader.GetUniformLocation("uTileAtlasSize"), new Vector2(Constants.AtlasWidth, Constants.AtlasHeight));
        _gBufferShader.SetFloat(_gBufferShader.GetUniformLocation("uTileSize"), Constants.TileSize);
        _gBufferShader.SetFloat(_gBufferShader.GetUniformLocation("uPixelPadding"), 0.1f);

        string ssaoVert = LoadShaderSource("Assets/Shaders/ssao.vert");
        string ssaoFrag = LoadShaderSource("Assets/Shaders/ssao.frag");
        _ssaoShader = new Shader(_gl, ssaoVert, ssaoFrag);

        string ssaoBlurVert = LoadShaderSource("Assets/Shaders/ssao_blur.vert");
        string ssaoBlurFrag = LoadShaderSource("Assets/Shaders/ssao_blur.frag");
        _ssaoBlurShader = new Shader(_gl, ssaoBlurVert, ssaoBlurFrag);

        string lightingVert = LoadShaderSource("Assets/Shaders/lighting.vert");
        string lightingFrag = LoadShaderSource("Assets/Shaders/lighting.frag");
        _lightingShader = new Shader(_gl, lightingVert, lightingFrag);

        _lightingShader.Use();
        _lightingShader.SetInt(_lightingShader.GetUniformLocation("gAlbedo"), 0);
        _lightingShader.SetInt(_lightingShader.GetUniformLocation("ssao"), 1);

        _ssaoShader.Use();
        _ssaoShader.SetInt(_ssaoShader.GetUniformLocation("gPosition"), 0);
        _ssaoShader.SetInt(_ssaoShader.GetUniformLocation("gNormal"), 1);
        _ssaoShader.SetInt(_ssaoShader.GetUniformLocation("texNoise"), 2);

        _ssaoBlurShader.Use();
        _ssaoBlurShader.SetInt(_ssaoBlurShader.GetUniformLocation("ssaoInput"), 0);

        var random = new Random();
        for (int i = 0; i < 64; ++i)
        {
            var sample = new Vector3(
                (float)random.NextDouble() * 2.0f - 1.0f,
                (float)random.NextDouble() * 2.0f - 1.0f,
                (float)random.NextDouble()
            );
            sample = Vector3.Normalize(sample);
            sample *= (float)random.NextDouble();
            float scale = (float)i / 64.0f;
            scale = 0.1f + scale * scale * (1.0f - 0.1f);
            sample *= scale;
            _ssaoKernel.Add(sample);
        }

        var ssaoNoise = new List<Vector3>();
        for (int i = 0; i < 16; i++)
        {
            ssaoNoise.Add(new Vector3(
                (float)random.NextDouble() * 2.0f - 1.0f,
                (float)random.NextDouble() * 2.0f - 1.0f,
                0.0f
            ));
        }
        _ssaoNoiseTexture = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _ssaoNoiseTexture);
        fixed (Vector3* p = CollectionsMarshal.AsSpan(ssaoNoise))
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba16f, 4, 4, 0, PixelFormat.Rgb, PixelType.Float, p);
        }
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);

        float[] quadVertices =
        [
            -1.0f,  1.0f, 0.0f, 1.0f,
            -1.0f, -1.0f, 0.0f, 0.0f,
             1.0f,  1.0f, 1.0f, 1.0f,
             1.0f, -1.0f, 1.0f, 0.0f,
        ];
        _quadVao = _gl.GenVertexArray();
        _quadVbo = _gl.GenBuffer();
        _gl.BindVertexArray(_quadVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _quadVbo);
        fixed (float* p = quadVertices)
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(quadVertices.Length * sizeof(float)), p, BufferUsageARB.StaticDraw);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)0);
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));

        _gl.ClearColor(0.53f, 0.81f, 0.92f, 1.0f);
        _gl.Enable(EnableCap.DepthTest);
        _gl.Enable(EnableCap.CullFace);
    }

    public void OnFramebufferResize(Vector2D<int> size)
    {
        _gl.Viewport(size);
        _viewportSize = size;

        _gBuffer?.Dispose();
        _ssaoFbo?.Dispose();
        _ssaoBlurFbo?.Dispose();

        _gBuffer = new Framebuffer(_gl, (uint)size.X, (uint)size.Y);
        _ssaoFbo = new Framebuffer(_gl, (uint)size.X, (uint)size.Y, true);
        _ssaoBlurFbo = new Framebuffer(_gl, (uint)size.X, (uint)size.Y, true);
    }

    public unsafe void Render(IReadOnlyCollection<ChunkSection> chunks, Camera camera)
    {
        if (_gBuffer is null)
            return;

        _gBuffer.Bind();
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        var view = camera.GetViewMatrix();
        var projection = camera.GetProjectionMatrix((float)_viewportSize.X / _viewportSize.Y);
        _frustum.Update(view * projection);

        _gBufferShader.Use();
        _gBufferShader.SetMatrix4x4(_gBufferShader.GetUniformLocation("view"), view);
        _gBufferShader.SetMatrix4x4(_gBufferShader.GetUniformLocation("projection"), projection);

        BlockTextureAtlas.Bind(TextureUnit.Texture0);

        lock (chunks)
        {
            foreach (var chunk in chunks)
            {
                var mesh = chunk.Mesh;
                if (mesh == null || mesh.IndexCount == 0 || chunk.State != ChunkState.Rendered) continue;

                var chunkWorldPos = chunk.Position * ChunkSize;
                var box = new BoundingBox(chunkWorldPos, chunkWorldPos + new Vector3(ChunkSize));
                if (!_frustum.Intersects(box))
                    continue;

                mesh.Bind();
                var model = Matrix4x4.CreateTranslation(chunkWorldPos);
                _gBufferShader.SetMatrix4x4(_gBufferShader.GetUniformLocation("model"), model);

                _gl.DrawElements(PrimitiveType.Triangles, (uint)mesh.IndexCount, DrawElementsType.UnsignedInt, null);
            }
        }
        _gBuffer.Unbind();

        _ssaoFbo.Bind();
        _gl.Clear(ClearBufferMask.ColorBufferBit);
        _ssaoShader.Use();
        for (int i = 0; i < 64; ++i)
            _ssaoShader.SetVector3(_ssaoShader.GetUniformLocation($"samples[{i}]"), _ssaoKernel[i]);
        _ssaoShader.SetMatrix4x4(_ssaoShader.GetUniformLocation("projection"), projection);
        _ssaoShader.SetVector2(_ssaoShader.GetUniformLocation("u_ScreenSize"), new Vector2(_viewportSize.X, _viewportSize.Y));

        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _gBuffer.ColorAttachments[0]);
        _gl.ActiveTexture(TextureUnit.Texture1);
        _gl.BindTexture(TextureTarget.Texture2D, _gBuffer.ColorAttachments[1]);
        _gl.ActiveTexture(TextureUnit.Texture2);
        _gl.BindTexture(TextureTarget.Texture2D, _ssaoNoiseTexture);

        _gl.BindVertexArray(_quadVao);
        _gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
        _ssaoFbo.Unbind();

        _ssaoBlurFbo.Bind();
        _gl.Clear(ClearBufferMask.ColorBufferBit);
        _ssaoBlurShader.Use();
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _ssaoFbo.ColorAttachments[0]);

        _gl.BindVertexArray(_quadVao);
        _gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
        _ssaoBlurFbo.Unbind();

        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        _lightingShader.Use();
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _gBuffer.ColorAttachments[2]);
        _gl.ActiveTexture(TextureUnit.Texture1);
        _gl.BindTexture(TextureTarget.Texture2D, _ssaoBlurFbo.ColorAttachments[0]);

        _gl.BindVertexArray(_quadVao);
        _gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
    }
}