using Silk.NET.OpenGL;

namespace Minecraft.NET.Graphics.Meshes;

public sealed class VertexBufferObject<TData> : IDisposable where TData : unmanaged
{
    private readonly GL _gl;
    public uint Handle { get; }

    public VertexBufferObject(GL gl, ReadOnlySpan<TData> data, BufferTargetARB bufferType)
    {
        _gl = gl;
        Handle = _gl.GenBuffer();
        Bind();

        _gl.BufferData(bufferType, data, BufferUsageARB.StaticDraw);
    }

    public void Bind()
    {
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, Handle);
    }

    public void Dispose()
    {
        _gl.DeleteBuffer(Handle);
    }
}