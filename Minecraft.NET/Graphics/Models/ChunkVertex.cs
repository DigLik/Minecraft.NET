using System.Runtime.InteropServices;

namespace Minecraft.NET.Graphics.Models;

internal readonly record struct VertexAttributeDescriptor(
    uint Location,
    int ComponentCount,
    VertexAttribPointerType Type,
    bool Normalized,
    string FieldName
);

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly unsafe struct ChunkVertex(Vector3 pos, Vector2 texIndex, Vector2 uv, float ao)
{
    public readonly byte X = (byte)pos.X, Y = (byte)pos.Y, Z = (byte)pos.Z;
    public readonly byte AO = (byte)(ao * 255);
    public readonly byte TexX = (byte)texIndex.X, TexY = (byte)texIndex.Y;
    public readonly byte U = (byte)uv.X, V = (byte)uv.Y;

    private static readonly VertexAttributeDescriptor[] Layout =
    [
        new(0, 3, VertexAttribPointerType.UnsignedByte, false, nameof(X)),
        new(1, 2, VertexAttribPointerType.UnsignedByte, false, nameof(TexX)),
        new(2, 2, VertexAttribPointerType.UnsignedByte, false, nameof(U)),
        new(3, 1, VertexAttribPointerType.UnsignedByte, true, nameof(AO)),
    ];

    public static readonly uint Stride = 8;

    public static unsafe void SetVertexAttribPointers(GL gl)
    {
        foreach (var attribute in Layout)
        {
            gl.EnableVertexAttribArray(attribute.Location);
            gl.VertexAttribPointer(
                index: attribute.Location,
                size: attribute.ComponentCount,
                type: attribute.Type,
                normalized: attribute.Normalized,
                stride: Stride,
                pointer: (void*)Marshal.OffsetOf<ChunkVertex>(attribute.FieldName)
            );
        }
    }
}