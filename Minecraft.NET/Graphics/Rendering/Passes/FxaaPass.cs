namespace Minecraft.NET.Graphics.Rendering.Passes;

public class FxaaPass(GL gl, FrameContext frameContext, LightingPass lightingPass) : IRenderPass
{
    private Shader _fxaaShader = null!;
    private int _fxaaInverseScreenSizeLocation;
    private uint _quadVao, _quadVbo;

    public unsafe void Initialize(uint width, uint height)
    {
        if (_fxaaShader == null)
        {
            _fxaaShader = new Shader(gl, Shader.LoadFromFile("Assets/Shaders/fxaa.vert"), Shader.LoadFromFile("Assets/Shaders/fxaa.frag"));
            _fxaaShader.Use();
            _fxaaShader.SetInt(_fxaaShader.GetUniformLocation("uTexture"), 0);
            _fxaaInverseScreenSizeLocation = _fxaaShader.GetUniformLocation("u_inverseScreenSize");

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

    public void OnResize(uint width, uint height) { }

    public void Execute()
    {
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        _fxaaShader.Use();

        var viewport = frameContext.ViewportSize;
        _fxaaShader.SetVector2(_fxaaInverseScreenSizeLocation, new Vector2(1.0f / viewport.X, 1.0f / viewport.Y));

        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, lightingPass.PostProcessFbo.ColorAttachments[0]);

        gl.BindVertexArray(_quadVao);
        gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
    }

    public void Dispose()
    {
        _fxaaShader?.Dispose();
        gl.DeleteVertexArray(_quadVao);
        gl.DeleteBuffer(_quadVbo);
    }
}