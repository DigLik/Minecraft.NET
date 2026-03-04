using Silk.NET.Vulkan;

using Minecraft.NET.Engine.Abstractions.Graphics;

namespace Minecraft.NET.Graphics.Vulkan.Core;

public class MeshAllocation(
    uint indexCount, uint firstIndex, int vertexOffset,
    ulong vertexByteOffset, ulong vertexByteSize,
    ulong indexByteOffset, ulong indexByteSize) : IMesh
{
    public uint IndexCount { get; } = indexCount;
    public uint FirstIndex { get; } = firstIndex;
    public int VertexOffset { get; } = vertexOffset;

    internal ulong VertexByteOffset { get; } = vertexByteOffset;
    internal ulong VertexByteSize { get; } = vertexByteSize;

    internal ulong IndexByteOffset { get; } = indexByteOffset;
    internal ulong IndexByteSize { get; } = indexByteSize;

    public AccelerationStructureKHR Blas;
    public ulong BlasDeviceAddress;

    internal ulong BlasByteOffset { get; set; }
    internal ulong BlasByteSize { get; set; }

    internal DynamicMeshPool.BufferChunk VertexChunk = null!;
    internal DynamicMeshPool.BufferChunk IndexChunk = null!;
    internal DynamicMeshPool.BufferChunk BlasChunk = null!;

    public ulong VertexAddress => VertexChunk.Buffer.DeviceAddress;
    public ulong IndexAddress => IndexChunk.Buffer.DeviceAddress;

    private volatile bool _isReady = false;
    public bool IsReady
    {
        get => _isReady;
        internal set => _isReady = value;
    }

    public void Dispose()
    {
    }
}