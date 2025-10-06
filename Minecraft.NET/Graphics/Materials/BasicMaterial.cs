using Minecraft.NET.Graphics.Shaders;

namespace Minecraft.NET.Graphics.Materials;

public sealed class BasicMaterial(BasicShader shader) : Material(shader)
{
    public Texture? Texture { get; set; }

    public override void Apply(Matrix4x4 model, Matrix4x4 view, Matrix4x4 projection)
    {
        Shader.Use();

        if (Texture is not null)
        {
            Texture.Bind(Silk.NET.OpenGL.TextureUnit.Texture0);
            shader.SetTextureUnit(0);
        }

        shader.SetModelMatrix(model);
        shader.SetViewMatrix(view);
        shader.SetProjectionMatrix(projection);
    }
}