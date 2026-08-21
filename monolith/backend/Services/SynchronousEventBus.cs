using System.Collections.Concurrent;

namespace CV_Generator.Services;

public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event) where TEvent : class;
    IDisposable Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : class;
}

public class SynchronousEventBus : IEventBus
{
    private readonly ConcurrentDictionary<Type, List<Func<object, Task>>> _handlers = new();
    private readonly ILogger<SynchronousEventBus> _logger;

    public SynchronousEventBus(ILogger<SynchronousEventBus> logger)
    {
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent @event) where TEvent : class
    {
        var eventType = typeof(TEvent);
        if (_handlers.TryGetValue(eventType, out var handlers))
        {
            foreach (var handler in handlers)
            {
                try
                {
                    await handler(@event);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Event handler failed for {EventType}", eventType.Name);
                }
            }
        }
    }

    public IDisposable Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : class
    {
        var eventType = typeof(TEvent);
        var handlers = _handlers.GetOrAdd(eventType, _ => new List<Func<object, Task>>());

        async Task HandlerWrapper(object o) => await handler((TEvent)o);

        lock (handlers)
        {
            handlers.Add(HandlerWrapper);
        }

        return new Subscription(() =>
        {
            lock (handlers)
            {
                handlers.Remove(HandlerWrapper);
            }
        });
    }

    private sealed class Subscription : IDisposable
    {
        private readonly Action _unsubscribe;
        private bool _disposed;
        public Subscription(Action unsubscribe) => _unsubscribe = unsubscribe;
        public void Dispose()
        {
            if (!_disposed) { _disposed = true; _unsubscribe(); }
        }
    }
}
