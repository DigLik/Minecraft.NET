using System.Numerics;
using System.Runtime.InteropServices;

namespace Minecraft.NET.Shared.Structs;

public sealed unsafe class LoopedArray<T> : IDisposable
    where T : unmanaged, INumber<T>
{
    #region Fields
    private readonly T* _buffer;
    private readonly bool _isCapacityPowerOfTwo;
    private bool _disposed = false;
    private T _sum = T.Zero;
    private readonly Lock _lock = new();
    private uint _nextWriteIndex = 0;
    #endregion

    #region Properties
    public uint Capacity { get; }
    public uint Count { get; private set; } = 0;
    public T Average
    {
        get
        {
            lock (_lock)
            {
                return Count == 0 ? T.Zero : _sum / T.CreateChecked(Count);
            }
        }
    }
    #endregion

    public LoopedArray(uint capacity)
    {
        ArgumentOutOfRangeException.ThrowIfZero(capacity);
        Capacity = capacity;
        _isCapacityPowerOfTwo = capacity != 0 && (capacity & (capacity - 1)) == 0;
        _buffer = (T*)NativeMemory.Alloc(capacity, (uint)sizeof(T));
    }

    #region Metods
    public void Add(T item)
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (Count == Capacity) _sum -= _buffer[_nextWriteIndex];

            _buffer[_nextWriteIndex] = item;
            _sum += item;

            _nextWriteIndex = _isCapacityPowerOfTwo
                ? (_nextWriteIndex + 1) & (Capacity - 1)
                : (_nextWriteIndex + 1) % Capacity;

            if (Count < Capacity) Count++;
        }
    }
    public T AverageOfLast(uint count)
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (Count == 0 || count == 0) return T.Zero;
            if (Count <= count) return Average;

            if (Count - count > count)
            {
                T excludedSum = T.Zero;
                for (uint i = count; i < Count; i++)
                    excludedSum += GetItemFromEnd(i);

                return (_sum - excludedSum) / T.CreateChecked(count);
            }
            else
            {
                T sum = T.Zero;
                for (uint i = 0; i < count; i++)
                    sum += GetItemFromEnd(i);

                return sum / T.CreateChecked(count);
            }
        }
    }
    private T GetItemFromEnd(uint reverseIndex)
    {
        uint index = _isCapacityPowerOfTwo
            ? (_nextWriteIndex + Capacity - 1 - reverseIndex) & (Capacity - 1)
            : (_nextWriteIndex + Capacity - 1 - reverseIndex) % Capacity;
        return _buffer[index];
    }
    #endregion

    #region Disposable
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            NativeMemory.Free(_buffer);
        }
        GC.SuppressFinalize(this);
    }
    ~LoopedArray() => Dispose();
    #endregion
}