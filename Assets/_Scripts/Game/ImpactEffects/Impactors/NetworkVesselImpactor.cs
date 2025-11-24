using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game
{
    [RequireComponent(typeof(VesselImpactor))]
    public class NetworkVesselImpactor : NetworkBehaviour
    {
        [SerializeField] VesselImpactor vesselImpactor;
        
        private void Awake()
        {
            vesselImpactor ??= GetComponent<VesselImpactor>();
        }
        
        public void ExecuteOnHitOmniCrystal(CrystalImpactData data)
        {
            ExecuteCrystalImpact_ServerRpc(data);
        }
        
        [ServerRpc]
        void ExecuteCrystalImpact_ServerRpc(CrystalImpactData data) =>
            ExecuteCrystalImpact_ClientRpc(data);

        [ClientRpc]
        void ExecuteCrystalImpact_ClientRpc(CrystalImpactData data) =>
            vesselImpactor.ExecuteCrystalImpact(data);

        void OnValidate()
        {
            vesselImpactor ??= GetComponent<VesselImpactor>();
        }
    }
}