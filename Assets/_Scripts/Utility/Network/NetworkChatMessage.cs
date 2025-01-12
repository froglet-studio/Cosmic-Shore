using Unity.Collections;
using Unity.Netcode;


namespace CosmicShore.Utilities.Network
{
    /// <summary>
    /// A simple chat message struct that can be sent over the network.
    /// </summary>
    public struct NetworkChatMessage : INetworkSerializeByMemcpy
    {
        public FixedPlayerName Name;
        public ChatMessage Message;
    }

    /// <summary>
    /// Wrapping FixedString so that if we want to change message max size in the future, we only do it once here.
    /// </summary>
    public struct ChatMessage : INetworkSerializable
    {
        private FixedString128Bytes _message;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref _message);
        }

        public override string ToString()
        {
            return _message.Value.ToString();
        }

        public static implicit operator string(ChatMessage s) => s.ToString();
        public static implicit operator ChatMessage(string s) => new ChatMessage() { _message = new FixedString32Bytes(s) };
    }
}

