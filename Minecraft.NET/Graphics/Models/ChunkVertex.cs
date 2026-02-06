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
public readonly unsafe struct ChunkVertex(Vector3 pos, int texIndex, Vector2 uv, float ao)
{
    public readonly byte X = (byte)pos.X, Y = (byte)pos.Y, Z = (byte)pos.Z;
    public readonly byte AO = (byte)(ao * 255);

    public readonly ushort TexIndex = (ushort)texIndex;

    public readonly byte U = (byte)uv.X;
    public readonly byte V = (byte)uv.Y;

    private static readonly VertexAttributeDescriptor[] Layout =
    [
        new(0, 3, VertexAttribPointerType.UnsignedByte, false, nameof(X)),
        new(1, 1, VertexAttribPointerType.UnsignedShort, false, nameof(TexIndex)),
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