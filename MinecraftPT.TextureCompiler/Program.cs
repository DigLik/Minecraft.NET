using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

using ZstdSharp;

if (args.Length < 2)
{
    Console.WriteLine("Usage: TextureCompiler <input.png> <output.ztex>");
    return;
}

string inputPath = args[0];
string outputPath = args[1];

using var image = Image.Load<Rgba32>(inputPath);
byte[] rawPixelData = new byte[image.Width * image.Height * 4];
image.CopyPixelDataTo(rawPixelData);

using var compressor = new Compressor();
Span<byte> compressedData = compressor.Wrap(rawPixelData);

File.WriteAllBytes(outputPath, compressedData);