using Minecraft.NET.Graphics.Shaders;

namespace Minecraft.NET.Graphics.Materials;

public sealed class BasicMaterial(BasicShader shader) : Material(shader)
{
    public override void Apply(Matrix4x4 model, Matrix4x4 view, Matrix4x4 projection)
    {
        Shader.Use();
        shader.SetModelMatrix(model);
        shader.SetViewMatrix(view);
        shader.SetProjectionMatrix(projection);
    }
}