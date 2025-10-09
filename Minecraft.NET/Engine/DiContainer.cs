namespace Minecraft.NET.Engine;

public interface IServiceRegistry
{
    void RegisterSingleton<TService>(Func<DIProvider, TService> factory) where TService : class;
    void RegisterInstance<TService>(TService instance) where TService : class;
}

public interface IServiceProvider
{
    T Resolve<T>() where T : class;
}

public sealed class DiContainer : IServiceRegistry, IServiceProvider, IDisposable
{
    private sealed class ServiceDescriptor
    {
        public Func<DIProvider, object>? Factory { get; init; }
        public object? Instance { get; set; }
    }

    private readonly Dictionary<Type, ServiceDescriptor> _descriptors = [];
    private readonly HashSet<Type> _resolvingTypes = [];

    public void RegisterSingleton<TService>(Func<DIProvider, TService> factory) where TService : class
    {
        _descriptors[typeof(TService)] = new ServiceDescriptor
        {
            Factory = sp => factory(sp)!,
        };
    }

    public void RegisterInstance<TService>(TService instance) where TService : class
    {
        _descriptors[typeof(TService)] = new ServiceDescriptor
        {
            Instance = instance
        };
    }

    public T Resolve<T>() where T : class
    {
        var type = typeof(T);
        if (!_descriptors.TryGetValue(type, out var descriptor))
            throw new InvalidOperationException($"Сервис типа {type.Name} не зарегистрирован.");

        if (descriptor.Instance is not null)
        {
            return (T)descriptor.Instance;
        }

        if (_resolvingTypes.Contains(type))
        {
            throw new InvalidOperationException($"Обнаружена циклическая зависимость при разрешении сервиса '{type.Name}'.");
        }

        _resolvingTypes.Add(type);

        if (descriptor.Factory is null)
            throw new InvalidOperationException($"Нет фабрики для создания сервиса {type.Name}.");

        var instance = descriptor.Factory(this);
        descriptor.Instance = instance;

        _resolvingTypes.Remove(type);

        return (T)instance;
    }

    public void Dispose()
    {
        foreach (var descriptor in _descriptors.Values)
        {
            if (descriptor.Instance is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}