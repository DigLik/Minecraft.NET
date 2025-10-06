using Silk.NET.OpenGL;

namespace Minecraft.NET.Graphics.Meshes;

public sealed class IndexBufferObject : IDisposable
{
    private readonly GL _gl;
    public uint Handle { get; }

    public IndexBufferObject(GL gl, ReadOnlySpan<uint> indices)
    {
        _gl = gl;
        Handle = _gl.GenBuffer();
        Bind();

        _gl.BufferData(BufferTargetARB.ElementArrayBuffer, indices, BufferUsageARB.StaticDraw);
    }

    public void Bind()
    {
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, Handle);
    }

    public void Dispose()
    {
        _gl.DeleteBuffer(Handle);
    }
}