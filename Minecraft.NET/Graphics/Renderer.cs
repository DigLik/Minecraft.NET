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
    private int _gBufferInverseViewLocation;

    private Framebuffer _ssaoFbo = null!;
    private Framebuffer _ssaoBlurFbo = null!;
    private Shader _ssaoShader = null!;
    private Shader _ssaoBlurShader = null!;
    private uint _ssaoNoiseTexture;
    private readonly List<Vector3> _ssaoKernel = [];

    private Shader _lightingShader = null!;

    private Framebuffer _postProcessFbo = null!;
    private Shader _fxaaShader = null!;
    private int _fxaaInverseScreenSizeLocation;

    private uint _quadVao, _quadVbo;

    private Vector2D<int> _viewportSize;

    private uint _instanceVbo;
    public uint InstanceVbo => _instanceVbo;
    private readonly List<Matrix4x4> _modelMatrices = [];
    private readonly List<Mesh> _visibleMeshes = [];
    private const int MaxInstances = 8192;

    public int VisibleSectionCount { get; private set; }

    private static string LoadShaderSource(string path)
        => new([.. File.ReadAllText(path).Where(c => c < 128)]);

    public unsafe void Load(GL gl)
    {
        _gl = gl;

        _instanceVbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _instanceVbo);
        _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(MaxInstances * sizeof(Matrix4x4)), null, BufferUsageARB.StreamDraw);

        BlockTextureAtlas = new Texture(_gl, "Assets/Textures/atlas.png");

        string gBufferVert = LoadShaderSource("Assets/Shaders/g_buffer.vert");
        string gBufferFrag = LoadShaderSource("Assets/Shaders/g_buffer.frag");
        _gBufferShader = new Shader(_gl, gBufferVert, gBufferFrag);

        _gBufferShader.Use();
        _gBufferShader.SetInt(_gBufferShader.GetUniformLocation("uTexture"), 0);
        _gBufferShader.SetVector2(_gBufferShader.GetUniformLocation("uTileAtlasSize"), new Vector2(Constants.AtlasWidth, Constants.AtlasHeight));
        _gBufferShader.SetFloat(_gBufferShader.GetUniformLocation("uTileSize"), Constants.TileSize);
        _gBufferShader.SetFloat(_gBufferShader.GetUniformLocation("uPixelPadding"), 0.1f);
        _gBufferInverseViewLocation = _gBufferShader.GetUniformLocation("inverseView");

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
        _lightingShader.SetInt(_lightingShader.GetUniformLocation("gPosition"), 2);
        _lightingShader.SetVector3(_lightingShader.GetUniformLocation("u_fogColor"), new Vector3(0.53f, 0.81f, 0.92f));
        _lightingShader.SetFloat(_lightingShader.GetUniformLocation("u_fogStart"), RenderDistance * ChunkSize * 0.5f);
        _lightingShader.SetFloat(_lightingShader.GetUniformLocation("u_fogEnd"), RenderDistance * ChunkSize * 0.95f);

        string fxaaVert = LoadShaderSource("Assets/Shaders/fxaa.vert");
        string fxaaFrag = LoadShaderSource("Assets/Shaders/fxaa.frag");
        _fxaaShader = new Shader(_gl, fxaaVert, fxaaFrag);
        _fxaaShader.Use();
        _fxaaShader.SetInt(_fxaaShader.GetUniformLocation("uTexture"), 0);
        _fxaaInverseScreenSizeLocation = _fxaaShader.GetUniformLocation("u_inverseScreenSize");

        _ssaoShader.Use();
        _ssaoShader.SetInt(_ssaoShader.GetUniformLocation("gPosition"), 0);
        _ssaoShader.SetInt(_ssaoShader.GetUniformLocation("gNormal"), 1);
        _ssaoShader.SetInt(_ssaoShader.GetUniformLocation("texNoise"), 2);

        _ssaoBlurShader.Use();
        _ssaoBlurShader.SetInt(_ssaoBlurShader.GetUniformLocation("ssaoInput"), 0);
        _ssaoBlurShader.SetInt(_ssaoBlurShader.GetUniformLocation("gPosition"), 1);

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
            ssaoNoise.Add(new Vector3(
                (float)random.NextDouble() * 2.0f - 1.0f,
                (float)random.NextDouble() * 2.0f - 1.0f,
                0.0f
            ));

        _ssaoNoiseTexture = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _ssaoNoiseTexture);
        fixed (Vector3* p = CollectionsMarshal.AsSpan(ssaoNoise))
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba16f, 4, 4, 0, PixelFormat.Rgb, PixelType.Float, p);
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
        if (_gl is null) return;

        _gl.Viewport(size);
        _viewportSize = size;

        _gBuffer?.Dispose();
        _ssaoFbo?.Dispose();
        _ssaoBlurFbo?.Dispose();
        _postProcessFbo?.Dispose();

        _gBuffer = new Framebuffer(_gl, (uint)size.X, (uint)size.Y);
        _ssaoFbo = new Framebuffer(_gl, (uint)size.X, (uint)size.Y, true);
        _ssaoBlurFbo = new Framebuffer(_gl, (uint)size.X, (uint)size.Y, true);
        _postProcessFbo = new Framebuffer(_gl, (uint)size.X, (uint)size.Y, false);
    }

    public unsafe void Render(IReadOnlyCollection<ChunkColumn> columns, Camera camera)
    {
        if (_gBuffer is null || _postProcessFbo is null)
            return;

        var projection = camera.GetProjectionMatrix((float)_viewportSize.X / _viewportSize.Y);

        Vector3d cameraOrigin = new(Math.Floor(camera.Position.X), Math.Floor(camera.Position.Y), Math.Floor(camera.Position.Z));
        Vector3 cameraRenderPos = (Vector3)(camera.Position - cameraOrigin);
        var relativeViewMatrix = Matrix4x4.CreateLookAt(cameraRenderPos, cameraRenderPos + camera.Front, camera.Up);

        _frustum.Update(relativeViewMatrix * projection);

        var fullViewMatrix = camera.GetViewMatrix();
        Matrix4x4.Invert(fullViewMatrix, out var inverseView);

        _visibleMeshes.Clear();
        _modelMatrices.Clear();

        foreach (var column in columns)
        {
            var chunkPosDouble = new Vector3d(column.Position.X, 0, column.Position.Y);
            var chunkWorldPosBase = chunkPosDouble * ChunkSize;

            for (int y = 0; y < WorldHeightInChunks; y++)
            {
                var mesh = column.Meshes[y];
                if (mesh == null || mesh.IndexCount == 0)
                    continue;

                int worldOffsetY = (y - VerticalChunkOffset) * ChunkSize;
                var chunkWorldPos = chunkWorldPosBase + new Vector3d(0, worldOffsetY, 0);
                var relativeChunkPosDouble = chunkWorldPos - cameraOrigin;
                var relativeChunkPos = (Vector3)relativeChunkPosDouble;

                var box = new BoundingBox(relativeChunkPos, relativeChunkPos + new Vector3(ChunkSize));
                if (!_frustum.Intersects(box))
                    continue;

                _visibleMeshes.Add(mesh);
                _modelMatrices.Add(Matrix4x4.CreateTranslation(relativeChunkPos));
            }
        }

        VisibleSectionCount = _visibleMeshes.Count;

        _gBuffer.Bind();
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        if (_visibleMeshes.Count > 0)
        {
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _instanceVbo);
            fixed (Matrix4x4* p = CollectionsMarshal.AsSpan(_modelMatrices))
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(_modelMatrices.Count * sizeof(Matrix4x4)), p);

            _gBufferShader.Use();
            _gBufferShader.SetMatrix4x4(_gBufferShader.GetUniformLocation("view"), relativeViewMatrix);
            _gBufferShader.SetMatrix4x4(_gBufferShader.GetUniformLocation("projection"), projection);
            _gBufferShader.SetMatrix4x4(_gBufferInverseViewLocation, inverseView);

            BlockTextureAtlas.Bind(TextureUnit.Texture0);

            for (int i = 0; i < _visibleMeshes.Count; i++)
            {
                var mesh = _visibleMeshes[i];
                mesh!.Bind();
                _gl.DrawElementsInstancedBaseInstance(PrimitiveType.Triangles, (uint)mesh.IndexCount, DrawElementsType.UnsignedInt, null, 1, (uint)i);
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
        _gl.ActiveTexture(TextureUnit.Texture1);
        _gl.BindTexture(TextureTarget.Texture2D, _gBuffer.ColorAttachments[0]);

        _gl.BindVertexArray(_quadVao);
        _gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
        _ssaoBlurFbo.Unbind();

        _postProcessFbo.Bind();
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        _lightingShader.Use();
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _gBuffer.ColorAttachments[2]);
        _gl.ActiveTexture(TextureUnit.Texture1);
        _gl.BindTexture(TextureTarget.Texture2D, _ssaoBlurFbo.ColorAttachments[0]);
        _gl.ActiveTexture(TextureUnit.Texture2);
        _gl.BindTexture(TextureTarget.Texture2D, _gBuffer.ColorAttachments[0]);

        _gl.BindVertexArray(_quadVao);
        _gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
        _postProcessFbo.Unbind();

        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        _fxaaShader.Use();
        _fxaaShader.SetVector2(_fxaaInverseScreenSizeLocation, new Vector2(1.0f / _viewportSize.X, 1.0f / _viewportSize.Y));
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _postProcessFbo.ColorAttachments[0]);

        _gl.BindVertexArray(_quadVao);
        _gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
    }
}