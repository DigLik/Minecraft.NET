namespace Minecraft.NET.Graphics.Rendering;

public sealed unsafe class Framebuffer : IDisposable
{
    private readonly GL _gl;
    public readonly uint Fbo;
    public readonly uint[] ColorAttachments;
    public readonly uint DepthAttachment;

    public Framebuffer(GL gl, uint width, uint height)
    {
        _gl = gl;
        Fbo = _gl.GenFramebuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, Fbo);

        uint gNormal = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, gNormal);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgb10A2, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedInt2101010Rev, null);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, gNormal, 0);

        uint gAlbedo = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, gAlbedo);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, TextureTarget.Texture2D, gAlbedo, 0);

        ColorAttachments = [gNormal, gAlbedo];
        var attachments = new[] { DrawBufferMode.ColorAttachment0, DrawBufferMode.ColorAttachment1 };
        _gl.DrawBuffers((uint)attachments.Length, attachments);

        DepthAttachment = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, DepthAttachment);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.DepthComponent32f, width, height, 0, PixelFormat.DepthComponent, PixelType.Float, null);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, DepthAttachment, 0);

        if (_gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
            throw new Exception("GBuffer Framebuffer is not complete!");

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    public Framebuffer(GL gl, uint width, uint height, InternalFormat internalFormat, PixelFormat format, PixelType type)
    {
        _gl = gl;
        Fbo = _gl.GenFramebuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, Fbo);

        uint colorBuffer = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, colorBuffer);

        _gl.TexImage2D(TextureTarget.Texture2D, 0, internalFormat, width, height, 0, format, type, null);

        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);

        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, colorBuffer, 0);

        ColorAttachments = [colorBuffer];
        DepthAttachment = 0;

        if (_gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
            throw new Exception($"Framebuffer (Format: {internalFormat}) is not complete!");

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    public Framebuffer(GL gl, uint width, uint height, bool singleChannel)
        : this(gl, width, height,
              singleChannel ? InternalFormat.R8 : InternalFormat.Rgba,
              singleChannel ? PixelFormat.Red : PixelFormat.Rgba,
              singleChannel ? PixelType.Float : PixelType.UnsignedByte)
    {
    }

    public void Bind() => _gl.BindFramebuffer(FramebufferTarget.Framebuffer, Fbo);
    public void Unbind() => _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

    public void Dispose()
    {
        _gl.DeleteFramebuffer(Fbo);
        _gl.DeleteTextures((uint)ColorAttachments.Length, ColorAttachments);

        if (DepthAttachment != 0)
            _gl.DeleteTextures(1, in DepthAttachment);
    }
}