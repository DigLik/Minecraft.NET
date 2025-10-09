namespace Minecraft.NET.Graphics.Models;

public sealed class Mesh(MeshData meshData) : IDisposable
{
    private uint _vao, _vbo, _ebo;
    private bool _isUploaded;
    private GL _gl = null!;

    public unsafe void UploadToGpu(GL gl, uint instanceVbo)
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

        const int stride = 7 * sizeof(float);

        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        _gl.EnableVertexAttribArray(0);

        _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
        _gl.EnableVertexAttribArray(1);

        _gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, (void*)(5 * sizeof(float)));
        _gl.EnableVertexAttribArray(2);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, instanceVbo);

        nuint matrixSize = (nuint)sizeof(Matrix4x4);
        _gl.EnableVertexAttribArray(3);
        _gl.VertexAttribPointer(3, 4, VertexAttribPointerType.Float, false, (uint)matrixSize, 0);
        _gl.EnableVertexAttribArray(4);
        _gl.VertexAttribPointer(4, 4, VertexAttribPointerType.Float, false, (uint)matrixSize, sizeof(Vector4));
        _gl.EnableVertexAttribArray(5);
        _gl.VertexAttribPointer(5, 4, VertexAttribPointerType.Float, false, (uint)matrixSize, (nint)(2 * sizeof(Vector4)));
        _gl.EnableVertexAttribArray(6);
        _gl.VertexAttribPointer(6, 4, VertexAttribPointerType.Float, false, (uint)matrixSize, (nint)(3 * sizeof(Vector4)));

        _gl.VertexAttribDivisor(3, 1);
        _gl.VertexAttribDivisor(4, 1);
        _gl.VertexAttribDivisor(5, 1);
        _gl.VertexAttribDivisor(6, 1);

        _gl.BindVertexArray(0);
        _isUploaded = true;

        meshData.Dispose();
    }

    public void Bind() => _gl.BindVertexArray(_vao);
    public int IndexCount => meshData.IndexCount;

    public void DisposeGlResources()
    {
        if (_isUploaded)
        {
            _gl.DeleteVertexArray(_vao);
            _gl.DeleteBuffer(_vbo);
            _gl.DeleteBuffer(_ebo);
            _isUploaded = false;
        }
    }

    public void Dispose()
    {
        meshData.Dispose();
        GC.SuppressFinalize(this);
    }
}