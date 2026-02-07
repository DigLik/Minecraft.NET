namespace Minecraft.NET.UI.Elements;

public class Button : UiElement
{
    public Button()
    {
        Style.Color = new Vector4(0.3f, 0.3f, 0.3f, 1.0f);
        Style.HoverColor = new Vector4(0.4f, 0.4f, 0.5f, 1.0f);
        Style.Padding = new Vector4(10);
    }
}