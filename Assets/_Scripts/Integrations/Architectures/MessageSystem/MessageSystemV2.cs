using System;
using System.Collections.Generic;
using System.Linq;

namespace CosmicShore.Integrations.Architectures.MessageSystem
{
    public class MessageSystemV2<T> : IMessageSystem<T>
    {
        /// <summary>
        /// A list of message handlers ready to be invoked and subscribed.
        /// </summary>
        private readonly List<Action<T>> _handlers = new();

        /// <summary>
        /// A dictionary of pending handlers.
        /// If the handler's pair value is true, it means the handler should be added.
        /// If the handler's pair value is false, it means the handler should be removed
        /// </summary>
        private readonly Dictionary<Action<T>, bool> _pendingHanders = new();
        
        public bool IsDisposed { get; private set; }

        public virtual void Dispose()
        {
            if (IsDisposed) return;

            IsDisposed = true;
            _handlers.Clear();
            _pendingHanders.Clear();
        }

        public virtual void Publish(T message)
        {
            // Add and remove handlers from pending handlers
            // Linq has deferred execution, the query is not executed until it actually enumerate over the results.
            // But in this case both AddRange and RemoveAll cause immediate execution.
            _handlers.AddRange(_pendingHanders
                .Where(pair => pair.Value).Select(pair => pair.Key));
            _handlers.RemoveAll(handler => 
                _pendingHanders.ContainsKey(handler) && !_pendingHanders[handler]);
            
            _pendingHanders.Clear();

            _handlers.Where(handler => handler != null)
                .ToList()
                .ForEach(handler => handler.Invoke(message));

        }

        public virtual IDisposable Subscribe(Action<T> handler)
        {
            if (!IsSubscribed(handler)) throw new Exception("Attempting to subscribe the same handler more than once.");

            if (!_pendingHanders.TryAdd(handler, true))
            {
                if (!_pendingHanders[handler])
                    _pendingHanders.Remove(handler);
            }

            var subscription = new DisposableSubscription<T>(this, handler);
            return subscription;
        }
        
        private bool IsSubscribed(Action<T> handler)
        {
            var isPendingRemoval = _pendingHanders.ContainsKey(handler) && !_pendingHanders[handler];
            var isPendingAdding = _pendingHanders.ContainsKey(handler) && _pendingHanders[handler];
            return _handlers.Contains(handler) && !isPendingRemoval || isPendingAdding;
        }

    }

    public class DisposableSubscription<T> : ISubscriber<T>, IDisposable
    {
        public DisposableSubscription(MessageSystemV2<T> messageSystemV2, Action<T> handler)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }

    public interface IMessageSystem<T> : IPublisher<T>, ISubscriber<T>, IDisposable
    {
    }

    public interface ISubscriber<T>
    {
    }

    public interface IPublisher<T>
    {
    }
}
