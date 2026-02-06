namespace Minecraft.NET.Graphics.Rendering.Passes;

public class SmaaPass(GL gl, LightingPass lightingPass) : IRenderPass
{
    private Shader _edgeShader = null!;
    private Shader _weightShader = null!;
    private Shader _blendShader = null!;

    private Texture _areaTex = null!;
    private Texture _searchTex = null!;

    private Framebuffer _edgesFbo = null!;
    private Framebuffer _blendFbo = null!;

    private uint _quadVao, _quadVbo;

    private int _edgeScreenSizeLoc;
    private int _weightScreenSizeLoc;
    private int _blendScreenSizeLoc;

    public unsafe void Initialize(uint width, uint height)
    {
        if (_areaTex == null)
        {
            _areaTex = new Texture(gl, "Assets/Textures/SMAA/AreaTexDX10.png");
            _searchTex = new Texture(gl, "Assets/Textures/SMAA/SearchTex.png");

            _areaTex.Bind();
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

            _searchTex.Bind();
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        }

        if (_edgeShader == null)
        {
            var vertSource = Shader.LoadFromFile("Assets/Shaders/smaa.vert");

            _edgeShader = new Shader(gl, vertSource, Shader.LoadFromFile("Assets/Shaders/smaa_edge.frag"));
            _weightShader = new Shader(gl, vertSource, Shader.LoadFromFile("Assets/Shaders/smaa_weight.frag"));
            _blendShader = new Shader(gl, vertSource, Shader.LoadFromFile("Assets/Shaders/smaa_blend.frag"));

            SetupUniforms();
            SetupQuad();
        }

        OnResize(width, height);
    }

    private void SetupUniforms()
    {
        _edgeShader.Use();
        _edgeShader.SetInt(_edgeShader.GetUniformLocation("uColorTex"), 0);
        _edgeScreenSizeLoc = _edgeShader.GetUniformLocation("uPixelSize");

        _weightShader.Use();
        _weightShader.SetInt(_weightShader.GetUniformLocation("uEdgesTex"), 0);
        _weightShader.SetInt(_weightShader.GetUniformLocation("uAreaTex"), 1);
        _weightShader.SetInt(_weightShader.GetUniformLocation("uSearchTex"), 2);
        _weightScreenSizeLoc = _weightShader.GetUniformLocation("uPixelSize");

        _blendShader.Use();
        _blendShader.SetInt(_blendShader.GetUniformLocation("uColorTex"), 0);
        _blendShader.SetInt(_blendShader.GetUniformLocation("uBlendTex"), 1);
        _blendScreenSizeLoc = _blendShader.GetUniformLocation("uPixelSize");
    }

    private unsafe void SetupQuad()
    {
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

    public void OnResize(uint width, uint height)
    {
        _edgesFbo?.Dispose();
        _blendFbo?.Dispose();

        _edgesFbo = new Framebuffer(gl, width, height, InternalFormat.RG8, PixelFormat.RG, PixelType.UnsignedByte);
        _blendFbo = new Framebuffer(gl, width, height, InternalFormat.Rgba8, PixelFormat.Rgba, PixelType.UnsignedByte);

        var pixelSize = new Vector4(1.0f / width, 1.0f / height, width, height);

        _edgeShader.Use();
        _edgeShader.SetVector4(_edgeScreenSizeLoc, pixelSize);

        _weightShader.Use();
        _weightShader.SetVector4(_weightScreenSizeLoc, pixelSize);

        _blendShader.Use();
        _blendShader.SetVector4(_blendScreenSizeLoc, pixelSize);
    }

    public void Execute()
    {
        gl.Disable(EnableCap.DepthTest);
        gl.Disable(EnableCap.CullFace);

        _edgesFbo.Bind();
        gl.ClearColor(0.0f, 0.0f, 0.0f, 0.0f);
        gl.Clear(ClearBufferMask.ColorBufferBit);

        _edgeShader.Use();
        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, lightingPass.PostProcessFbo.ColorAttachments[0]);

        gl.BindVertexArray(_quadVao);
        gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);

        _blendFbo.Bind();
        gl.ClearColor(0.0f, 0.0f, 0.0f, 0.0f);
        gl.Clear(ClearBufferMask.ColorBufferBit);

        _weightShader.Use();
        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, _edgesFbo.ColorAttachments[0]);
        gl.ActiveTexture(TextureUnit.Texture1);
        _areaTex.Bind(TextureUnit.Texture1);
        gl.ActiveTexture(TextureUnit.Texture2);
        _searchTex.Bind(TextureUnit.Texture2);

        gl.BindVertexArray(_quadVao);
        gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);

        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        _blendShader.Use();
        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, lightingPass.PostProcessFbo.ColorAttachments[0]);
        gl.ActiveTexture(TextureUnit.Texture1);
        gl.BindTexture(TextureTarget.Texture2D, _blendFbo.ColorAttachments[0]);

        gl.BindVertexArray(_quadVao);
        gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);

        gl.Enable(EnableCap.DepthTest);
        gl.Enable(EnableCap.CullFace);
    }

    public void Dispose()
    {
        _edgeShader?.Dispose();
        _weightShader?.Dispose();
        _blendShader?.Dispose();
        _edgesFbo?.Dispose();
        _blendFbo?.Dispose();
        _areaTex?.Dispose();
        _searchTex?.Dispose();
        gl.DeleteVertexArray(_quadVao);
        gl.DeleteBuffer(_quadVbo);
    }
}