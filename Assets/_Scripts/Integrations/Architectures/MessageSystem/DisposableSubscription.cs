using System;

namespace CosmicShore.Integrations.Architectures.MessageSystem
{
    public class DisposableSubscription<T> : IDisposable
    {
        /// <summary>
        /// Event handler for dealing with 
        /// </summary>
        private Action<T> _handler;
        private bool _isDisposed;
        private IMessageSystem<T> _messageSystem;
        
        public DisposableSubscription(MessageSystemV2<T> messageSystem, Action<T> handler)
        {
            _messageSystem = messageSystem;
            _handler = handler;
        }

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