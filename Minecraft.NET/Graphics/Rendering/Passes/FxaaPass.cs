using Minecraft.NET.Abstractions;

namespace Minecraft.NET.Graphics.Rendering.Passes;

public class FxaaPass : IRenderPass
{
    private Shader _fxaaShader = null!;
    private int _fxaaInverseScreenSizeLocation;
    private uint _quadVao, _quadVbo;

    public unsafe void Initialize(GL gl, uint width, uint height)
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

    public void Execute(GL gl, SharedRenderData sharedData)
    {
        gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        _fxaaShader.Use();
        _fxaaShader.SetVector2(_fxaaInverseScreenSizeLocation, new Vector2(1.0f / sharedData.ViewportSize.X, 1.0f / sharedData.ViewportSize.Y));

        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, sharedData.PostProcessBuffer!.ColorAttachments[0]);

        gl.BindVertexArray(_quadVao);
        gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
    }

    public void OnResize(uint width, uint height) { }

    public void Dispose()
    {
        _fxaaShader?.Dispose();
    }
}