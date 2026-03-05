using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Minecraft.NET.Utils.Collections;

public unsafe ref struct NativeLinearAllocator : IDisposable
{
    private const int SmallSlotSize = 64;
    private const int SlotsPerChunk = 64;
    private const int ChunkMemorySize = SmallSlotSize * SlotsPerChunk;

    private struct ChunkNode
    {
        public ulong Bitmask;
        public byte* Memory;
        public ChunkNode* Next;
    }

    private ChunkNode* _headSmallChunk;
    private ChunkNode* _currentSmallChunk;

    private byte* _linearStart;
    private byte* _linearCurrent;
    private readonly byte* _linearEnd;

    private struct LargeNode
    {
        public byte* Memory;
        public LargeNode* Next;
    }
    private LargeNode* _largeAllocations;

    public NativeLinearAllocator(nuint linearBufferSize = 65536)
    {
        _headSmallChunk = null;
        _currentSmallChunk = null;
        _largeAllocations = null;
        _linearStart = (byte*)NativeMemory.Alloc(linearBufferSize);
        _linearCurrent = _linearStart;
        _linearEnd = _linearStart + linearBufferSize;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void* Allocate(nuint size)
    {
        if (size <= SmallSlotSize)
        {
            if (_currentSmallChunk != null && _currentSmallChunk->Bitmask != ulong.MaxValue)
            {
                int index = BitOperations.TrailingZeroCount(~_currentSmallChunk->Bitmask);
                _currentSmallChunk->Bitmask |= (1UL << index);
                return _currentSmallChunk->Memory + (index * SmallSlotSize);
            }
            return AllocateSmallSlow();
        }

        nuint alignedSize = (nuint)((size + 7) & ~7ul);
        if ((nuint)(_linearEnd - _linearCurrent) >= alignedSize)
        {
            void* ptr = _linearCurrent;
            _linearCurrent += alignedSize;
            return ptr;
        }

        return AllocateLargeSlow(alignedSize);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void* AllocateSmallSlow()
    {
        ChunkNode* curr = _headSmallChunk;
        while (curr != null)
        {
            if (curr->Bitmask != ulong.MaxValue)
            {
                _currentSmallChunk = curr;
                int index = BitOperations.TrailingZeroCount(~curr->Bitmask);
                curr->Bitmask |= (1UL << index);
                return curr->Memory + (index * SmallSlotSize);
            }
            curr = curr->Next;
        }

        ChunkNode* newChunk = (ChunkNode*)NativeMemory.Alloc((nuint)sizeof(ChunkNode));
        newChunk->Memory = (byte*)NativeMemory.Alloc(ChunkMemorySize);
        newChunk->Bitmask = 1UL;
        newChunk->Next = _headSmallChunk;

        _headSmallChunk = newChunk;
        _currentSmallChunk = newChunk;

        return newChunk->Memory;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void* AllocateLargeSlow(nuint size)
    {
        LargeNode* node = (LargeNode*)NativeMemory.Alloc((nuint)sizeof(LargeNode));
        node->Memory = (byte*)NativeMemory.Alloc(size);
        node->Next = _largeAllocations;
        _largeAllocations = node;
        return node->Memory;
    }

    public readonly void Free(void* ptr)
    {
        ChunkNode* curr = _headSmallChunk;
        while (curr != null)
        {
            if (ptr >= curr->Memory && ptr < curr->Memory + ChunkMemorySize)
            {
                int index = (int)((byte*)ptr - curr->Memory) / SmallSlotSize;
                curr->Bitmask &= ~(1UL << index);
                return;
            }
            curr = curr->Next;
        }
    }

    public void Dispose()
    {
        if (_linearStart != null)
        {
            NativeMemory.Free(_linearStart);
            _linearStart = null;
        }

        ChunkNode* currChunk = _headSmallChunk;
        while (currChunk != null)
        {
            ChunkNode* next = currChunk->Next;
            NativeMemory.Free(currChunk->Memory);
            NativeMemory.Free(currChunk);
            currChunk = next;
        }
        _headSmallChunk = null;

        LargeNode* currLarge = _largeAllocations;
        while (currLarge != null)
        {
            LargeNode* next = currLarge->Next;
            NativeMemory.Free(currLarge->Memory);
            NativeMemory.Free(currLarge);
            currLarge = next;
        }
        _largeAllocations = null;
    }
}