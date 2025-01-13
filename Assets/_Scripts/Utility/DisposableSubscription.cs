using CosmicShore.Utilities;
using System;

namespace CosmicShore.Utilities
{
    public class DisposableSubscription<T> : IDisposable
    {
        private Action<T> _handler;
        private bool _isDisposed;
        private IMessageChannel<T> _messageChannel;

        public DisposableSubscription(IMessageChannel<T> messageChannel, Action<T> handler)
        {
            _messageChannel = messageChannel;
            _handler = handler;
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;

                if (!_messageChannel.IsDisposed)
                {
                    _messageChannel.Unsubscribe(_handler);
                }

                _handler = null;
                _messageChannel = null;
            }
        }
    }
}