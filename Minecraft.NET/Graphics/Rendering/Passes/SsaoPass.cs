using System.Runtime.InteropServices;

namespace Minecraft.NET.Graphics.Rendering.Passes;

public class SsaoPass : IRenderPass
{
    private GL _gl = null!;
    private Shader _ssaoShader = null!;
    public Framebuffer SsaoFbo { get; private set; } = null!;
    private uint _ssaoNoiseTexture;
    private readonly List<Vector3> _ssaoKernel = [];
    private uint _quadVao, _quadVbo;

    public unsafe void Initialize(GL gl, uint width, uint height)
    {
        _gl = gl;
        _ssaoShader = new Shader(gl, Shader.LoadFromFile("Assets/Shaders/ssao.vert"), Shader.LoadFromFile("Assets/Shaders/ssao.frag"));
        _ssaoShader.Use();
        _ssaoShader.SetInt(_ssaoShader.GetUniformLocation("gPosition"), 0);
        _ssaoShader.SetInt(_ssaoShader.GetUniformLocation("gNormal"), 1);
        _ssaoShader.SetInt(_ssaoShader.GetUniformLocation("texNoise"), 2);

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

        _ssaoNoiseTexture = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, _ssaoNoiseTexture);
        fixed (Vector3* p = CollectionsMarshal.AsSpan(ssaoNoise))
            gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba16f, 4, 4, 0, PixelFormat.Rgb, PixelType.Float, p);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);

        float[] quadVertices =
        [
            -1.0f,  1.0f, 0.0f, 1.0f,
            -1.0f, -1.0f, 0.0f, 0.0f,
             1.0f,  1.0f, 1.0f, 1.0f,
             1.0f, -1.0f, 1.0f, 0.0f,
        ];
        _quadVao = gl.GenVertexArray();
        _quadVbo = gl.GenBuffer();
        gl.BindVertexArray(_quadVao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _quadVbo);
        fixed (float* p = quadVertices)
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(quadVertices.Length * sizeof(float)), p, BufferUsageARB.StaticDraw);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)0);
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));

        OnResize(width, height);
    }

    public void OnResize(uint width, uint height)
    {
        SsaoFbo?.Dispose();
        SsaoFbo = new Framebuffer(_gl, width, height, true);
    }

    public void Execute(GL gl, SharedRenderData sharedData)
    {
        SsaoFbo.Bind();
        gl.Clear(ClearBufferMask.ColorBufferBit);
        _ssaoShader.Use();
        for (int i = 0; i < 64; ++i)
            _ssaoShader.SetVector3(_ssaoShader.GetUniformLocation($"samples[{i}]"), _ssaoKernel[i]);
        _ssaoShader.SetMatrix4x4(_ssaoShader.GetUniformLocation("projection"), sharedData.ProjectionMatrix);
        _ssaoShader.SetVector2(_ssaoShader.GetUniformLocation("u_ScreenSize"), new Vector2(sharedData.ViewportSize.X, sharedData.ViewportSize.Y));

        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, sharedData.GBuffer!.ColorAttachments[0]);
        gl.ActiveTexture(TextureUnit.Texture1);
        gl.BindTexture(TextureTarget.Texture2D, sharedData.GBuffer.ColorAttachments[1]);
        gl.ActiveTexture(TextureUnit.Texture2);
        gl.BindTexture(TextureTarget.Texture2D, _ssaoNoiseTexture);

        gl.BindVertexArray(_quadVao);
        gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
        SsaoFbo.Unbind();
    }

    public void Dispose()
    {
        _ssaoShader?.Dispose();
        SsaoFbo?.Dispose();
        _gl?.DeleteTexture(_ssaoNoiseTexture);
        GC.SuppressFinalize(this);
    }
}