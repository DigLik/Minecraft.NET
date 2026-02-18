using Minecraft.NET.Graphics.Rendering;
using StbTrueTypeSharp;

namespace Minecraft.NET.Services;

public unsafe class FontService(IGlContextAccessor glAccessor) : IDisposable
{
    private const string DefaultFontPath = "Assets/Fonts/CascadiaCode-VariableFont_wght.ttf";

    private GL Gl => glAccessor.Gl;
    private uint _textureHandle;
    private const int BitmapWidth = 512;
    private const int BitmapHeight = 512;
    private const float FontSize = 24.0f;
    private readonly StbTrueType.stbtt_packedchar[] _charData = new StbTrueType.stbtt_packedchar[96];
    private bool _isInitialized;

    public void Initialize()
    {
        if (_isInitialized)
            return;
        LoadFont(DefaultFontPath);
        _isInitialized = true;
    }

    private void LoadFont(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Font not found", path);

        byte[] ttfData = File.ReadAllBytes(path);
        byte[] bitmap = new byte[BitmapWidth * BitmapHeight];

        fixed (byte* ttfPtr = ttfData)
        fixed (byte* bitmapPtr = bitmap)
        fixed (StbTrueType.stbtt_packedchar* charPtr = _charData)
        {
            var context = new StbTrueType.stbtt_pack_context();
            if (StbTrueType.stbtt_PackBegin(context, bitmapPtr, BitmapWidth, BitmapHeight, 0, 1, null) == 0)
                throw new Exception("Failed to initialize font packer");

            StbTrueType.stbtt_PackSetOversampling(context, 2, 2);

            if (StbTrueType.stbtt_PackFontRange(context, ttfPtr, 0, FontSize, 32, 96, charPtr) == 0)
                Console.WriteLine("[ERROR] Font packing failed! Use a STATIC TTF font (e.g. Arial.ttf).");

            StbTrueType.stbtt_PackEnd(context);
        }

        _textureHandle = Gl!.GenTexture();
        Gl.BindTexture(TextureTarget.Texture2D, _textureHandle);

        Gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
        Gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.R8, BitmapWidth, BitmapHeight, 0, PixelFormat.Red, PixelType.UnsignedByte, bitmap);

        Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 0);

        Gl.PixelStore(PixelStoreParameter.UnpackAlignment, 4);
    }

    public StbTrueType.stbtt_aligned_quad GetQuad(char c, ref float x, ref float y)
    {
        if (!_isInitialized)
            return default;
        if (c is < (char)32 or > (char)126)
            c = '?';

        StbTrueType.stbtt_aligned_quad q = new();
        fixed (StbTrueType.stbtt_packedchar* ptr = _charData)
        {
            fixed (float* px = &x)
            fixed (float* py = &y)
                StbTrueType.stbtt_GetPackedQuad(ptr, BitmapWidth, BitmapHeight, c - 32, px, py, &q, 0);
        }
        return q;
    }

    public float MeasureText(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
            return 0;
        float x = 0, y = 0;
        for (int i = 0; i < text.Length; i++)
            GetQuad(text[i], ref x, ref y);
        return x;
    }

    public void Bind(TextureUnit unit = TextureUnit.Texture0)
    {
        if (!_isInitialized)
            return;
        Gl!.ActiveTexture(unit);
        Gl.BindTexture(TextureTarget.Texture2D, _textureHandle);
    }

    public void Dispose() { if (_isInitialized) Gl?.DeleteTexture(_textureHandle); }
}