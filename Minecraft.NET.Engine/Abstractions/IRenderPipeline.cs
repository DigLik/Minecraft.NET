using System.Numerics;

using Minecraft.NET.Engine.Abstractions.Graphics;
using Minecraft.NET.Utils.Collections;
using Minecraft.NET.Utils.Math;

namespace Minecraft.NET.Engine.Abstractions;

public interface IRenderPipeline : IDisposable
{
    void Initialize(VertexElement[] layout, uint stride);
    IMesh CreateMesh<T>(NativeList<T> vertices, NativeList<uint> indices) where T : unmanaged;
    void DeleteMesh(IMesh mesh);
    ITextureArray CreateTextureArray(int width, int height, byte[][] pixels);
    void BindTextureArray(ITextureArray textureArray);
    void SubmitDraw(IMesh mesh, Vector3 position);
    void ClearDraws();
    void RenderFrame(CameraData cameraData);
    void OnFramebufferResize(Vector2Int newSize);
}