namespace Minecraft.NET.Graphics.Rendering.Passes;

public class FogPass(GL gl, FrameContext frameContext, RenderResources resources) : IRenderPass
{
    public int Priority => 1500;
    public string Name => "Fog Pass";

    private Shader _fogShader = null!;
    private uint _quadVao, _quadVbo;

    public unsafe void Initialize(uint width, uint height)
    {
        if (_fogShader == null)
        {
            _fogShader = new Shader(gl, Shader.LoadFromFile("Assets/Shaders/fog.vert"), Shader.LoadFromFile("Assets/Shaders/fog.frag"));
            _fogShader.Use();
            _fogShader.SetInt(_fogShader.GetUniformLocation("uColorTex"), 0);
            _fogShader.SetInt(_fogShader.GetUniformLocation("uDepthTex"), 1);

            _fogShader.SetVector3(_fogShader.GetUniformLocation("u_fogColor"), new Vector3(0.53f, 0.81f, 0.92f));
            _fogShader.SetFloat(_fogShader.GetUniformLocation("u_fogStart"), RenderDistance * ChunkSize * 0.5f);
            _fogShader.SetFloat(_fogShader.GetUniformLocation("u_fogEnd"), RenderDistance * ChunkSize * 0.95f);

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
        resources.PostProcessFbo?.Dispose();
        resources.PostProcessFbo = new Framebuffer(gl, width, height, InternalFormat.Rgba8, PixelFormat.Rgba, PixelType.UnsignedByte);
    }

    public void Execute(RenderResources renderResources)
    {
        var lightingFbo = renderResources.LightingFbo;
        var gBuffer = renderResources.GBuffer;
        var outFbo = renderResources.PostProcessFbo;

        if (lightingFbo == null || gBuffer == null || outFbo == null)
            return;

        outFbo.Bind();
        gl.Disable(EnableCap.DepthTest);
        gl.Disable(EnableCap.CullFace);

        gl.Clear(ClearBufferMask.ColorBufferBit);

        _fogShader.Use();

        Matrix4x4.Invert(frameContext.RelativeViewMatrix, out var invView);
        Matrix4x4.Invert(frameContext.ProjectionMatrix, out var invProj);

        _fogShader.SetMatrix4x4(_fogShader.GetUniformLocation("u_inverseView"), invView);
        _fogShader.SetMatrix4x4(_fogShader.GetUniformLocation("u_inverseProjection"), invProj);

        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, lightingFbo.ColorAttachments[0]);

        gl.ActiveTexture(TextureUnit.Texture1);
        gl.BindTexture(TextureTarget.Texture2D, gBuffer.DepthAttachment);

        gl.BindVertexArray(_quadVao);
        gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);

        gl.Enable(EnableCap.DepthTest);
        gl.Enable(EnableCap.CullFace);
    }

    public void Dispose()
    {
        _fogShader?.Dispose();
        gl.DeleteVertexArray(_quadVao);
        gl.DeleteBuffer(_quadVbo);
    }
}