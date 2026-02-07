using Minecraft.NET.Services;
using Minecraft.NET.UI;

namespace Minecraft.NET.Graphics.Rendering.Passes;

public class UiRenderPass(GL gl, UiContext uiContext, FontService fontService) : IRenderPass
{
    private UiRenderer _renderer = null!;

    public void Initialize(uint width, uint height)
    {
        _renderer = new UiRenderer(gl, fontService);
        OnResize(width, height);
    }

    public void OnResize(uint width, uint height)
        => uiContext.OnResize(new Vector2(width, height));

    public void Execute()
        => uiContext.Render(_renderer);

    public void Dispose()
        => _renderer?.Dispose();
}