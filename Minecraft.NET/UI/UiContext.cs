using Minecraft.NET.Engine;
using Minecraft.NET.Services;

namespace Minecraft.NET.UI;

public class UiContext(FontService fontService)
{
    public UiElement Root { get; private set; } = new Elements.Panel();
    private Vector2 _viewportSize;

    public void SetRoot(UiElement root) => Root = root;

    public void OnResize(Vector2 size) => _viewportSize = size;

    public void Update(IInputManager inputManager)
    {
        Root.ComputedPosition = Vector2.Zero;
        Root.Style.Width = _viewportSize.X;
        Root.Style.Height = _viewportSize.Y;
        Root.CalculateLayout(_viewportSize, fontService);
        Root.UpdateInteraction(inputManager.MousePosition, inputManager.IsMouseButtonPressed(MouseButton.Left));
    }

    public void Render(UiRenderer renderer)
    {
        renderer.Begin(_viewportSize);
        renderer.DrawElementRecursive(Root);
        renderer.End();
    }
}