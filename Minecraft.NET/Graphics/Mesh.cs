using System.Runtime.InteropServices;

namespace Minecraft.NET.Graphics;

public sealed class Mesh(MeshData meshData) : IDisposable
{
    private uint _vao, _vbo, _ebo;
    private bool _isUploaded;
    private GL _gl = null!;

    public unsafe void UploadToGpu(GL gl)
    {
        if (_isUploaded) return;
        _gl = gl;

        _vao = _gl.GenVertexArray();
        _gl.BindVertexArray(_vao);

        _vbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(meshData.VertexCount * sizeof(float)), meshData.Vertices, BufferUsageARB.StaticDraw);

        _ebo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(meshData.IndexCount * sizeof(uint)), meshData.Indices, BufferUsageARB.StaticDraw);

        const int stride = 6 * sizeof(float);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(2, 1, VertexAttribPointerType.Float, false, stride, (void*)(5 * sizeof(float)));
        _gl.EnableVertexAttribArray(2);

        _gl.BindVertexArray(0);
        _isUploaded = true;

        meshData.Dispose();
    }

    public void Bind() => _gl.BindVertexArray(_vao);
    public int IndexCount => meshData.IndexCount;

    public void Dispose()
    {
        if (_isUploaded)
        {
            _gl.DeleteVertexArray(_vao);
            _gl.DeleteBuffer(_vbo);
            _gl.DeleteBuffer(_ebo);
        }

        meshData.Dispose();
    }
}

public unsafe sealed class MeshData(float* vertices, int vertexCount, uint* indices, int indexCount) : IDisposable
{
    public float* Vertices { get; } = vertices;
    public uint* Indices { get; } = indices;
    public int VertexCount { get; } = vertexCount;
    public int IndexCount { get; } = indexCount;

    private bool _isDisposed = false;

    public void Dispose()
    {
        if (_isDisposed) return;

        if (Vertices != null) NativeMemory.Free(Vertices);
        if (Indices != null) NativeMemory.Free(Indices);

        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}