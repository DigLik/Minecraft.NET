using Minecraft.NET.Character;
using Minecraft.NET.Core.Common;
using Minecraft.NET.Graphics;
using Minecraft.NET.Graphics.Rendering;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Minecraft.NET.Services;

public class VisibleScene
{
    public DrawElementsIndirectCommand[] IndirectCommands { get; }
        = new DrawElementsIndirectCommand[MaxVisibleSections];
    public Vector3[] ChunkOffsets { get; } = new Vector3[MaxVisibleSections];

    public int VisibleSectionCount;

    public void Reset() => VisibleSectionCount = 0;
}

public unsafe class SceneCuller(Player player, ChunkManager chunkProvider)
{
    private readonly Frustum _frustum = new();
    private static readonly float SectionSphereRadius = MathF.Sqrt(3) * (ChunkSize / 2f);
    private static readonly Vector3 SectionExtent = new(ChunkSize / 2f);

    private Vector256<float> _negSphereRadiusVec;
    private static readonly Matrix4x4 _identityMatrix = Matrix4x4.Identity;

    public VisibleScene Result { get; } = new();

    public void Cull(in Matrix4x4 projectionMatrix, in Matrix4x4 relativeViewMatrix)
    {
        _frustum.Update(relativeViewMatrix * projectionMatrix);
        _negSphereRadiusVec = Vector256.Create(-SectionSphereRadius);
        Result.Reset();

        var cameraOrigin = new Vector3d(
            Math.Floor(player.Position.X),
            Math.Floor(player.Position.Y),
            Math.Floor(player.Position.Z)
        );
        float camX = (float)cameraOrigin.X;
        float camY = (float)cameraOrigin.Y;
        float camZ = (float)cameraOrigin.Z;

        var chunks = chunkProvider.RenderChunks;
        int chunkCount = chunks.Count;
        if (chunkCount == 0) return;

        var yInc1 = _frustum.YIncrement;
        var yInc2 = yInc1 + yInc1;
        var yInc3 = yInc2 + yInc1;
        var yInc4 = yInc2 + yInc2;

        var sectionProjectedRadius = _frustum.SectionExtentProjection;
        var zero = Vector256<float>.Zero;

        int globalCount = 0;
        int maxCount = MaxVisibleSections;

        var chunksSpan = CollectionsMarshal.AsSpan(chunks);

        fixed (DrawElementsIndirectCommand* pCommandsBase = Result.IndirectCommands)
        fixed (Vector3* pOffsetsBase = Result.ChunkOffsets)
        {
            DrawElementsIndirectCommand* ptrCommands = pCommandsBase;
            Vector3* ptrOffsets = pOffsetsBase;

            float colCenterY = (float)((WorldHeightInBlocks / 2.0) - VerticalChunkOffset * ChunkSize) - camY;
            float startSectionRelY = ((0 - VerticalChunkOffset) * ChunkSize) - camY + SectionExtent.Y;

            for (int i = 0; i < chunkCount; i++)
            {
                var column = chunksSpan[i];

                if (column.ActiveMask == 0) continue;

                float colCenterX = (column.Position.X * ChunkSize) - camX + SectionExtent.X;
                float colCenterZ = (column.Position.Y * ChunkSize) - camZ + SectionExtent.Z;

                if (!_frustum.IntersectsColumn(colCenterX, colCenterY, colCenterZ))
                    continue;

                var dist0 = _frustum.GetDistances(colCenterX, startSectionRelY, colCenterZ);
                var dist1 = dist0 + yInc1;
                var dist2 = dist0 + yInc2;
                var dist3 = dist0 + yInc3;

                ref var geometriesRef = ref MemoryMarshal.GetArrayDataReference(column.MeshGeometries);

                for (int y = 0; y < WorldHeightInChunks; y++)
                {
                    float sectionRelY = startSectionRelY + (y * ChunkSize);
                    var dist = _frustum.GetDistances(colCenterX, sectionRelY, colCenterZ);

                    ref var geometry = ref Unsafe.Add(ref geometriesRef, y);

                    if (geometry.IndexCount == 0) continue;

                    var sphereFail = Vector256.LessThan(dist, _negSphereRadiusVec);
                    var boxFail = Vector256.LessThan(dist + sectionProjectedRadius, zero);

                    if (Vector256.ExtractMostSignificantBits(Vector256.BitwiseOr(sphereFail, boxFail)) != 0)
                        continue;

                    if (globalCount >= maxCount) break;

                    ptrCommands[globalCount] = new DrawElementsIndirectCommand(
                        Count: geometry.IndexCount,
                        InstanceCount: 1,
                        FirstIndex: geometry.FirstIndex,
                        BaseVertex: geometry.BaseVertex,
                        BaseInstance: (uint)globalCount
                    );

                    float x = colCenterX - SectionExtent.X;
                    float z = colCenterZ - SectionExtent.Z;
                    float py = sectionRelY - SectionExtent.Y;

                    ptrOffsets[globalCount] = new Vector3(x, py, z);

                    globalCount++;
                }
            }
        }
        Result.VisibleSectionCount = globalCount;
    }
}