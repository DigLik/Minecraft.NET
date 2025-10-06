using Minecraft.NET.Graphics.Materials;
using Minecraft.NET.Graphics.Meshes;
using Minecraft.NET.Graphics.Scene;

namespace Minecraft.NET.GameObjects;

public sealed class SpinningCube(Mesh mesh, Material material, Transform transform) : IRenderable, IUpdateable
{
    public Mesh Mesh { get; } = mesh;
    public Material Material { get; } = material;
    public Transform Transform { get; } = transform;

    public void Update(float deltaTime)
    {
        var rotationAmount = Quaternion.CreateFromAxisAngle(
            Vector3.Normalize(new Vector3(0.5f, 1.0f, 0.2f)),
            float.DegreesToRadians(50.0f * deltaTime)
        );

        Transform.Rotation *= rotationAmount;
    }
}