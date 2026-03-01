namespace Minecraft.NET.Engine.Abstractions.Graphics;

public enum VertexFormat
{
    Float,
    Float2,
    Float3,
    Float4,
    Int,
    UInt
}

public struct VertexElement(uint location, VertexFormat format, uint offset)
{
    public uint Location = location;
    public VertexFormat Format = format;
    public uint Offset = offset;
}