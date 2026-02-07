using Minecraft.NET.Character;
using Minecraft.NET.Core.Chunks;
using Minecraft.NET.Graphics.Rendering;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Minecraft.NET.Services;

public class VisibleScene
{
    public uint IndirectBufferHandle;
    public uint InstanceBufferHandle;
    public uint CountBufferHandle;
    public int MaxPossibleCount;
}

public unsafe class SceneCuller(Player player, ChunkManager chunkManager, IGlContextAccessor glAccessor) : IDisposable
{
    [StructLayout(LayoutKind.Sequential, Pack = 16)]
    private struct ChunkInputGPU
    {
        public Vector4 Position;
        public Vector4 Center;
        public uint IndexCount;
        public uint FirstIndex;
        public int BaseVertex;
        public uint Padding;
    }

    private GL Gl => glAccessor.Gl;
    private Shader _cullShader = null!;

    private uint _inputBuffer;
    private uint _indirectBuffer;
    private uint _instanceBuffer;
    private uint _atomicCounterBuffer;

    private ChunkInputGPU[] _cpuInputBuffer = [];
    private readonly int _maxCapacity = MaxVisibleSections;
    private readonly Vector4[] _frustumPlanes = new Vector4[6];
    private long _lastStateHash = -1;
    private int _cachedTotalSections = 0;

    public VisibleScene Result { get; } = new();

    private const int CommandSize = 20;
    private const int InstanceSize = 16;

    public void Initialize()
    {
        InitializeBuffers();
        InitializeShader();
    }

    private void InitializeShader()
    {
        _cullShader = new Shader(Gl, Shader.LoadFromFile("Assets/Shaders/cull.comp"));
        _cullShader.Use();
        _cullShader.SetInt(_cullShader.GetUniformLocation("visibleCount"), 0);
    }

    private void InitializeBuffers()
    {
        _inputBuffer = Gl.GenBuffer();
        Gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, _inputBuffer);
        Gl.BufferData(BufferTargetARB.ShaderStorageBuffer, (nuint)(_maxCapacity * sizeof(ChunkInputGPU)), null, BufferUsageARB.DynamicDraw);

