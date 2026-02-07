using Minecraft.NET.Services;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Minecraft.NET.UI.Elements;

public class Label : UiElement, IDisposable
{
    private char[] _textBuffer;
    private int _textLength;
    private const int DefaultCapacity = 64;
    private bool _isDisposed;

    public float FontSize { get; set; } = 24.0f;

    public ReadOnlySpan<char> Text => new(_textBuffer, 0, _textLength);
    public bool IsEmpty => _textLength == 0;

    public Label(string initialText = "", int capacity = DefaultCapacity)
    {
        _textBuffer = ArrayPool<char>.Shared.Rent(capacity);
        _ = Append(initialText);

        Style.Color = Vector4.One;
        Style.Width = float.NaN;
        Style.Height = float.NaN;
    }

    [SuppressMessage("Performance", "CA1822:Пометьте члены как статические", Justification = "<Ожидание>")]
    [SuppressMessage("Style", "IDE0060:Удалите неиспользуемый параметр", Justification = "<Ожидание>")]
    public void SetText([InterpolatedStringHandlerArgument("")] ref LabelInterpolatedStringHandler handler) { }

    public Label Clear()
    {
        _textLength = 0;
        return this;
    }

    public Label Append(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
            return this;
        EnsureCapacity(_textLength + text.Length);
        text.CopyTo(_textBuffer.AsSpan(_textLength));
        _textLength += text.Length;
        return this;
    }

    public Label Append(string? text) => Append(text.AsSpan());

    public Label Append<T>(T value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
        where T : ISpanFormattable
    {
        EnsureCapacity(_textLength + 32);

        int charsWritten;
        while (!value.TryFormat(_textBuffer.AsSpan(_textLength), out charsWritten, format, provider))
            EnsureCapacity(_textBuffer.Length * 2);

        _textLength += charsWritten;
        return this;
    }

    private void EnsureCapacity(int requiredSize)
    {
        if (_textBuffer.Length >= requiredSize)
            return;

        int newSize = Math.Max(_textBuffer.Length * 2, requiredSize);
        char[] newBuffer = ArrayPool<char>.Shared.Rent(newSize);

        if (_textLength > 0)
            Array.Copy(_textBuffer, 0, newBuffer, 0, _textLength);

        ArrayPool<char>.Shared.Return(_textBuffer);
        _textBuffer = newBuffer;
    }

    public override void CalculateLayout(Vector2 availableSpace, FontService fontService)
    {
        if (_textLength > 0)
        {
            float scale = FontSize / 24.0f;
            float measuredWidth = fontService.MeasureText(Text) * scale;

            Style.Width = measuredWidth;
            Style.Height = FontSize;
        }
        else
        {
            Style.Width = 0;
            Style.Height = 0;
        }

        base.CalculateLayout(availableSpace, fontService);
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;
        if (_textBuffer != null)
        {
            ArrayPool<char>.Shared.Return(_textBuffer);
            _textBuffer = null!;
        }

        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    ~Label() => Dispose();

    [InterpolatedStringHandler]
    public readonly ref struct LabelInterpolatedStringHandler
    {
        private readonly Label _label;

        public LabelInterpolatedStringHandler(int literalLength, int formattedCount, Label label)
        {
            _label = label;
            _ = _label.Clear();
            _label.EnsureCapacity(literalLength + formattedCount * 12);
        }

        public void AppendLiteral(string s) => _label.Append(s.AsSpan());

        public void AppendFormatted<T>(T value) where T : ISpanFormattable
            => _label.Append(value);

        public void AppendFormatted<T>(T value, string? format) where T : ISpanFormattable
            => _label.Append(value, format.AsSpan());

        public void AppendFormatted(string? value) => _label.Append(value.AsSpan());

        public void AppendFormatted(ReadOnlySpan<char> value) => _label.Append(value);
    }
}