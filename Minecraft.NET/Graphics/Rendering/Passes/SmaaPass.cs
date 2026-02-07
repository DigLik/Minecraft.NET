namespace Minecraft.NET.Graphics.Rendering.Passes;

public class SmaaPass(IGlContextAccessor glAccessor, RenderResources resources) : IRenderPass
{
    public int Priority => 2000;
    public string Name => "SMAA";
    public GL Gl => glAccessor.Gl;

    private Shader _edgeShader = null!;
    private Shader _weightShader = null!;
    private Shader _blendShader = null!;

    private Texture _areaTex = null!;
    private Texture _searchTex = null!;

    private uint _quadVao, _quadVbo;

    private int _edgeScreenSizeLoc;
    private int _weightScreenSizeLoc;
    private int _blendScreenSizeLoc;

    public unsafe void Initialize(uint width, uint height)
    {
        if (_areaTex == null)
        {
            _areaTex = new Texture(Gl, "Assets/Textures/SMAA/AreaTexDX10.png");
            _searchTex = new Texture(Gl, "Assets/Textures/SMAA/SearchTex.png");

            _areaTex.Bind();
            Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
            Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
            Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
            Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

            _searchTex.Bind();
            Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
            Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
            Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
            Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        }

        if (_edgeShader == null)
        {
            var vertSource = Shader.LoadFromFile("Assets/Shaders/smaa.vert");
            _edgeShader = new Shader(Gl, vertSource, Shader.LoadFromFile("Assets/Shaders/smaa_edge.frag"));
            _weightShader = new Shader(Gl, vertSource, Shader.LoadFromFile("Assets/Shaders/smaa_weight.frag"));
            _blendShader = new Shader(Gl, vertSource, Shader.LoadFromFile("Assets/Shaders/smaa_blend.frag"));

            SetupUniforms();
            SetupQuad();
        }
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

    public void OnResize(uint width, uint height)
    {
        resources.SmaaEdges?.Dispose();
        resources.SmaaBlend?.Dispose();

        resources.SmaaEdges = new Framebuffer(Gl, width, height, InternalFormat.RG8, PixelFormat.RG, PixelType.UnsignedByte);
        resources.SmaaBlend = new Framebuffer(Gl, width, height, InternalFormat.Rgba8, PixelFormat.Rgba, PixelType.UnsignedByte);

        var pixelSize = new Vector4(1.0f / width, 1.0f / height, width, height);

        _edgeShader.Use();
        _edgeShader.SetVector4(_edgeScreenSizeLoc, pixelSize);

        _weightShader.Use();
        _weightShader.SetVector4(_weightScreenSizeLoc, pixelSize);

        _blendShader.Use();
        _blendShader.SetVector4(_blendScreenSizeLoc, pixelSize);
    }

    public void Execute(RenderResources renderResources)
    {
        var inputFbo = renderResources.PostProcessFbo;
        var edgesFbo = renderResources.SmaaEdges;
        var blendFbo = renderResources.SmaaBlend;

        if (inputFbo == null || edgesFbo == null || blendFbo == null)
            return;

        Gl.Disable(EnableCap.DepthTest);
        Gl.Disable(EnableCap.CullFace);

        edgesFbo.Bind();
        Gl.ClearColor(0.0f, 0.0f, 0.0f, 0.0f);
        Gl.Clear(ClearBufferMask.ColorBufferBit);

        _edgeShader.Use();
        Gl.ActiveTexture(TextureUnit.Texture0);
        Gl.BindTexture(TextureTarget.Texture2D, inputFbo.ColorAttachments[0]);

        Gl.BindVertexArray(_quadVao);
        Gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);

        blendFbo.Bind();
        Gl.ClearColor(0.0f, 0.0f, 0.0f, 0.0f);
        Gl.Clear(ClearBufferMask.ColorBufferBit);

        _weightShader.Use();
        Gl.ActiveTexture(TextureUnit.Texture0);
        Gl.BindTexture(TextureTarget.Texture2D, edgesFbo.ColorAttachments[0]);
        Gl.ActiveTexture(TextureUnit.Texture1);
        _areaTex.Bind(TextureUnit.Texture1);
        Gl.ActiveTexture(TextureUnit.Texture2);
        _searchTex.Bind(TextureUnit.Texture2);

        Gl.BindVertexArray(_quadVao);
        Gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);

        Gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        Gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        _blendShader.Use();
        Gl.ActiveTexture(TextureUnit.Texture0);
        Gl.BindTexture(TextureTarget.Texture2D, inputFbo.ColorAttachments[0]);
        Gl.ActiveTexture(TextureUnit.Texture1);
        Gl.BindTexture(TextureTarget.Texture2D, blendFbo.ColorAttachments[0]);

        Gl.BindVertexArray(_quadVao);
        Gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);

        Gl.Enable(EnableCap.DepthTest);
        Gl.Enable(EnableCap.CullFace);
    }

    public void Dispose()
    {
        _edgeShader?.Dispose();
        _weightShader?.Dispose();
        _blendShader?.Dispose();
        _areaTex?.Dispose();
        _searchTex?.Dispose();
        Gl.DeleteVertexArray(_quadVao);
        Gl.DeleteBuffer(_quadVbo);
    }
}