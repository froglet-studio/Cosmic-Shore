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
    /// Replicated state container with the same API contract ported code was written
    /// against: construct with read/write permissions, set <see cref="Value"/>, observe
    /// via <see cref="OnValueChanged"/> (fires only on actual change, with previous and
    /// new values, locally on write and — once the transport phase lands — on remote
    /// replication). Wire replication plugs into this type in the networking phase;
    /// until then behavior is exact for single-process (host-mode) play.
    /// </summary>
    public class NetworkVariable<T>
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

        public T Value
        {
            get => _value;
            set
            {
                if (EqualityComparer<T>.Default.Equals(_value, value)) return;
                T previous = _value;
                _value = value;
                OnValueChanged?.Invoke(previous, value);
            }
        }
    }
}
