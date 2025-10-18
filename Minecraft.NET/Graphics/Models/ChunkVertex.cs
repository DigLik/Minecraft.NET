using System.Runtime.InteropServices;

namespace Minecraft.NET.Graphics.Models;

internal readonly record struct VertexAttributeDescriptor(
    uint Location,
    int ComponentCount,
    VertexAttribPointerType Type,
    string FieldName
);

[StructLayout(LayoutKind.Sequential)]
public readonly unsafe struct ChunkVertex(Vector3 position, Vector2 texIndex, Vector2 uv)
{
    private readonly Half Px = (Half)position.X;
    private readonly Half Py = (Half)position.Y;
    private readonly Half Pz = (Half)position.Z;

    private readonly Half TexX = (Half)texIndex.X;
    private readonly Half TexY = (Half)texIndex.Y;

    private readonly Half U = (Half)uv.X;
    private readonly Half V = (Half)uv.Y;

    private static readonly VertexAttributeDescriptor[] Layout =
    [
        new(Location: 0, ComponentCount: 3, Type: VertexAttribPointerType.HalfFloat, FieldName: nameof(Px)),
        new(Location: 1, ComponentCount: 2, Type: VertexAttribPointerType.HalfFloat, FieldName: nameof(TexX)),
        new(Location: 2, ComponentCount: 2, Type: VertexAttribPointerType.HalfFloat, FieldName: nameof(U)),
    ];

    public static readonly uint Stride = (uint)sizeof(ChunkVertex);

    public static unsafe void SetVertexAttribPointers(GL gl)
    {
        foreach (var attribute in Layout)
        {
            gl.EnableVertexAttribArray(attribute.Location);
            gl.VertexAttribPointer(
                index: attribute.Location,
                size: attribute.ComponentCount,
                type: attribute.Type,
                normalized: false,
                stride: Stride,
                pointer: (void*)Marshal.OffsetOf<ChunkVertex>(attribute.FieldName)
            );
        }
    }
}