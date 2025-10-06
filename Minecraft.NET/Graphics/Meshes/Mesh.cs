using Silk.NET.OpenGL;

namespace Minecraft.NET.Graphics.Meshes;

public sealed class Mesh : IDisposable
{
    private readonly GL _gl;
    private readonly VertexBufferObject<float> _vbo;
    private readonly IndexBufferObject _ibo;
    private readonly VertexArrayObject<float> _vao;
    private readonly uint _indexCount;

    public unsafe Mesh(GL gl, float[] vertices, uint[] indices)
    {
        _gl = gl;
        _indexCount = (uint)indices.Length;

        _vbo = new VertexBufferObject<float>(gl, vertices, BufferTargetARB.ArrayBuffer);
        _vao = new VertexArrayObject<float>(gl, _vbo);
        _ibo = new IndexBufferObject(gl, indices);

        _vao.SetVertexAttributePointer(0, 3, VertexAttribPointerType.Float, 3 * sizeof(float), 0);
    }

    public void Bind()
    {
        _vao.Bind();
    }

    public unsafe void Draw()
    {
        Bind();
        _gl.DrawElements(PrimitiveType.Triangles, _indexCount, DrawElementsType.UnsignedInt, null);
    }

    public void Dispose()
    {
        _vao.Dispose();
        _ibo.Dispose();
        _vbo.Dispose();
    }
}