using Minecraft.NET.Services;

namespace Minecraft.NET.UI;

public abstract class UiElement
{
    public UiStyle Style = new();
    public UiElement? Parent { get; private set; }
    public List<UiElement> Children { get; } = [];

    public Vector2 ComputedPosition;
    public Vector2 ComputedSize;

    public Action? OnClick;
    public Action<bool>? OnHover;

    public bool IsHovered { get; internal set; }

    public void Add(UiElement child)
    {
        child.Parent = this;
        Children.Add(child);
    }

    public void Add(IEnumerable<UiElement> children)
    {
        foreach (var child in children)
            Add(child);
    }

    public virtual void CalculateLayout(Vector2 availableSpace, FontService fontService)
    {
        float maxChildWidth = 0;
        float maxChildHeight = 0;
        float totalFlexSize = 0;

        foreach (var child in Children)
        {
            child.CalculateLayout(availableSpace, fontService);

            float childTotalW = child.ComputedSize.X + child.Style.Margin.X + child.Style.Margin.Z;
            float childTotalH = child.ComputedSize.Y + child.Style.Margin.Y + child.Style.Margin.W;

            if (Style.Direction == LayoutDirection.Column)
            {
                totalFlexSize += childTotalH;
                maxChildWidth = Math.Max(maxChildWidth, childTotalW);
            }
            else
            {
                totalFlexSize += childTotalW;
                maxChildHeight = Math.Max(maxChildHeight, childTotalH);
            }
        }

        if (Children.Count > 1)
            totalFlexSize += Style.Gap * (Children.Count - 1);

        if (float.IsNaN(Style.Width))
            ComputedSize.X = (Style.Direction == LayoutDirection.Row ? totalFlexSize : maxChildWidth)
                             + Style.Padding.X + Style.Padding.Z;
        else
            ComputedSize.X = Style.Width;

        if (float.IsNaN(Style.Height))
            ComputedSize.Y = (Style.Direction == LayoutDirection.Column ? totalFlexSize : maxChildHeight)
                             + Style.Padding.Y + Style.Padding.W;
        else
            ComputedSize.Y = Style.Height;

        float contentW = ComputedSize.X - Style.Padding.X - Style.Padding.Z;
        float contentH = ComputedSize.Y - Style.Padding.Y - Style.Padding.W;

        float mainAxisAvailable = (Style.Direction == LayoutDirection.Column) ? contentH : contentW;
        float mainAxisUsed = totalFlexSize;
        float startOffset = 0;

        switch (Style.JustifyContent)
        {
            case Alignment.Center:
            startOffset = (mainAxisAvailable - mainAxisUsed) / 2;
            break;
            case Alignment.End:
            startOffset = mainAxisAvailable - mainAxisUsed;
            break;
        }

        float currentMain = startOffset;

        foreach (var child in Children)
        {
            float childW = child.ComputedSize.X;
            float childH = child.ComputedSize.Y;
            float childFullW = childW + child.Style.Margin.X + child.Style.Margin.Z;
            float childFullH = childH + child.Style.Margin.Y + child.Style.Margin.W;

            float offsetX = 0;
            float offsetY = 0;

            if (Style.Direction == LayoutDirection.Column)
            {
                offsetY = currentMain + Style.Padding.Y + child.Style.Margin.Y;
                currentMain += childFullH + Style.Gap;

                switch (Style.AlignItems)
                {
                    case Alignment.Start:
                    offsetX = Style.Padding.X + child.Style.Margin.X;
                    break;
                    case Alignment.Center:
                    offsetX = Style.Padding.X + (contentW - childFullW) / 2 + child.Style.Margin.X;
                    break;
                    case Alignment.End:
                    offsetX = Style.Padding.X + (contentW - childFullW) + child.Style.Margin.X;
                    break;
                    case Alignment.Stretch:
                    offsetX = Style.Padding.X + child.Style.Margin.X;
                    break;
                }
            }
            else
            {
                offsetX = currentMain + Style.Padding.X + child.Style.Margin.X;
                currentMain += childFullW + Style.Gap;

                switch (Style.AlignItems)
                {
                    case Alignment.Start:
                    offsetY = Style.Padding.Y + child.Style.Margin.Y;
                    break;
                    case Alignment.Center:
                    offsetY = Style.Padding.Y + (contentH - childFullH) / 2 + child.Style.Margin.Y;
                    break;
                    case Alignment.End:
                    offsetY = Style.Padding.Y + (contentH - childFullH) + child.Style.Margin.Y;
                    break;
                }
            }

            child.ComputedPosition = new Vector2(
                ComputedPosition.X + offsetX,
                ComputedPosition.Y + offsetY
            );
        }
    }

    public virtual void UpdateInteraction(Vector2 mousePos, bool mouseClicked)
    {
        bool isInside = mousePos.X >= ComputedPosition.X && mousePos.X <= ComputedPosition.X + ComputedSize.X &&
                        mousePos.Y >= ComputedPosition.Y && mousePos.Y <= ComputedPosition.Y + ComputedSize.Y;

        if (isInside != IsHovered)
        {
            IsHovered = isInside;
            OnHover?.Invoke(IsHovered);
        }

        if (isInside && mouseClicked)
            OnClick?.Invoke();

        foreach (var child in Children)
            child.UpdateInteraction(mousePos, mouseClicked);
    }
}