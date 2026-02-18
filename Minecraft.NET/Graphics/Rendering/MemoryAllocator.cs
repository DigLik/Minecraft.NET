namespace Minecraft.NET.Graphics.Rendering;

public class MemoryAllocator
{
    private struct FreeBlock(nuint offset, nuint size) : IComparable<FreeBlock>
    {
        public nuint Offset = offset;
        public nuint Size = size;

        public readonly int CompareTo(FreeBlock other) => Offset.CompareTo(other.Offset);
    }

    private readonly List<FreeBlock> _freeList = [];
    private readonly Dictionary<nuint, nuint> _allocations = [];
    public nuint Capacity { get; private set; }

    public MemoryAllocator(nuint initialCapacity)
    {
        Capacity = initialCapacity;
        _freeList.Add(new FreeBlock(0, Capacity));
    }

    public bool TryAllocate(nuint size, out nuint offset)
    {
        if (size == 0)
        {
            offset = 0;
            return false;
        }

        int count = _freeList.Count;
        for (int i = 0; i < count; i++)
        {
            var block = _freeList[i];
            if (block.Size >= size)
            {
                offset = block.Offset;
                _allocations[offset] = size;

                if (block.Size == size)
                {
                    _freeList.RemoveAt(i);
                }
                else
                {
                    var newBlock = new FreeBlock(block.Offset + size, block.Size - size);
                    _freeList[i] = newBlock;
                }
                return true;
            }
        }

        offset = 0;
        return false;
    }

    public void Free(nuint offset)
    {
        if (!_allocations.Remove(offset, out nuint size))
            return;

        var newBlock = new FreeBlock(offset, size);
        int index = _freeList.BinarySearch(newBlock);

        if (index < 0)
            index = ~index;

        _freeList.Insert(index, newBlock);
        Coalesce(index);
    }

    private void Coalesce(int index)
    {
        if (index + 1 < _freeList.Count)
        {
            var current = _freeList[index];
            var next = _freeList[index + 1];

            if (current.Offset + current.Size == next.Offset)
            {
                var merged = new FreeBlock(current.Offset, current.Size + next.Size);
                _freeList[index] = merged;
                _freeList.RemoveAt(index + 1);
            }
        }

        if (index > 0)
        {
            var prev = _freeList[index - 1];
            var current = _freeList[index];

            if (prev.Offset + prev.Size == current.Offset)
            {
                var merged = new FreeBlock(prev.Offset, prev.Size + current.Size);
                _freeList[index - 1] = merged;
                _freeList.RemoveAt(index);
            }
        }
    }

    public void Grow(nuint newCapacity)
    {
        if (newCapacity <= Capacity)
            return;

        nuint addedSize = newCapacity - Capacity;
        nuint oldCapacity = Capacity;
        Capacity = newCapacity;

        var newBlock = new FreeBlock(oldCapacity, addedSize);
        int index = _freeList.Count;
        _freeList.Add(newBlock);
        Coalesce(index);
    }
}