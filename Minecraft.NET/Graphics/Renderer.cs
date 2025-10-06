using Minecraft.NET.Graphics.Scene;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace Minecraft.NET.Graphics;

public sealed class Renderer : IDisposable
{
    private GL _gl = null!;
    private Camera _camera = null!;
    private Vector2D<int> _viewportSize;

    public void Load(IWindow window, Camera camera)
    {
        _gl = window.CreateOpenGL();
        _camera = camera;
        _viewportSize = window.FramebufferSize;

        _gl.ClearColor(System.Drawing.Color.Black);
        _gl.Enable(EnableCap.DepthTest);
    }

    public void Render(IReadOnlyList<IRenderable> renderables)
    {
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        var view = _camera.GetViewMatrix();
        var projection = _camera.GetProjectionMatrix((float)_viewportSize.X / _viewportSize.Y);

        foreach (var renderable in renderables)
        {
            renderable.Material.Apply(renderable.Transform.GetModelMatrix(), view, projection);
            renderable.Mesh.Draw();
        }
    }

    public void OnResize(Vector2D<int> newSize)
    {
        _gl.Viewport(newSize);
        _viewportSize = newSize;
    }

    public void Dispose() => _gl?.Dispose();
}