namespace CosmicShore.Engine.Networking
{
    /// <summary>
    /// Network-serialization contract RPC payload structs implement (e.g. CrystalImpactData),
    /// preserving the original API shape so those structs port verbatim. Nothing constructs
    /// a <see cref="BufferSerializer{TReaderWriter}"/> until the transport phase supplies an
    /// <see cref="IReaderWriter"/> implementation — single-process play never serializes.
    /// </summary>
    public interface INetworkSerializable
    {
        void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter;
    }

    /// <summary>Reader/writer backing a <see cref="BufferSerializer{TReaderWriter}"/> (transport phase supplies implementations).</summary>
    public interface IReaderWriter
    {
        bool IsReader { get; }
        bool IsWriter { get; }
        void SerializeValue<T>(ref T value) where T : unmanaged;
    }

    /// <summary>
    /// Thin façade over an <see cref="IReaderWriter"/>, mirroring the original engine's
    /// dual-mode (read/write) serializer that <see cref="INetworkSerializable.NetworkSerialize{T}"/>
    /// receives. One unmanaged-generic SerializeValue covers the primitive and enum
    /// overload families ported call sites use.
    /// </summary>
    public struct BufferSerializer<TReaderWriter> where TReaderWriter : IReaderWriter
    {
        TReaderWriter _implementation;

        public BufferSerializer(TReaderWriter implementation) { _implementation = implementation; }

        public bool IsReader => _implementation.IsReader;
        public bool IsWriter => _implementation.IsWriter;

        public void SerializeValue<T>(ref T value) where T : unmanaged => _implementation.SerializeValue(ref value);
    }
}
