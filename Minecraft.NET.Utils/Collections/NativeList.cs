using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Minecraft.NET.Utils.Collections;

public unsafe struct NativeList<T> : IDisposable where T : unmanaged
{
    private T* _buffer;
    private int _capacity;
    private int _count;

    public readonly int Count => _count;
    public readonly int Capacity => _capacity;
    public readonly bool IsCreated => _buffer != null;

    public readonly T* Data => _buffer;

    public NativeList(int initialCapacity = 8)
    {
        if (initialCapacity <= 0) initialCapacity = 8;
        _capacity = initialCapacity;
        _count = 0;
        _buffer = (T*)NativeMemory.Alloc((nuint)(_capacity * sizeof(T)));
    }

    public ref T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)index >= (uint)_count) throw new IndexOutOfRangeException();
            return ref _buffer[index];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T item)
    {
        if (_count == _capacity) Grow();
        _buffer[_count++] = item;
    }

    public void RemoveAtSwapBack(int index)
    {
        if ((uint)index >= (uint)_count) throw new IndexOutOfRangeException();
        _buffer[index] = _buffer[_count - 1];
        _count--;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Grow()
    {
        _capacity *= 2;
        _buffer = (T*)NativeMemory.Realloc(_buffer, (nuint)(_capacity * sizeof(T)));
    }

    public void Resize(int newSize, T defaultValue = default)
    {
        if (newSize > _capacity)
        {
            _capacity = newSize;
            _buffer = (T*)NativeMemory.Realloc(_buffer, (nuint)(_capacity * sizeof(T)));
        }
        if (newSize > _count)
        {
            for (int i = _count; i < newSize; i++)
                _buffer[i] = defaultValue;
        }
        _count = newSize;
    }

    public void Clear() => _count = 0;

    public void Dispose()
    {
        if (_buffer != null)
        {
            NativeMemory.Free(_buffer);
            _buffer = null;
        }
        _capacity = 0;
        _count = 0;
    }
}