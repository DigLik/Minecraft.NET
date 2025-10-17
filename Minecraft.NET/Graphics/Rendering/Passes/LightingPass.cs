namespace Minecraft.NET.Graphics.Rendering.Passes;

public class LightingPass : IRenderPass
{
    private GL _gl = null!;
    private Shader _lightingShader = null!;
    public Framebuffer PostProcessFbo { get; private set; } = null!;
    private uint _quadVao, _quadVbo;

    public unsafe void Initialize(GL gl, uint width, uint height)
    {
        _gl = gl;

        if (_lightingShader == null)
        {
            _lightingShader = new Shader(gl, Shader.LoadFromFile("Assets/Shaders/lighting.vert"), Shader.LoadFromFile("Assets/Shaders/lighting.frag"));
            _lightingShader.Use();
            _lightingShader.SetInt(_lightingShader.GetUniformLocation("gAlbedo"), 0);
            _lightingShader.SetInt(_lightingShader.GetUniformLocation("gPosition"), 1);
            _lightingShader.SetVector3(_lightingShader.GetUniformLocation("u_fogColor"), new Vector3(0.53f, 0.81f, 0.92f));
            _lightingShader.SetFloat(_lightingShader.GetUniformLocation("u_fogStart"), RenderDistance * ChunkSize * 0.5f);
            _lightingShader.SetFloat(_lightingShader.GetUniformLocation("u_fogEnd"), RenderDistance * ChunkSize * 0.95f);

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
        }

        OnResize(width, height);
    }

    public void OnResize(uint width, uint height)
    {
        PostProcessFbo?.Dispose();
        PostProcessFbo = new Framebuffer(_gl, width, height, false);
    }

    public void Execute(GL gl, SharedRenderData sharedData)
    {
        PostProcessFbo.Bind();
        gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        _lightingShader.Use();

        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, sharedData.GBuffer!.ColorAttachments[2]);

        gl.ActiveTexture(TextureUnit.Texture1);
        gl.BindTexture(TextureTarget.Texture2D, sharedData.GBuffer!.ColorAttachments[0]);

        gl.BindVertexArray(_quadVao);
        gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
        PostProcessFbo.Unbind();
    }

    public void Dispose()
    {
        _lightingShader?.Dispose();
        PostProcessFbo?.Dispose();
        GC.SuppressFinalize(this);
    }
}