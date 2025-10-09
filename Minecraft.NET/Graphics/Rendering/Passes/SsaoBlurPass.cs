using Minecraft.NET.Abstractions;

namespace Minecraft.NET.Graphics.Rendering.Passes;

public class SsaoBlurPass : IRenderPass
{
    private GL _gl = null!;
    private Shader _ssaoBlurShader = null!;
    public Framebuffer SsaoBlurFbo { get; private set; } = null!;
    private uint _quadVao, _quadVbo;

    public unsafe void Initialize(GL gl, uint width, uint height)
    {
        _gl = gl;
        _ssaoBlurShader = new Shader(gl, Shader.LoadFromFile("Assets/Shaders/ssao_blur.vert"), Shader.LoadFromFile("Assets/Shaders/ssao_blur.frag"));
        _ssaoBlurShader.Use();
        _ssaoBlurShader.SetInt(_ssaoBlurShader.GetUniformLocation("ssaoInput"), 0);
        _ssaoBlurShader.SetInt(_ssaoBlurShader.GetUniformLocation("gPosition"), 1);

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
        SsaoBlurFbo?.Dispose();
        SsaoBlurFbo = new Framebuffer(_gl, width, height, true);
    }

    public void Execute(GL gl, SharedRenderData sharedData)
    {
        SsaoBlurFbo.Bind();
        gl.Clear(ClearBufferMask.ColorBufferBit);
        _ssaoBlurShader.Use();

        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, sharedData.SsaoBuffer!.ColorAttachments[0]);
        gl.ActiveTexture(TextureUnit.Texture1);
        gl.BindTexture(TextureTarget.Texture2D, sharedData.GBuffer!.ColorAttachments[0]);

        gl.BindVertexArray(_quadVao);
        gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
        SsaoBlurFbo.Unbind();
    }

    public void Dispose()
    {
        _ssaoBlurShader?.Dispose();
        SsaoBlurFbo?.Dispose();
        GC.SuppressFinalize(this);
    }
}