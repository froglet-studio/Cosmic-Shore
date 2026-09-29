using Unity.Netcode;
using UnityEngine;
using CosmicShore.Data;
namespace CosmicShore.Gameplay
{
    [RequireComponent(typeof(VesselImpactor))]
    public class NetworkVesselImpactor : NetworkBehaviour
    {
        [SerializeField] VesselImpactor vesselImpactor;

        public VesselImpactor VesselImpactor => vesselImpactor;

        private void Awake()
        {
            vesselImpactor ??= GetComponent<VesselImpactor>();
        }

        /// <summary>
        /// Owner-side entry point for a validated joust: this vessel (the impactee, whose
        /// skimmer a slower opponent just swept through) scored a joust point against
        /// <paramref name="impactorNetImpactor"/>'s vessel. Routed through the server and
        /// broadcast so every machine runs the confirmed effects exactly once - the
        /// server's SOAP raise is the one StatsManager records. Mirrors the crystal
        /// impact round-trip below.
        /// </summary>
        public void ReportJoust(NetworkVesselImpactor impactorNetImpactor)
        {
            ExecuteJoust_ServerRpc(impactorNetImpactor);
        }

        [ServerRpc]
        void ExecuteJoust_ServerRpc(NetworkBehaviourReference impactorRef) =>
            ExecuteJoust_ClientRpc(impactorRef);

        [ClientRpc]
        void ExecuteJoust_ClientRpc(NetworkBehaviourReference impactorRef)
        {
            if (!impactorRef.TryGet(out NetworkVesselImpactor impactorNetImpactor))
                return;

            vesselImpactor.ExecuteJoustImpact(impactorNetImpactor.VesselImpactor);
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
            vesselImpactor.ExecuteOmniCrystalImpact(data);
        
        public void ExecuteOnHitElementalCrystal(CrystalImpactData data)
        {
            ExecuteElementalCrystalImpact_ServerRpc(data);
        }

        [ServerRpc]
        void ExecuteElementalCrystalImpact_ServerRpc(CrystalImpactData data) =>
            ExecuteElementalCrystalImpact_ClientRpc(data);

        [ClientRpc]
        void ExecuteElementalCrystalImpact_ClientRpc(CrystalImpactData data) =>
            vesselImpactor.ExecuteElementalCrystalImpact(data);

        void OnValidate()
        {
            vesselImpactor ??= GetComponent<VesselImpactor>();
        }
    }
}