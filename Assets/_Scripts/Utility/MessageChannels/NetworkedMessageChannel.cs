using Unity.Netcode;
using UnityEngine;
using VContainer;

namespace CosmicShore.Utilities
{ 
    /// <summary>
    /// This type of message channel allows the server to publish a message that will be sent to clients as well
    /// as being published locally. Clients and the server both can subscribe to it.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class NetworkedMessageChannel<T> : MessageChannel<T> where T : unmanaged, INetworkSerializeByMemcpy
    {
        private NetworkManager _networkManager;

        private string _name;

        public NetworkedMessageChannel()
        {
            _name = $"{typeof(T).FullName}NetworkMessageChannel";
        }

        [Inject]
        private void InjectDependencies(NetworkManager networkManager)
        {
            _networkManager = networkManager;
            _networkManager.OnClientConnectedCallback += OnClientConnected;
            if (_networkManager.IsListening)
            {
                RegisterHandler();
            }
        }

        public override void Publish(T message)
        {
            if (_networkManager.IsServer)
            {
                // send message to clients, then publish locally.
                SendMessageThroughNetwork(message);
                base.Publish(message);
            }
            else
            {
                Debug.LogError("Only a server can publish in a NetworkedMessageChannel");
            }
        }

        public override void Dispose()
        {
            if (!IsDisposed)
            {
                if (_networkManager != null && _networkManager.CustomMessagingManager != null)
                {
                    _networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(_name);
                }
            }
            base.Dispose();
        }

        private void OnClientConnected(ulong clientId)
        {
            RegisterHandler();
        }

        private void RegisterHandler()
        {
            // Only register message handler on clients
            if (!_networkManager.IsServer)
            {
                _networkManager.CustomMessagingManager.RegisterNamedMessageHandler(_name, ReceiveMessageThroughNetwork);
            }
        }

        private void SendMessageThroughNetwork(T message)
        {
            FastBufferWriter writer = new FastBufferWriter(FastBufferWriter.GetWriteSize<T>(), Unity.Collections.Allocator.Temp);
            writer.WriteValueSafe(message);
            _networkManager.CustomMessagingManager.SendNamedMessageToAll(_name, writer);
        }

        private void ReceiveMessageThroughNetwork(ulong clientId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out T message);
            base.Publish(message);
        }


    }
}

