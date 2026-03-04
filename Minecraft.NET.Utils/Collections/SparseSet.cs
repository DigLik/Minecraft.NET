using System.Runtime.InteropServices;

namespace Minecraft.NET.Utils.Collections;

public class SparseSet<T>
{
    private readonly List<int> _dense = new(1024);
    private readonly List<T> _elements = new(1024);
    private int[] _sparse = new int[1024];

    public int Count => _dense.Count;

    public List<int> Entities => _dense;

    public void Add(int id, in T element)
    {
        if (id >= _sparse.Length)
            Array.Resize(ref _sparse, Max(id + 1, _sparse.Length * 2));

        _dense.Add(id);
        _elements.Add(element);
        _sparse[id] = _dense.Count - 1;
    }

    public bool Contains(int id)
    {
        if (id >= _sparse.Length) return false;
        int denseIndex = _sparse[id];
        return denseIndex < _dense.Count && _dense[denseIndex] == id;
    }

    public ref T Get(int id)
    {
        int denseIndex = _sparse[id];
        return ref CollectionsMarshal.AsSpan(_elements)[denseIndex];
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

        _dense.RemoveAt(lastDenseIndex);
        _elements.RemoveAt(lastDenseIndex);
    }
}