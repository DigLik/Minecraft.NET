namespace Minecraft.NET.UI;

public enum LayoutDirection { Row, Column }
public enum Alignment { Start, Center, End, Stretch }

public struct UiStyle()
{
    public Vector4 Color { get; set; } = Vector4.One;
    public Vector4 HoverColor { get; set; } = Vector4.One;

    public float BorderRadius { get; set; } = 0;
    public float BorderWidth { get; set; } = 0;
    public Vector4 BorderColor { get; set; } = Vector4.Zero;

    public float Width { get; set; } = float.NaN;
    public float Height { get; set; } = float.NaN;
    public Vector4 Margin { get; set; }
    public Vector4 Padding { get; set; }

    public LayoutDirection Direction { get; set; } = LayoutDirection.Column;
    public Alignment JustifyContent { get; set; } = Alignment.Start;
    public Alignment AlignItems { get; set; } = Alignment.Start;
    public float Gap { get; set; } = 0;
}