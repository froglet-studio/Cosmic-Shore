using System;

namespace CosmicShore.Integrations.Architectures.MessageSystem
{
    /// <summary>
    /// Disposable Subscription handles a message system subscription, automatically unsubscribes it when being disposed.
    /// </summary>
    /// <typeparam name="T">Message data type</typeparam>
    public class DisposableSubscription<T> : IDisposable
    {
        /// <summary>
        /// Event handler for dealing with corresponding type of message
        /// </summary>
        private Action<T> _handler;
        
        /// <summary>
        /// A flag marks whether the subscription is disposed, used in Dispose() implementation
        /// </summary>
        private bool _isDisposed;
        
        /// <summary>
        /// A reference of the messaging system
        /// </summary>
        private IMessageSystem<T> _messageSystem;
        
        /// <summary>
        /// Disposable Subscription constructor, intake the message system and event handler.
        /// </summary>
        /// <param name="messageSystem">Message system</param>
        /// <param name="handler">Event handler</param>
        public DisposableSubscription(MessageSystemV2<T> messageSystem, Action<T> handler)
        {
            _messageSystem = messageSystem;
            _handler = handler;
        }

        /// <summary>
        /// IDisposable implementation, unsubscribe the event handler when being disposed.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;
            
            if(!_messageSystem.IsDisposed) _messageSystem.Unsubscribe(_handler);

            _handler = null;
            _messageSystem = null;
        }
    }
}