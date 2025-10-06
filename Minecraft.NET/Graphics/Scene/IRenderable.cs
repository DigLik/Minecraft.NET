using Minecraft.NET.Graphics.Materials;
using Minecraft.NET.Graphics.Meshes;

namespace Minecraft.NET.Graphics.Scene;

public interface IRenderable
{
    Mesh Mesh { get; }
    Material Material { get; }
    Transform Transform { get; }
}