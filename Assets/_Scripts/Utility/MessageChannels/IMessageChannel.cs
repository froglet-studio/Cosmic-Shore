using System;

namespace CosmicShore.Utilities
{
    public interface IPublisher<T>
    {
        public void Publish(T message);
    }

    public interface ISubscriber<T>
    {
        public IDisposable Subscribe(Action<T> handler);
        public void Unsubscribe(Action<T> handler);
    }

    public interface IMessageChannel<T> : IPublisher<T>, ISubscriber<T>, IDisposable
    {
        public bool IsDisposed { get; }
    }

    public interface IBufferedMessageChannel<T> : IMessageChannel<T>
    {
        public bool HasBufferedMessages { get; }
        public T BufferedMessage { get; }
    }
}