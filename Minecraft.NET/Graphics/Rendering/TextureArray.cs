using StbImageSharp;

namespace Minecraft.NET.Graphics.Rendering;

public sealed class TextureArray : IDisposable
{
    private readonly GL _gl;
    public readonly uint Handle;

    public unsafe TextureArray(GL gl, List<string> filePaths)
    {
        _gl = gl;
        Handle = _gl.GenTexture();
        Bind();

        if (filePaths.Count == 0)
            throw new ArgumentException("No files provided for TextureArray");

        using var firstStream = File.OpenRead(filePaths[0]);
        var firstImage = ImageResult.FromStream(firstStream, ColorComponents.RedGreenBlueAlpha);
        uint width = (uint)firstImage.Width;
        uint height = (uint)firstImage.Height;
        uint layers = (uint)filePaths.Count;
        uint mipLevels = (uint)Math.Floor(Math.Log2(Math.Max(width, height))) + 1;

        _gl.TexStorage3D(TextureTarget.Texture2DArray, mipLevels, SizedInternalFormat.Rgba8, width, height, layers);

        for (int i = 0; i < layers; i++)
        {
            using var stream = File.OpenRead(filePaths[i]);
            var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

            if (image.Width != width || image.Height != height)
                throw new Exception($"Texture {filePaths[i]} has incorrect size. All textures in array must be same size.");

            fixed (byte* ptr = image.Data)
            {
                _gl.TexSubImage3D(
                    TextureTarget.Texture2DArray, 0,
                    0, 0, i,
                    width, height, 1,
                    PixelFormat.Rgba,
                    PixelType.UnsignedByte,
                    ptr
                );
            }
        }

        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)GLEnum.NearestMipmapLinear);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMaxAnisotropy, 16.0f);

        _gl.GenerateMipmap(TextureTarget.Texture2DArray);
    }

    public void Bind(TextureUnit unit = TextureUnit.Texture0)
    {
        _gl.ActiveTexture(unit);
        _gl.BindTexture(TextureTarget.Texture2DArray, Handle);
    }

    public void Dispose() => _gl.DeleteTexture(Handle);
}