using Unity.Collections;
using Unity.Netcode;

namespace CosmicShore.Utilities.Network
{
    public class CustomNetworkVariable<T> : NetworkVariable<T>
    {
        /*public CustomNetworkVariable() { }

        public CustomNetworkVariable(NetworkVariableReadPermission readPerm = DefaultReadPerm, NetworkVariableWritePermission writePerm = DefaultWritePerm)
            : base(readPerm, writePerm) { }

        public CustomNetworkVariable(T value, NetworkVariableReadPermission readPerm = DefaultReadPerm, NetworkVariableWritePermission writePerm = DefaultWritePerm)
            : base(value, readPerm, writePerm) { }

        public void ForceNotify()
        {
            NotifyObservers();
        }

        private void NotifyObservers()
        {
            foreach (var observer in m_Observers)
            {
                observer.Value.ClientRpcParams.SendParams.TargetClientIds = new ulong[] { observer.Key };
                observer.Value.OnNetworkVariableUpdateClientRpc(GetNetworkObject().NetworkObjectId, NetworkBehaviour.__NetworkVariableIndexInvalid, FastBufferWriter.Create(1, Allocator.Temp), 0);
            }
        }*/
    }
}