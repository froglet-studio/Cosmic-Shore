using System.Threading;
using Loxodon.Framework.Messaging;
using UnityEngine;
using System.Threading.Tasks;
using VContainer.Unity;

namespace CosmicShore.Integrations.Loxodon.MVVM
{
    public class TestMessenger : IStartable
    {
        public IMessenger Messager { get; } = new Messenger();
        private ISubscription<TestMessage> _syncMessage;
        private ISubscription<TestMessage> _asyncMessage;

        public void Start()
        {
            _syncMessage = Messager.Subscribe<TestMessage>(OnSendingMessage);
            _asyncMessage = Messager.Subscribe<TestMessage>(OnSendingMessageAsync)
                .ObserveOn(SynchronizationContext.Current);
#if UNITY_WEBGL && !UNITY_EDITOR
            Messager.Publish(new TestMessage(this, "A test message for WEBGL build."))
#else
            Task.Run(() =>
            {
                Messager.Publish(new TestMessage(this, "A test message for PC or console builds."));
            });
#endif
        }

        protected void OnSendingMessage(TestMessage message)
        {
            Debug.LogFormat("Sync: Thread ID {0} received: {1}", Thread.CurrentThread.ManagedThreadId, message.Content);
        }

        protected void OnSendingMessageAsync(TestMessage message)
        {
            Debug.LogFormat("Async: Thread ID: {0} received {1}", Thread.CurrentThread.ManagedThreadId, message.Content);
        }

        private void OnDestroy()
        {
            _syncMessage?.Dispose();
            _syncMessage = null;
            
            _asyncMessage?.Dispose();
            _asyncMessage = null;

            Debug.Log("Test Messenger: all messages disposed.");
        }
    }
}
