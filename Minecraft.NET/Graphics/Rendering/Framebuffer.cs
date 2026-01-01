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
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba16f, width, height, 0, PixelFormat.Rgba, PixelType.Float, null);
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
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.DepthComponent24, width, height, 0, PixelFormat.DepthComponent, PixelType.Float, null);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, DepthAttachment, 0);

        if (_gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
            throw new Exception("GBuffer Framebuffer is not complete!");

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    public Framebuffer(GL gl, uint width, uint height, bool singleChannel)
    {
        _gl = gl;
        Fbo = _gl.GenFramebuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, Fbo);

        uint colorBuffer = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, colorBuffer);

        var internalFormat = singleChannel ? InternalFormat.R8 : InternalFormat.Rgba;
        var format = singleChannel ? PixelFormat.Red : PixelFormat.Rgba;
        var type = singleChannel ? PixelType.Float : PixelType.UnsignedByte;

        _gl.TexImage2D(TextureTarget.Texture2D, 0, internalFormat, width, height, 0, format, type, null);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, colorBuffer, 0);

        ColorAttachments = [colorBuffer];
        DepthAttachment = 0; // Нет глубины

        if (_gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
            throw new Exception("PP Framebuffer is not complete!");

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    public void Bind() => _gl.BindFramebuffer(FramebufferTarget.Framebuffer, Fbo);
    public void Unbind() => _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

    public void Dispose()
    {
        _gl.DeleteFramebuffer(Fbo);
        _gl.DeleteTextures((uint)ColorAttachments.Length, ColorAttachments);
        if (DepthAttachment != 0) _gl.DeleteTextures(1, in DepthAttachment);
    }
}