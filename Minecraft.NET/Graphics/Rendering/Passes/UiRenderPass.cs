using Minecraft.NET.Services;
using Minecraft.NET.UI;

namespace Minecraft.NET.Graphics.Rendering.Passes;

public class UiRenderPass(IGlContextAccessor glAccessor, UiContext uiContext, FontService fontService) : IRenderPass
{
    public int Priority => 5000;
    public string Name => "User Interface";
    public GL Gl => glAccessor.Gl;

    private UiRenderer _renderer = null!;

    public void Initialize(uint width, uint height)
    {
        _renderer = new UiRenderer(Gl, fontService);
        if (width > 0 && height > 0)
            OnResize(width, height);
    }

    public void OnResize(uint width, uint height)
        => uiContext.OnResize(new Vector2(width, height));

    public void Execute(RenderResources resources)
        => uiContext.Render(_renderer);

    public void Dispose()
        => _renderer?.Dispose();
}