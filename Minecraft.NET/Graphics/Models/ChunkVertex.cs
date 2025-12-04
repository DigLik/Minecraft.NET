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
    public readonly sbyte X = (sbyte)pos.X, Y = (sbyte)pos.Y, Z = (sbyte)pos.Z;

    public readonly byte AO = (byte)(ao * 255);

    public readonly byte TexX = (byte)texIndex.X, TexY = (byte)texIndex.Y;

    public readonly sbyte U = (sbyte)uv.X, V = (sbyte)uv.Y;
    private static readonly VertexAttributeDescriptor[] Layout =
    [
        new(0, 3, VertexAttribPointerType.Byte, false, nameof(X)), 
        new(1, 2, VertexAttribPointerType.Byte, false, nameof(TexX)),
        new(2, 2, VertexAttribPointerType.Byte, false, nameof(U)),
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