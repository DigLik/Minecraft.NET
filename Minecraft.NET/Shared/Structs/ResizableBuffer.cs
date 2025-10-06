using System.Runtime.CompilerServices;

namespace Minecraft.NET.Shared.Structs;

public unsafe ref struct ResizableBuffer<T> where T : unmanaged
{
    private Span<T> _span;
    private int _count;

    public ResizableBuffer(Span<T> initialBuffer)
    {
        _span = initialBuffer;
        _count = 0;
    }

    public readonly int Count => _count;
    public readonly int Capacity => _span.Length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T item)
    {
        if (_count < _span.Length)
            _span[_count++] = item;
    }

    public readonly Span<T> AsSpan() => _span[.._count];

    public void Clear() => _count = 0;

}