using Silk.NET.OpenGL;

namespace Minecraft.NET.Graphics.Meshes;

public sealed class VertexArrayObject<TVertex> : IDisposable where TVertex : unmanaged
{
    private readonly GL _gl;
    public uint Handle { get; }

    public VertexArrayObject(GL gl, VertexBufferObject<TVertex> vbo)
    {
        _gl = gl;
        Handle = _gl.GenVertexArray();
        Bind();

        vbo.Bind();
    }

    public unsafe void SetVertexAttributePointer(uint index, int count, VertexAttribPointerType type, uint stride, int offset)
    {
        _gl.EnableVertexAttribArray(index);
        _gl.VertexAttribPointer(index, count, type, false, stride, (void*)offset);
    }

    public void Bind()
    {
        _gl.BindVertexArray(Handle);
    }

    public void Dispose()
    {
        _gl.DeleteVertexArray(Handle);
    }
}