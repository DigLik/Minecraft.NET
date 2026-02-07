namespace Minecraft.NET.UI.Elements;

public class Stack : Panel
{
    public Stack(LayoutDirection direction = LayoutDirection.Column, float gap = 5)
    {
        Style.Direction = direction;
        Style.Gap = gap;
        Style.Color = Vector4.Zero;
        Style.Width = float.NaN;
        Style.Height = float.NaN;
    }
}