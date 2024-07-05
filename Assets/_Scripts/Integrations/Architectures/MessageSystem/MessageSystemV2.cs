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
        private readonly Dictionary<Action<T>, bool> _pendingHandlers = new();
        
        /// <summary>
        /// A flag signifying if the message system is disposed.
        /// If true, don't try to access the message system, invoke or subscribe to any event handlers.
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// IDisposable implementation, clear event handlers and pending handlers upon message system garbage collection.
        /// </summary>
        public virtual void Dispose()
        {
            if (IsDisposed) return;

            IsDisposed = true;
            _handlers.Clear();
            _pendingHandlers.Clear();
        }

        /// <summary>
        /// Publish corresponding message type
        /// </summary>
        /// <param name="message">Message type to publish</param>
        public virtual void Publish(T message)
        {
            // Add handlers from pending handlers
            // Linq has deferred execution, the query is not executed until it actually enumerate over the results.
            // But in this case both AddRange and RemoveAll cause immediate execution.
            _handlers.AddRange(_pendingHandlers
                .Where(pair => pair.Value)
                .Select(pair => pair.Key));
            
            // Remove all handlers where pending value is false
            // which means these handlers are marked as to be removed.
            _handlers.RemoveAll(handler => 
                _pendingHandlers.ContainsKey(handler) && !_pendingHandlers[handler]);
            
            // Clear out pending handlers because they are already been added to handlers.
            _pendingHandlers.Clear();

            // Invoke all the event handlers with the corresponding message type
            _handlers.Where(handler => handler != null)
                .ToList()
                .ForEach(handler => handler.Invoke(message));

        }
        
        /// <summary>
        /// Subscribe to event handlers
        /// </summary>
        /// <param name="handler">Event that handles corresponding type</param>
        /// <returns>A disposable subscription</returns>
        /// <exception cref="Exception">An exception to notify </exception>
        public virtual IDisposable Subscribe(Action<T> handler)
        {
            if (IsSubscribed(handler)) throw new Exception("Attempting to subscribe the same handler more than once.");

            if (!_pendingHandlers.TryAdd(handler, true))
            {
                if (!_pendingHandlers[handler])
                    _pendingHandlers.Remove(handler);
            }

            var subscription = new DisposableSubscription<T>(this, handler);
            return subscription;
        }

        public void Unsubscribe(Action<T> handler)
        {
            if (!IsSubscribed(handler)) return;

            if (_pendingHandlers.TryAdd(handler, false)) return;
            
            if (_pendingHandlers[handler])
                _pendingHandlers.Remove(handler);
        }
        
        private bool IsSubscribed(Action<T> handler)
        {
            var isPendingRemoval = _pendingHandlers.ContainsKey(handler) && !_pendingHandlers[handler];
            var isPendingAdding = _pendingHandlers.ContainsKey(handler) && _pendingHandlers[handler];
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

        public IDisposable Subscribe(Action<T> handler)
        {
            throw new NotImplementedException();
        }

        public void Unsubscribe(Action<T> handler)
        {
            throw new NotImplementedException();
        }
    }

    public interface IMessageSystem<T> : IPublisher<T>, ISubscriber<T>, IDisposable
    {
        
    }

    public interface ISubscriber<T>
    {
        IDisposable Subscribe(Action<T> handler);
        void Unsubscribe(Action<T> handler);
    }

    public interface IPublisher<T>
    {
        void Publish(T message);
    }
}
