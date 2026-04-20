using Minecraft.NET.Utils.Collections;

namespace Minecraft.NET.Engine.ECS;

public class ComponentPool<T> : IPool where T : unmanaged
{
    private readonly SparseSet<T> _sparseSet = new();

    public int Count => _sparseSet.Count;
    public ReadOnlySpan<int> EntitiesList => _sparseSet.Data;

    public void Add(int entityId, in T component) => _sparseSet.Add(entityId, in component);
    public ref T Get(int entityId) => ref _sparseSet.Get(entityId);
    public void Remove(int entityId) => _sparseSet.Remove(entityId);
    public bool Has(int entityId) => _sparseSet.Contains(entityId);
}