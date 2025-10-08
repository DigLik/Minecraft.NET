namespace Minecraft.NET.Graphics;

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

        uint gPosition = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, gPosition);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba16f, width, height, 0, PixelFormat.Rgba, PixelType.Float, null);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, gPosition, 0);

        uint gNormal = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, gNormal);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba16f, width, height, 0, PixelFormat.Rgba, PixelType.Float, null);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, TextureTarget.Texture2D, gNormal, 0);

        uint gAlbedo = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, gAlbedo);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment2, TextureTarget.Texture2D, gAlbedo, 0);

        ColorAttachments = [gPosition, gNormal, gAlbedo];

        var attachments = new[] { DrawBufferMode.ColorAttachment0, DrawBufferMode.ColorAttachment1, DrawBufferMode.ColorAttachment2 };
        _gl.DrawBuffers((uint)attachments.Length, attachments);

        DepthAttachment = _gl.GenRenderbuffer();
        _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, DepthAttachment);
        _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.DepthComponent, width, height);
        _gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, DepthAttachment);

        if (_gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
            throw new Exception("Framebuffer is not complete!");

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    public Framebuffer(GL gl, uint width, uint height, bool singleChannel)
    {
        _gl = gl;

        Fbo = _gl.GenFramebuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, Fbo);

        uint ssaoColorBuffer = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, ssaoColorBuffer);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, singleChannel ? InternalFormat.R8 : InternalFormat.Rgba, width, height, 0, PixelFormat.Red, PixelType.Float, null);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, ssaoColorBuffer, 0);

        ColorAttachments = [ssaoColorBuffer];
        DepthAttachment = 0;

        if (_gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
            throw new Exception("SSAO Framebuffer is not complete!");

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    public void Bind() => _gl.BindFramebuffer(FramebufferTarget.Framebuffer, Fbo);

    public void Unbind() => _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

    public void Dispose()
    {
        _gl.DeleteFramebuffer(Fbo);
        _gl.DeleteTextures((uint)ColorAttachments.Length, ColorAttachments);
        if (DepthAttachment != 0)
        {
            _gl.DeleteRenderbuffer(DepthAttachment);
        }
    }
}