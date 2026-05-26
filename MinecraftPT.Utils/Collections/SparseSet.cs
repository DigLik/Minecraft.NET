using System.Runtime.InteropServices;

namespace MinecraftPT.Utils.Collections;

public class SparseSet<T> : IDisposable where T : unmanaged
{
    private NativeList<int> _dense = new(1024);
    private NativeList<T> _elements = new(1024);
    private int[] _sparse;

    public int Count => _dense.Count;

    public ReadOnlySpan<int> Data => MemoryMarshal.CreateReadOnlySpan(ref _dense[0], _dense.Count);

    public SparseSet()
    {
        _sparse = new int[1024];
        Array.Fill(_sparse, -1);
    }

    public void Add(int id, in T element)
    {
        if (id >= _sparse.Length)
        {
            int oldLength = _sparse.Length;
            int newLength = System.Math.Max(id + 1, oldLength * 2);
            Array.Resize(ref _sparse, newLength);
            Array.Fill(_sparse, -1, oldLength, newLength - oldLength);
        }

        _dense.Add(id);
        _elements.Add(element);
        _sparse[id] = _dense.Count - 1;
    }

    public bool Contains(int id)
    {
        if (id < 0 || id >= _sparse.Length) return false;
        int denseIndex = _sparse[id];
        return denseIndex >= 0 && denseIndex < _dense.Count && _dense[denseIndex] == id;
    }

    public ref T Get(int id)
    {
        if (!Contains(id)) throw new KeyNotFoundException($"Key {id} was not found in SparseSet.");
        int denseIndex = _sparse[id];
        return ref _elements[denseIndex];
    }

    public void Remove(int id)
    {
        if (!Contains(id)) return;

        int denseIndex = _sparse[id];
        int lastDenseIndex = _dense.Count - 1;
        int lastId = _dense[lastDenseIndex];

        _dense[denseIndex] = lastId;
        _elements[denseIndex] = _elements[lastDenseIndex];
        _sparse[lastId] = denseIndex;
        _sparse[id] = -1;

        _dense.RemoveAtSwapBack(lastDenseIndex);
        _elements.RemoveAtSwapBack(lastDenseIndex);
    }

    public void Dispose()
    {
        _dense.Dispose();
        _elements.Dispose();
    }
}