        _indirectBuffer = Gl.GenBuffer();
        Gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, _indirectBuffer);
        Gl.BufferData(BufferTargetARB.ShaderStorageBuffer, (nuint)(_maxCapacity * CommandSize), null, BufferUsageARB.DynamicCopy);

        _instanceBuffer = Gl.GenBuffer();
        Gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, _instanceBuffer);
        Gl.BufferData(BufferTargetARB.ShaderStorageBuffer, (nuint)(_maxCapacity * InstanceSize), null, BufferUsageARB.DynamicCopy);

        _atomicCounterBuffer = Gl.GenBuffer();
        Gl.BindBuffer(BufferTargetARB.AtomicCounterBuffer, _atomicCounterBuffer);
        Gl.BufferData(BufferTargetARB.AtomicCounterBuffer, sizeof(uint), null, BufferUsageARB.DynamicDraw);

        Gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, 0);
        Gl.BindBuffer(BufferTargetARB.AtomicCounterBuffer, 0);

        Result.IndirectBufferHandle = _indirectBuffer;
        Result.InstanceBufferHandle = _instanceBuffer;
        Result.CountBufferHandle = _atomicCounterBuffer;
        Result.MaxPossibleCount = _maxCapacity;
    }

    public void Cull(in Matrix4x4 projectionMatrix, in Matrix4x4 relativeViewMatrix)
    {
        if (Gl == null)
            return;

        _cullShader.Use();

        UpdateFrustumPlanes(relativeViewMatrix * projectionMatrix);
        _cullShader.SetVector4Array(_cullShader.GetUniformLocation("u_frustumPlanes"), _frustumPlanes);

        var camPos = player.Position;
        float camX = (float)Math.Floor(camPos.X);
        float camY = (float)Math.Floor(camPos.Y);
        float camZ = (float)Math.Floor(camPos.Z);
        _cullShader.SetVector3(_cullShader.GetUniformLocation("u_viewPos"), new Vector3(camX, camY, camZ));

        var chunks = chunkManager.GetRenderChunks();
        UpdateInputBufferIfNeeded(chunks);

        if (_cachedTotalSections == 0)
        {
            Gl.BindBuffer(BufferTargetARB.AtomicCounterBuffer, _atomicCounterBuffer);
            uint zero = 0;
            Gl.BufferSubData(BufferTargetARB.AtomicCounterBuffer, 0, sizeof(uint), &zero);
            Gl.BindBuffer(BufferTargetARB.AtomicCounterBuffer, 0);
            return;
        }

        Gl.BindBuffer(BufferTargetARB.AtomicCounterBuffer, _atomicCounterBuffer);
        uint zeroVal = 0;
        Gl.BufferSubData(BufferTargetARB.AtomicCounterBuffer, 0, sizeof(uint), &zeroVal);

        Gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 0, _inputBuffer);
        Gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 1, _indirectBuffer);
        Gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 2, _instanceBuffer);
        Gl.BindBufferBase(BufferTargetARB.AtomicCounterBuffer, 3, _atomicCounterBuffer);

        _cullShader.SetUInt(_cullShader.GetUniformLocation("u_chunkCount"), (uint)_cachedTotalSections);

        uint groupSize = 64;
        uint numGroups = ((uint)_cachedTotalSections + groupSize - 1) / groupSize;
        _cullShader.Dispatch(numGroups, 1, 1);

        Gl.MemoryBarrier(MemoryBarrierMask.CommandBarrierBit | MemoryBarrierMask.VertexAttribArrayBarrierBit | MemoryBarrierMask.ClientMappedBufferBarrierBit);
    }

    private void UpdateInputBufferIfNeeded(IReadOnlyList<ChunkColumn> columns)
    {
        long currentHash = 0;
        int colCount = columns.Count;
        for (int i = 0; i < colCount; i++)
            currentHash = currentHash * 31 + (columns[i].Version ^ columns[i].Position.GetHashCode());

        if (currentHash == _lastStateHash)
            return;

        _lastStateHash = currentHash;
        int count = 0;

        if (_cpuInputBuffer.Length < MaxVisibleSections)
            Array.Resize(ref _cpuInputBuffer, MaxVisibleSections);

        float sectionRadius = 13.856f;
        float halfSize = 8.0f;

        for (int i = 0; i < colCount; i++)
        {
            var column = columns[i];
            if (column.ActiveMask == 0)
                continue;

            float colX = column.Position.X * ChunkSize;
            float colZ = column.Position.Y * ChunkSize;

            var meshGeometries = column.MeshGeometries;
            int activeMask = column.ActiveMask;

            for (int y = 0; y < WorldHeightInChunks; y++)
            {
                if (((activeMask >> y) & 1) == 0)
                    continue;

                ref readonly var geometry = ref meshGeometries[y];
                if (geometry.IndexCount == 0)
                    continue;

                if (count >= _maxCapacity)
                    goto EndLoop;

                float colY = y * ChunkSize - VerticalChunkOffset * ChunkSize;

                ref var input = ref _cpuInputBuffer[count];
                input.Position.X = colX;
                input.Position.Y = colY;
                input.Position.Z = colZ;
                input.Position.W = 0;

                input.Center.X = colX + halfSize;
                input.Center.Y = colY + halfSize;
                input.Center.Z = colZ + halfSize;
                input.Center.W = sectionRadius;

                input.IndexCount = geometry.IndexCount;
                input.FirstIndex = geometry.FirstIndex;
                input.BaseVertex = geometry.BaseVertex;

                count++;
            }
        }

EndLoop:
        _cachedTotalSections = count;

        Gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, _inputBuffer);
        fixed (ChunkInputGPU* ptr = _cpuInputBuffer)
            Gl.BufferSubData(BufferTargetARB.ShaderStorageBuffer, 0, (nuint)(count * sizeof(ChunkInputGPU)), ptr);
    }

    private void UpdateFrustumPlanes(Matrix4x4 vp)
    {
        float m11 = vp.M11, m12 = vp.M12, m13 = vp.M13, m14 = vp.M14;
        float m21 = vp.M21, m22 = vp.M22, m23 = vp.M23, m24 = vp.M24;
        float m31 = vp.M31, m32 = vp.M32, m33 = vp.M33, m34 = vp.M34;
        float m41 = vp.M41, m42 = vp.M42, m43 = vp.M43, m44 = vp.M44;

        _frustumPlanes[0] = NormalizePlane(m14 + m11, m24 + m21, m34 + m31, m44 + m41);
        _frustumPlanes[1] = NormalizePlane(m14 - m11, m24 - m21, m34 - m31, m44 - m41);
        _frustumPlanes[2] = NormalizePlane(m14 + m12, m24 + m22, m34 + m32, m44 + m42);
        _frustumPlanes[3] = NormalizePlane(m14 - m12, m24 - m22, m34 - m32, m44 - m42);
        _frustumPlanes[4] = NormalizePlane(m14 + m13, m24 + m23, m34 + m33, m44 + m43);
        _frustumPlanes[5] = NormalizePlane(m14 - m13, m24 - m23, m34 - m33, m44 - m43);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector4 NormalizePlane(float x, float y, float z, float w)
    {
        float len = MathF.Sqrt(x * x + y * y + z * z);
        float invLen = 1.0f / len;
        return new Vector4(x * invLen, y * invLen, z * invLen, w * invLen);
    }

    public void Dispose()
    {
        if (Gl == null)
            return;

        _cullShader?.Dispose();
        Gl.DeleteBuffer(_inputBuffer);
        Gl.DeleteBuffer(_indirectBuffer);
        Gl.DeleteBuffer(_instanceBuffer);
        Gl.DeleteBuffer(_atomicCounterBuffer);
    }
}