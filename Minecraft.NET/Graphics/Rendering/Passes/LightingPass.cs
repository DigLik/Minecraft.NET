namespace Minecraft.NET.Graphics.Rendering.Passes;

public class LightingPass(GL gl) : IRenderPass
{
    public int Priority => 1000;
    public string Name => "Deferred Lighting";

    private Shader _lightingShader = null!;
    private uint _quadVao, _quadVbo;

    public unsafe void Initialize(uint width, uint height)
    {
        if (_lightingShader == null)
        {
            _lightingShader = new Shader(gl, Shader.LoadFromFile("Assets/Shaders/lighting.vert"), Shader.LoadFromFile("Assets/Shaders/lighting.frag"));
            _lightingShader.Use();
            _lightingShader.SetInt(_lightingShader.GetUniformLocation("gNormal"), 0);
            _lightingShader.SetInt(_lightingShader.GetUniformLocation("gAlbedo"), 1);
            _lightingShader.SetInt(_lightingShader.GetUniformLocation("gDepth"), 2);

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
    }

    public void OnResize(uint width, uint height)
    {
    }

    public void Execute(RenderResources renderResources)
    {
        var gBuffer = renderResources.GBuffer;

        if (gBuffer == null)
            return;

        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        gl.Disable(EnableCap.DepthTest);
        gl.Disable(EnableCap.CullFace);

        gl.Clear(ClearBufferMask.ColorBufferBit);

        _lightingShader.Use();

        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, gBuffer.ColorAttachments[0]);
        gl.ActiveTexture(TextureUnit.Texture1);
        gl.BindTexture(TextureTarget.Texture2D, gBuffer.ColorAttachments[1]);
        gl.ActiveTexture(TextureUnit.Texture2);
        gl.BindTexture(TextureTarget.Texture2D, gBuffer.DepthAttachment);

        gl.BindVertexArray(_quadVao);
        gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);

        gl.Enable(EnableCap.DepthTest);
        gl.Enable(EnableCap.CullFace);
    }

    public void Dispose()
    {
        _lightingShader?.Dispose();
        gl.DeleteVertexArray(_quadVao);
        gl.DeleteBuffer(_quadVbo);
    }
}