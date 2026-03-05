namespace Minecraft.NET.Engine.ECS;

internal static class ComponentTypeId<T>
{
    public static readonly int Id = Interlocked.Increment(ref Registry.NextComponentId);
}

public class Registry
{
    internal static int NextComponentId = -1;

    private int _nextEntityId = 0;
    private readonly Queue<int> _reusableIds = [];
    private IPool[] _pools = new IPool[32];

    public EntityBuilder Create()
        => new(this, new Entity(_reusableIds.Count > 0 ? _reusableIds.Dequeue() : _nextEntityId++));

    public void Destroy(Entity entity)
    {
        for (int i = 0; i < _pools.Length; i++)
        {
            var pool = _pools[i];
            if (pool.Has(entity.Id))
                pool.Remove(entity.Id);
        }
        _reusableIds.Enqueue(entity.Id);
    }

    public ComponentPool<T> GetPool<T>()
        where T : unmanaged
    {
        int id = ComponentTypeId<T>.Id;

        if (id >= _pools.Length)
            Array.Resize(ref _pools, id * 2);

        ref IPool pool = ref _pools[id];
        pool ??= new ComponentPool<T>();

        return (ComponentPool<T>)pool;
    }

    public View<T1> GetView<T1>()
        where T1 : unmanaged
        => new(this);
    public View<T1, T2> GetView<T1, T2>()
        where T1 : unmanaged
        where T2 : unmanaged
        => new(this);
    public View<T1, T2, T3> GetView<T1, T2, T3>()
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        => new(this);

    public void AddComponent<T>(Entity entity, in T component) where T : unmanaged
        => GetPool<T>().Add(entity.Id, in component);
    public ref T GetComponent<T>(Entity entity) where T : unmanaged
        => ref GetPool<T>().Get(entity.Id);
    public bool HasComponent<T>(Entity entity) where T : unmanaged
        => GetPool<T>().Has(entity.Id);
    public void RemoveComponent<T>(Entity entity) where T : unmanaged
        => GetPool<T>().Remove(entity.Id);
}