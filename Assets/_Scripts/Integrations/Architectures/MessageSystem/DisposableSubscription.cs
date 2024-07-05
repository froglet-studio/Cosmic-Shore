using System;

namespace CosmicShore.Integrations.Architectures.MessageSystem
{
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
}