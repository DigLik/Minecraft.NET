namespace Minecraft.NET.Graphics.Rendering.Passes;

public class LightingPass(GL gl, FrameContext frameContext, GBufferPass gBufferPass) : IRenderPass
{
    private Shader _lightingShader = null!;
    private uint _quadVao, _quadVbo;

    public Framebuffer PostProcessFbo { get; private set; } = null!;

    public unsafe void Initialize(uint width, uint height)
    {
        if (_lightingShader == null)
        {
            _lightingShader = new Shader(gl, Shader.LoadFromFile("Assets/Shaders/lighting.vert"), Shader.LoadFromFile("Assets/Shaders/lighting.frag"));

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
        PostProcessFbo = new Framebuffer(gl, width, height, singleChannel: false);
    }

    public void Execute()
    {
        PostProcessFbo.Bind();
        gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        _lightingShader.Use();

        Matrix4x4.Invert(frameContext.RelativeViewMatrix, out var invView);
        Matrix4x4.Invert(frameContext.ProjectionMatrix, out var invProj);

        _lightingShader.SetMatrix4x4(_lightingShader.GetUniformLocation("u_inverseView"), invView);
        _lightingShader.SetMatrix4x4(_lightingShader.GetUniformLocation("u_inverseProjection"), invProj);

        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, gBufferPass.GBuffer.ColorAttachments[0]);

        gl.ActiveTexture(TextureUnit.Texture1);
        gl.BindTexture(TextureTarget.Texture2D, gBufferPass.GBuffer.ColorAttachments[1]);

        gl.ActiveTexture(TextureUnit.Texture2);
        gl.BindTexture(TextureTarget.Texture2D, gBufferPass.GBuffer.DepthAttachment);

        gl.BindVertexArray(_quadVao);
        gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);

        PostProcessFbo.Unbind();
    }

    public void Dispose()
    {
        _lightingShader?.Dispose();
        PostProcessFbo?.Dispose();
        gl.DeleteVertexArray(_quadVao);
        gl.DeleteBuffer(_quadVbo);
    }
}