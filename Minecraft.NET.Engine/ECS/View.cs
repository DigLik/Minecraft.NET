namespace Minecraft.NET.Engine.ECS;

public readonly ref struct QueryItem<T1>
{
    public readonly Entity Entity;
    public readonly ref T1 Comp1;

    public QueryItem(Entity entity, ref T1 comp1)
    {
        Entity = entity;
        Comp1 = ref comp1;
    }
}

public readonly ref struct QueryItem<T1, T2>
{
    public readonly Entity Entity;
    public readonly ref T1 Comp1;
    public readonly ref T2 Comp2;

    public QueryItem(Entity entity, ref T1 comp1, ref T2 comp2)
    {
        Entity = entity;
        Comp1 = ref comp1;
        Comp2 = ref comp2;
    }
}

public readonly ref struct QueryItem<T1, T2, T3>
{
    public readonly Entity Entity;
    public readonly ref T1 Comp1;
    public readonly ref T2 Comp2;
    public readonly ref T3 Comp3;

    public QueryItem(Entity entity, ref T1 comp1, ref T2 comp2, ref T3 comp3)
    {
        Entity = entity;
        Comp1 = ref comp1;
        Comp2 = ref comp2;
        Comp3 = ref comp3;
    }
}

public readonly ref struct View<T1>(Registry registry)
    where T1 : unmanaged
{
    private readonly ComponentPool<T1> _pool1 = registry.GetPool<T1>();

    public Enumerator GetEnumerator() => new(_pool1);

    public ref struct Enumerator(ComponentPool<T1> pool1)
    {
        private readonly ReadOnlySpan<int> _entities = pool1.EntitiesList;
        private int _index = -1;

        public bool MoveNext() => ++_index < _entities.Length;

        public readonly QueryItem<T1> Current
        {
            get
            {
                int entityId = _entities[_index];
                return new QueryItem<T1>(new Entity(entityId), ref pool1.Get(entityId));
            }
        }
    }
}

public readonly ref struct View<T1, T2>(Registry registry)
    where T1 : unmanaged
    where T2 : unmanaged
{
    private readonly ComponentPool<T1> _pool1 = registry.GetPool<T1>();
    private readonly ComponentPool<T2> _pool2 = registry.GetPool<T2>();

    public Enumerator GetEnumerator() => new(_pool1, _pool2);

    public ref struct Enumerator
    {
        private readonly ComponentPool<T1> _pool1;
        private readonly ComponentPool<T2> _pool2;
        private readonly ReadOnlySpan<int> _entities;
        private int _index = -1;
        private readonly bool _pool1IsSmaller;

        public Enumerator(ComponentPool<T1> pool1, ComponentPool<T2> pool2)
        {
            _pool1 = pool1;
            _pool2 = pool2;
            _pool1IsSmaller = pool1.Count <= pool2.Count;
            _entities = _pool1IsSmaller ? pool1.EntitiesList : pool2.EntitiesList;
        }

        public bool MoveNext()
        {
            while (++_index < _entities.Length)
            {
                int entityId = _entities[_index];
                if (_pool1IsSmaller ? _pool2.Has(entityId) : _pool1.Has(entityId))
                    return true;
            }
            return false;
        }

        public readonly QueryItem<T1, T2> Current
        {
            get
            {
                int entityId = _entities[_index];
                return new QueryItem<T1, T2>(
                    new Entity(entityId),
                    ref _pool1.Get(entityId),
                    ref _pool2.Get(entityId)
                );
            }
        }
    }
}

public readonly ref struct View<T1, T2, T3>(Registry registry)
    where T1 : unmanaged
    where T2 : unmanaged
    where T3 : unmanaged
{
    private readonly ComponentPool<T1> _pool1 = registry.GetPool<T1>();
    private readonly ComponentPool<T2> _pool2 = registry.GetPool<T2>();
    private readonly ComponentPool<T3> _pool3 = registry.GetPool<T3>();

    public Enumerator GetEnumerator() => new(_pool1, _pool2, _pool3);

    public ref struct Enumerator
    {
        private readonly ComponentPool<T1> _pool1;
        private readonly ComponentPool<T2> _pool2;
        private readonly ComponentPool<T3> _pool3;
        private readonly ReadOnlySpan<int> _entities;

        private int _index;
        private readonly byte _smallestPoolIndex;

        public Enumerator(ComponentPool<T1> pool1, ComponentPool<T2> pool2, ComponentPool<T3> pool3)
        {
            _pool1 = pool1;
            _pool2 = pool2;
            _pool3 = pool3;
            _index = -1;

            int c1 = pool1.Count, c2 = pool2.Count, c3 = pool3.Count;

            if (c1 <= c2 && c1 <= c3)
            {
                _smallestPoolIndex = 1;
                _entities = pool1.EntitiesList;
            }
            else if (c2 <= c1 && c2 <= c3)
            {
                _smallestPoolIndex = 2;
                _entities = pool2.EntitiesList;
            }
            else
            {
                _smallestPoolIndex = 3;
                _entities = pool3.EntitiesList;
            }
        }

        public bool MoveNext()
        {
            while (++_index < _entities.Length)
            {
                int entityId = _entities[_index];

                bool match = _smallestPoolIndex switch
                {
                    1 => _pool2.Has(entityId) && _pool3.Has(entityId),
                    2 => _pool1.Has(entityId) && _pool3.Has(entityId),
                    _ => _pool1.Has(entityId) && _pool2.Has(entityId)
                };

                if (match) return true;
            }
            return false;
        }

        public readonly QueryItem<T1, T2, T3> Current
        {
            get
            {
                int entityId = _entities[_index];
                return new QueryItem<T1, T2, T3>(
                    new Entity(entityId),
                    ref _pool1.Get(entityId),
                    ref _pool2.Get(entityId),
                    ref _pool3.Get(entityId)
                );
            }
        }
    }
}