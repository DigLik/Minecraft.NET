using Silk.NET.OpenGL;
using StbImageSharp;

namespace Minecraft.NET.Graphics;

public sealed class Texture : IDisposable
{
    private readonly GL _gl;
    public uint Handle { get; }

    public unsafe Texture(GL gl, string path)
    {
        _gl = gl;
        Handle = _gl.GenTexture();
        Bind();

        using var stream = File.OpenRead(path);
        ImageResult image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

        _gl.TexImage2D<byte>(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)image.Width, (uint)image.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, image.Data);

        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.LinearMipmapLinear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);

        float maxAnisotropy = _gl.GetFloat((GetPName)GLEnum.MaxTextureMaxAnisotropy);
        _gl.TexParameter(TextureTarget.Texture2D, (TextureParameterName)GLEnum.TextureMaxAnisotropy, maxAnisotropy);

        _gl.GenerateMipmap(TextureTarget.Texture2D);
    }

    public void Bind(TextureUnit unit = TextureUnit.Texture0)
    {
        _gl.ActiveTexture(unit);
        _gl.BindTexture(TextureTarget.Texture2D, Handle);
    }

    public void Dispose()
    {
        _gl.DeleteTexture(Handle);
    }
}