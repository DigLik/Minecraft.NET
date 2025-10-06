using Minecraft.NET.Graphics.Materials;
using Minecraft.NET.Graphics.Meshes;

namespace Minecraft.NET.Graphics.Scene;

public sealed class RenderObject(Mesh mesh, Material material, Transform transform)
{
    public readonly Mesh Mesh = mesh;
    public readonly Material Material = material;
    public readonly Transform Transform = transform;

    public void Update(float deltaTime)
    {
        var rotationAmount = Quaternion.CreateFromAxisAngle(
            Vector3.Normalize(new Vector3(0.5f, 1.0f, 0.0f)),
            float.DegreesToRadians(25.0f * deltaTime)
        );

        Transform.Rotation *= rotationAmount;
    }
}