namespace Minecraft.NET.Graphics.Rendering;

public class MemoryAllocator
{
    private class FreeBlock(nuint offset, nuint size)
    {
        public nuint Offset = offset;
        public nuint Size = size;
    }

    private readonly LinkedList<FreeBlock> _freeList = new();
    private readonly Dictionary<nuint, nuint> _allocations = [];
    public nuint Capacity { get; private set; }

    public MemoryAllocator(nuint initialCapacity)
    {
        Capacity = initialCapacity;
        _freeList.AddFirst(new LinkedListNode<FreeBlock>(new FreeBlock(0, Capacity)));
    }

    public bool TryAllocate(nuint size, out nuint offset)
    {
        if (size == 0)
        {
            offset = 0;
            return false;
        }

        var node = _freeList.First;
        while (node != null)
        {
            var block = node.Value;
            if (block.Size >= size)
            {
                offset = block.Offset;
                _allocations[offset] = size;

                block.Offset += size;
                block.Size -= size;

                if (block.Size == 0)
                    _freeList.Remove(node);

                return true;
            }
            node = node.Next;
        }

        offset = 0;
        return false;
    }

    public void Free(nuint offset)
    {
        if (!_allocations.Remove(offset, out nuint size))
            return;

        var node = _freeList.First;
        LinkedListNode<FreeBlock>? insertBefore = null;

        while (node != null)
        {
            if (node.Value.Offset > offset)
            {
                insertBefore = node;
                break;
            }
            node = node.Next;
        }

        var newNode = insertBefore != null
            ? _freeList.AddBefore(insertBefore, new FreeBlock(offset, size))
            : _freeList.AddLast(new FreeBlock(offset, size));

        Coalesce(newNode);
    }

    public void Grow(nuint newCapacity)
    {
        if (newCapacity <= Capacity) return;

        nuint addedSize = newCapacity - Capacity;
        nuint oldCapacity = Capacity;
        Capacity = newCapacity;

        var newNode = _freeList.AddLast(new FreeBlock(oldCapacity, addedSize));

        Coalesce(newNode);
    }

    private void Coalesce(LinkedListNode<FreeBlock> node)
    {
        var block = node.Value;
        var prevNode = node.Previous;
        if (prevNode != null)
        {
            var prevBlock = prevNode.Value;
            if (prevBlock.Offset + prevBlock.Size == block.Offset)
            {
                prevBlock.Size += block.Size;
                _freeList.Remove(node);
                node = prevNode;
                block = node.Value;
            }
        }

        var nextNode = node.Next;
        if (nextNode != null)
        {
            var nextBlock = nextNode.Value;
            if (block.Offset + block.Size == nextBlock.Offset)
            {
                block.Size += nextBlock.Size;
                _freeList.Remove(nextNode);
            }
        }
    }
}