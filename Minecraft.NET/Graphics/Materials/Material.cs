using Minecraft.NET.Graphics.Shaders;

namespace Minecraft.NET.Graphics.Materials;

public abstract class Material(Shader shader)
{
    public Shader Shader { get; } = shader;

    public abstract void Apply(Matrix4x4 model, Matrix4x4 view, Matrix4x4 projection);
}