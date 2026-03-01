using System.Numerics;

using Minecraft.NET.Engine.Abstractions.Graphics;
using Minecraft.NET.Utils.Math;

namespace Minecraft.NET.Engine.Abstractions;

public interface IRenderPipeline : IDisposable
{
    void Initialize(VertexElement[] layout, uint stride);

    IMesh CreateMesh<T>(T[] vertices, uint[] indices) where T : unmanaged;
    void DeleteMesh(IMesh mesh);

    ITextureArray CreateTextureArray(int width, int height, byte[][] pixels);

    void BindTextureArray(ITextureArray textureArray);
    void SubmitDraw(IMesh mesh, Vector3<float> position);
    void ClearDraws();
    void RenderFrame(Matrix4x4 viewProjection);
    void OnFramebufferResize(Vector2<int> newSize);
}