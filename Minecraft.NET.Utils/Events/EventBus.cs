using System.Runtime.CompilerServices;

namespace HighPerformanceBus;

public interface IEventHandler<TEvent>
{
    void Handle(in TEvent @event);
}

public static class EventBus<TEvent>
{
    private static IEventHandler<TEvent>[] _handlers = [];

    public static void Subscribe(IEventHandler<TEvent> handler)
    {
        IEventHandler<TEvent>[] oldArray, newArray;
        do
        {
            oldArray = _handlers;
            newArray = new IEventHandler<TEvent>[oldArray.Length + 1];
            Array.Copy(oldArray, newArray, oldArray.Length);
            newArray[^1] = handler;
        }
        while (Interlocked.CompareExchange(ref _handlers, newArray, oldArray) != oldArray);
    }

    public static void Unsubscribe(IEventHandler<TEvent> handler)
    {
        IEventHandler<TEvent>[] oldArray, newArray;
        do
        {
            oldArray = _handlers;
            int index = Array.IndexOf(oldArray, handler);
            if (index < 0) return;

            newArray = new IEventHandler<TEvent>[oldArray.Length - 1];
            if (index > 0) Array.Copy(oldArray, newArray, index);
            if (index < oldArray.Length - 1)
                Array.Copy(oldArray, index + 1, newArray, index, oldArray.Length - index - 1);
        }
        while (Interlocked.CompareExchange(ref _handlers, newArray, oldArray) != oldArray);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Publish(in TEvent @event)
    {
        var handlers = _handlers;
        foreach (ref readonly var handler in handlers.AsSpan())
            handler.Handle(in @event);
    }
}

public static class EventBus
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Publish<TEvent>(in TEvent @event)
        => EventBus<TEvent>.Publish(in @event);

    public static void Subscribe<TEvent>(IEventHandler<TEvent> handler)
        => EventBus<TEvent>.Subscribe(handler);

    public static void Unsubscribe<TEvent>(IEventHandler<TEvent> handler)
        => EventBus<TEvent>.Unsubscribe(handler);
}