using System;

namespace CosmicShore.Utilities
{
    public class BufferedMessageChannel<T> : MessageChannel<T>, IBufferedMessageChannel<T>
    {
        public bool HasBufferedMessages { get; private set; } = false;
        public T BufferedMessage {get; private set;}

        public override void Publish(T message)
        {
            HasBufferedMessages = true;
            BufferedMessage = message;
            base.Publish(message);
        }

        public override IDisposable Subscribe(Action<T> handler)
        {
            IDisposable subscription = base.Subscribe(handler);

            if (HasBufferedMessages)
            {
                handler?.Invoke(BufferedMessage);
            }

            return subscription;
        }
    }
}