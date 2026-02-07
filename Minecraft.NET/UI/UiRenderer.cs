using System.Runtime.InteropServices;
using Minecraft.NET.Services;
using Minecraft.NET.UI.Elements;

namespace Minecraft.NET.UI;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct UiVertex
{
    public Vector2 Pos;
    public Vector2 UV;
    public Vector4 Color;
    public float Type;
    public Vector2 Size;
    public float Radius;
}

public unsafe class UiRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly Shader _shader;
    private readonly FontService _fontService;
    private readonly uint _vao, _vbo, _ebo;

    private const int MaxVertices = 16384;
    private const int MaxIndices = 24576;

    private readonly UiVertex[] _vertices = new UiVertex[MaxVertices];
    private readonly ushort[] _indices = new ushort[MaxIndices];
    private int _vertexCount;
    private int _indexCount;

    public UiRenderer(GL gl, FontService fontService)
    {
        _gl = gl;
        _fontService = fontService;
        _shader = new Shader(gl, Shader.LoadFromFile("Assets/Shaders/ui.vert"), Shader.LoadFromFile("Assets/Shaders/ui.frag"));

        _vao = _gl.GenVertexArray();
        _vbo = _gl.GenBuffer();
        _ebo = _gl.GenBuffer();

        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(MaxVertices * sizeof(UiVertex)), null, BufferUsageARB.DynamicDraw);

        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(MaxIndices * sizeof(ushort)), null, BufferUsageARB.DynamicDraw);

        uint stride = (uint)sizeof(UiVertex);

        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, (void*)0);
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, (void*)sizeof(Vector2));
        _gl.EnableVertexAttribArray(2);
        _gl.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, stride, (void*)(2 * sizeof(Vector2)));
        _gl.EnableVertexAttribArray(3);
        _gl.VertexAttribPointer(3, 1, VertexAttribPointerType.Float, false, stride, (void*)(2 * sizeof(Vector2) + sizeof(Vector4)));
        _gl.EnableVertexAttribArray(4);
        _gl.VertexAttribPointer(4, 2, VertexAttribPointerType.Float, false, stride, (void*)(2 * sizeof(Vector2) + sizeof(Vector4) + sizeof(float)));
        _gl.EnableVertexAttribArray(5);
        _gl.VertexAttribPointer(5, 1, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(Vector2) + sizeof(Vector4) + sizeof(float)));

        _gl.BindVertexArray(0);
    }

    public void Begin(Vector2 viewportSize)
    {
        _vertexCount = 0;
        _indexCount = 0;

        _shader.Use();
        var projection = Matrix4x4.CreateOrthographicOffCenter(0, viewportSize.X, viewportSize.Y, 0, -1, 1);
        _shader.SetMatrix4x4(_shader.GetUniformLocation("uProjection"), projection);

        _shader.SetInt(_shader.GetUniformLocation("uFontTexture"), 0);
        _fontService.Bind(TextureUnit.Texture0);

        _gl.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.CullFace);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
    }

    public void DrawElementRecursive(UiElement element)
    {
        if (element.Style.Color.W > 0 && element is not Label)
        {
            var color = element.IsHovered ? element.Style.HoverColor : element.Style.Color;
            PushRect(element.ComputedPosition, element.ComputedSize, Vector2.Zero, Vector2.One, color, 0.0f, element.Style.BorderRadius);
        }

        if (element is Label label && !label.IsEmpty)
            DrawText(label);

        foreach (var child in element.Children)
            DrawElementRecursive(child);
    }

    private void DrawText(Label label)
    {
        float scale = label.FontSize / 24.0f;
        float startX = label.ComputedPosition.X + label.Style.Padding.X;
        float startY = label.ComputedPosition.Y + label.Style.Padding.Y + label.FontSize * 0.8f;

        float currentX = 0;
        float currentY = 0;

        ReadOnlySpan<char> textSpan = label.Text;
        for (int i = 0; i < textSpan.Length; i++)
        {
            char c = textSpan[i];
            var q = _fontService.GetQuad(c, ref currentX, ref currentY);

            float scaledX0 = q.x0 * scale;
            float scaledY0 = q.y0 * scale;
            float scaledX1 = q.x1 * scale;
            float scaledY1 = q.y1 * scale;

            Vector2 posMin = new Vector2(startX + scaledX0, startY + scaledY0);
            Vector2 size = new Vector2(scaledX1 - scaledX0, scaledY1 - scaledY0);

            PushRect(posMin, size, new Vector2(q.s0, q.t0), new Vector2(q.s1, q.t1), label.Style.Color, 1.0f);
        }
    }

    private void PushRect(Vector2 pos, Vector2 size, Vector2 uvMin, Vector2 uvMax, Vector4 color, float type, float radius = 0)
    {
        if (_vertexCount + 4 >= MaxVertices || _indexCount + 6 >= MaxIndices)
            return;

        ushort baseIdx = (ushort)_vertexCount;

        Vector2 uv0 = type < 0.5f ? new(0, 0) : uvMin;
        Vector2 uv1 = type < 0.5f ? new(1, 0) : new(uvMax.X, uvMin.Y);
        Vector2 uv2 = type < 0.5f ? new(1, 1) : uvMax;
        Vector2 uv3 = type < 0.5f ? new(0, 1) : new(uvMin.X, uvMax.Y);

        _vertices[_vertexCount++] = new UiVertex
        { Pos = pos, UV = uv0, Color = color, Type = type, Size = size, Radius = radius };
        _vertices[_vertexCount++] = new UiVertex
        { Pos = new(pos.X + size.X, pos.Y), UV = uv1, Color = color, Type = type, Size = size, Radius = radius };
        _vertices[_vertexCount++] = new UiVertex
        { Pos = new(pos.X + size.X, pos.Y + size.Y), UV = uv2, Color = color, Type = type, Size = size, Radius = radius };
        _vertices[_vertexCount++] = new UiVertex
        { Pos = new(pos.X, pos.Y + size.Y), UV = uv3, Color = color, Type = type, Size = size, Radius = radius };

        _indices[_indexCount++] = baseIdx;
        _indices[_indexCount++] = (ushort)(baseIdx + 1);
        _indices[_indexCount++] = (ushort)(baseIdx + 2);
        _indices[_indexCount++] = baseIdx;
        _indices[_indexCount++] = (ushort)(baseIdx + 2);
        _indices[_indexCount++] = (ushort)(baseIdx + 3);
    }

    public void End()
    {
        if (_indexCount > 0)
        {
            _gl.BindVertexArray(_vao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            fixed (void* p = _vertices)
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(_vertexCount * sizeof(UiVertex)), p);
            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
            fixed (void* p = _indices)
                _gl.BufferSubData(BufferTargetARB.ElementArrayBuffer, 0, (nuint)(_indexCount * sizeof(ushort)), p);

            _gl.DrawElements(PrimitiveType.Triangles, (uint)_indexCount, DrawElementsType.UnsignedShort, (void*)0);
        }

        _gl.Enable(EnableCap.DepthTest);
        _gl.Enable(EnableCap.CullFace);
        _gl.Disable(EnableCap.Blend);
    }

    public void Dispose()
    {
        _shader.Dispose();
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
    }
}