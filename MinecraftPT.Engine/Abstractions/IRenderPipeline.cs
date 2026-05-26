using System.Numerics;

using MinecraftPT.Engine.Abstractions.Graphics;
using MinecraftPT.Utils.Collections;
using MinecraftPT.Utils.Math;

namespace MinecraftPT.Engine.Abstractions;

public interface IRenderPipeline : IDisposable
{
    void Initialize(VertexElement[] layout, uint stride);
    IMesh CreateMesh<T>(NativeList<T> vertices, NativeList<ushort> indices, uint opaqueIndexCount = 0) where T : unmanaged;
    void DeleteMesh(IMesh mesh);
    ITextureArray CreateTextureArray(int width, int height, byte[][] pixels);
    void BindTextureArray(ITextureArray textureArray);
    void BindMaterials(MaterialData[] materials);
    void SubmitDraw(IMesh mesh, Vector3 position);
    void ClearDraws();
    void RenderFrame(CameraData cameraData);
    void OnFramebufferResize(Vector2Int newSize);
    void StartFrame();
    bool GetPredictedCamera(out Matrix4x4 view, out Matrix4x4 proj);
    void SetSimulationStart();
    void SetSimulationEnd();
}