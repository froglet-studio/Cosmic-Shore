using System.Collections.Generic;

namespace CosmicShore.Engine.Networking
{
    public enum NetworkVariableReadPermission
    {
        Everyone = 0,
        Owner = 1,
    }

    public enum NetworkVariableWritePermission
    {
        Server = 0,
        Owner = 1,
    }

    /// <summary>
    /// A replicated field of a NetworkBehaviour (NetworkVariable / NetworkList), as the network
    /// driver sees it: bound to its behaviour at spawn, dirty when written, and able to write its
    /// full state or its pending delta and to apply either from the wire.
    /// </summary>
    internal interface INetVar
    {
        NetworkVariableReadPermission ReadPerm { get; }
        NetworkVariableWritePermission WritePerm { get; }
        NetworkBehaviour Behaviour { get; }
        int Index { get; }
        void Bind(NetworkBehaviour behaviour, int index);
        void WriteState(System.IO.BinaryWriter w);
        void ReadState(System.IO.BinaryReader r, bool notify);
        void WriteDelta(System.IO.BinaryWriter w);
        void ReadDelta(System.IO.BinaryReader r);
        void ClearDelta();
    }

    /// <summary>
    /// Replicated state container with the same API contract ported code was written
    /// against: construct with read/write permissions, set <see cref="Value"/>, observe
    /// via <see cref="OnValueChanged"/> (fires only on actual change, with previous and
    /// new values, locally on write and — once the transport phase lands — on remote
    /// replication). Wire replication plugs into this type in the networking phase;
    /// until then behavior is exact for single-process (host-mode) play.
    /// [Serializable] like the original (NetworkVariableBase): Instantiate inlines it BY VALUE,
    /// so two clones of one prefab never share a variable (or its subscribers).
    /// </summary>
    [System.Serializable]
    public class NetworkVariable<T> : INetVar
    {
        public delegate void OnValueChangedDelegate(T previousValue, T newValue);

        /// <summary>Fires after the value changes. Field (not event) to match the original API shape.</summary>
        public OnValueChangedDelegate OnValueChanged;

        public NetworkVariableReadPermission ReadPerm { get; }
        public NetworkVariableWritePermission WritePerm { get; }

        T _value;

        public NetworkVariable(
            T value = default,
            NetworkVariableReadPermission readPerm = NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission writePerm = NetworkVariableWritePermission.Server)
        {
            _value = value;
            ReadPerm = readPerm;
            WritePerm = writePerm;
        }

        [System.NonSerialized] NetworkBehaviour _behaviour;
        [System.NonSerialized] int _index;

        public T Value
        {
            get => _value;
            set
            {
                if (_behaviour != null && !NetDriver.CanWrite(this))
                {
                    NetDriver.ReportWritePermission(this);
                    return;
                }
                if (EqualityComparer<T>.Default.Equals(_value, value)) return;
                T previous = _value;
                _value = value;
                if (_behaviour != null) NetDriver.MarkDirty(this);
                OnValueChanged?.Invoke(previous, value);
            }
        }

        /// <summary>Original surface: the dirty flag the driver clears after sending.</summary>
        public void SetDirty(bool isDirty) { if (isDirty && _behaviour != null) NetDriver.MarkDirty(this); }

        /// <summary>Original surface: re-send the value (a struct mutated in place).</summary>
        public void CheckDirtyState(bool forceCheck = false) => SetDirty(true);

        NetworkBehaviour INetVar.Behaviour => _behaviour;
        int INetVar.Index => _index;
        void INetVar.Bind(NetworkBehaviour behaviour, int index) { _behaviour = behaviour; _index = index; }
        void INetVar.WriteState(System.IO.BinaryWriter w) => NetWire.Write(w, typeof(T), _value);
        void INetVar.WriteDelta(System.IO.BinaryWriter w) => NetWire.Write(w, typeof(T), _value);
        void INetVar.ClearDelta() { }
        void INetVar.ReadDelta(System.IO.BinaryReader r) => ((INetVar)this).ReadState(r, notify: true);

        void INetVar.ReadState(System.IO.BinaryReader r, bool notify)
        {
            T incoming = (T)NetWire.Read(r, typeof(T));
            T previous = _value;
            _value = incoming;
            if (notify && !EqualityComparer<T>.Default.Equals(previous, incoming)) OnValueChanged?.Invoke(previous, incoming);
        }
    }
}
