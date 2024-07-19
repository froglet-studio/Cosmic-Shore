using System;

namespace CosmicShore.Integrations.Architectures.MessageSystem
{
    public interface IMessageSystem<T> : IPublisher<T>, ISubscriber<T>, IDisposable
    {
        bool IsDisposed { get; }
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