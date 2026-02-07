namespace Minecraft.NET.Graphics.Rendering.Passes;

public class LightingPass(IGlContextAccessor glAccessor, FrameContext frameContext, RenderResources resources) : IRenderPass
{
    public int Priority => 1000;
    public string Name => "Deferred Lighting";
    public GL Gl => glAccessor.Gl;

    private Shader _lightingShader = null!;
    private uint _quadVao, _quadVbo;

    public unsafe void Initialize(uint width, uint height)
    {
        if (_lightingShader == null)
        {
            _lightingShader = new Shader(Gl, Shader.LoadFromFile("Assets/Shaders/lighting.vert"), Shader.LoadFromFile("Assets/Shaders/lighting.frag"));
            _lightingShader.Use();
            _lightingShader.SetInt(_lightingShader.GetUniformLocation("gNormal"), 0);
            _lightingShader.SetInt(_lightingShader.GetUniformLocation("gAlbedo"), 1);
            _lightingShader.SetInt(_lightingShader.GetUniformLocation("gDepth"), 2);

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

            _quadVao = Gl.GenVertexArray();
            _quadVbo = Gl.GenBuffer();
            Gl.BindVertexArray(_quadVao);
            Gl.BindBuffer(BufferTargetARB.ArrayBuffer, _quadVbo);
            fixed (float* p = quadVertices)
                Gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(quadVertices.Length * sizeof(float)), p, BufferUsageARB.StaticDraw);
            Gl.EnableVertexAttribArray(0);
            Gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)0);
            Gl.EnableVertexAttribArray(1);
            Gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));
        }
    }

    public void OnResize(uint width, uint height)
    {
        resources.PostProcessFbo?.Dispose();
        resources.PostProcessFbo = new Framebuffer(Gl, width, height, InternalFormat.Rgba8, PixelFormat.Rgba, PixelType.UnsignedByte);
    }

    public void Execute(RenderResources renderResources)
    {
        var gBuffer = renderResources.GBuffer;
        var outFbo = renderResources.PostProcessFbo;

        if (gBuffer == null || outFbo == null)
            return;

        outFbo.Bind();
        Gl.Disable(EnableCap.DepthTest);
        Gl.Disable(EnableCap.CullFace);

        Gl.Clear(ClearBufferMask.ColorBufferBit);

        _lightingShader.Use();

        Matrix4x4.Invert(frameContext.RelativeViewMatrix, out var invView);
        Matrix4x4.Invert(frameContext.ProjectionMatrix, out var invProj);

        _lightingShader.SetMatrix4x4(_lightingShader.GetUniformLocation("u_inverseView"), invView);
        _lightingShader.SetMatrix4x4(_lightingShader.GetUniformLocation("u_inverseProjection"), invProj);

        Gl.ActiveTexture(TextureUnit.Texture0);
        Gl.BindTexture(TextureTarget.Texture2D, gBuffer.ColorAttachments[0]);

        Gl.ActiveTexture(TextureUnit.Texture1);
        Gl.BindTexture(TextureTarget.Texture2D, gBuffer.ColorAttachments[1]);

        Gl.ActiveTexture(TextureUnit.Texture2);
        Gl.BindTexture(TextureTarget.Texture2D, gBuffer.DepthAttachment);

        Gl.BindVertexArray(_quadVao);
        Gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);

        Gl.Enable(EnableCap.DepthTest);
        Gl.Enable(EnableCap.CullFace);
    }

    public void Dispose()
    {
        _lightingShader?.Dispose();
        Gl.DeleteVertexArray(_quadVao);
        Gl.DeleteBuffer(_quadVbo);
    }
}