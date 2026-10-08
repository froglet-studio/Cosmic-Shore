using Unity.Netcode;
using UnityEngine;
using CosmicShore.Data;
namespace CosmicShore.Gameplay
{
    [RequireComponent(typeof(VesselImpactor))]
    public class NetworkVesselImpactor : NetworkBehaviour, IElementalLossRelay
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

        // ── Combat petal ejection (IElementalLossRelay) ──────────────────────
        //
        // Element levels are OWNER state: NetElementLevels is owner-write, and a take from any
        // other copy of this hull changes nothing anybody reads. So a combat eject is settled
        // here, on the owner, as the shooter's owner saw the hit (ElementalTransfer type doc):
        //
        //   shooter's owner ──RelayEjectToOwner──> [owner here?] settle
        //                   └─CombatEject_ServerRpc─> [server owns it?] settle
        //                                          └─CombatEject_ClientRpc ─(owner only)─> settle
        //   settle ── mint locally ── PublishEject_ServerRpc ── PublishEject_ClientRpc ─(not owner)─> mint
        //
        // The ServerRpc + targeted ClientRpc pair is the ExecuteJoust shape above, narrowed to one
        // recipient with ClientRpcParams (MultiplayerMiniGameControllerBase does the same). Every
        // hop runs on the main thread, as every NGO RPC does.

        public bool IsNetworked => IsSpawned;
        public bool IsOwnedHere => IsSpawned && IsOwner;

        public int RelayEjectToOwner(float normalizedAmountPerElement, Vector3 impactVelocity,
                                     ElementalDebuffSources source)
        {
            if (normalizedAmountPerElement <= 0f) return 0;
            if (IsOwner) return SettleEjectAsOwner(normalizedAmountPerElement, impactVelocity, source);

            CombatEject_ServerRpc(normalizedAmountPerElement, impactVelocity, source);
            return 0;
        }

        // RequireOwnership = false: the CALLER is the shooter's owner, which by construction does
        // not own this hull. The server only forwards; the owner's AccrueElementalLoss still
        // clamps the take to what is held and honours a ward.
        [ServerRpc(RequireOwnership = false)]
        void CombatEject_ServerRpc(float normalizedAmountPerElement, Vector3 impactVelocity,
                                   ElementalDebuffSources source)
        {
            // The server owns every AI hull and the host's own; settle without a further hop.
            if (IsOwner)
            {
                SettleEjectAsOwner(normalizedAmountPerElement, impactVelocity, source);
                return;
            }

            CombatEject_ClientRpc(normalizedAmountPerElement, impactVelocity, source, new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
            });
        }

        [ClientRpc]
        void CombatEject_ClientRpc(float normalizedAmountPerElement, Vector3 impactVelocity,
                                   ElementalDebuffSources source, ClientRpcParams rpcParams = default)
        {
            // Ownership can move while the RPC is in flight (Hijack's swap). The pilot who was
            // shot no longer flies this hull, and its levels are no longer this machine's to publish.
            if (!IsOwner) return;
            SettleEjectAsOwner(normalizedAmountPerElement, impactVelocity, source);
        }

        int SettleEjectAsOwner(float normalizedAmountPerElement, Vector3 impactVelocity,
                               ElementalDebuffSources source)
        {
            var victim = vesselImpactor ? vesselImpactor.Vessel?.VesselStatus : null;
            if (victim == null) return 0;

            uint packed = ElementalTransfer.SettleEjectAll(victim, normalizedAmountPerElement, source);
            if (packed == 0u) return 0;   // warded, empty, or still accruing toward a whole petal

            ElementalTransfer.EjectSettled(victim, packed, impactVelocity);
            PublishEject_ServerRpc(packed, impactVelocity);
            return ElementalTransfer.TotalPetals(packed);
        }

        // Owner-only (the default): only the owner settled anything, so only the owner may
        // announce crystals. The levels themselves travel on NetElementLevels, not here.
        [ServerRpc]
        void PublishEject_ServerRpc(uint packedPetals, Vector3 impactVelocity) =>
            PublishEject_ClientRpc(packedPetals, impactVelocity);

        [ClientRpc]
        void PublishEject_ClientRpc(uint packedPetals, Vector3 impactVelocity)
        {
            if (IsOwner) return;   // the owner minted its own when it settled
            var victim = vesselImpactor ? vesselImpactor.Vessel?.VesselStatus : null;
            ElementalTransfer.EjectSettled(victim, packedPetals, impactVelocity);
        }

        void OnValidate()
        {
            vesselImpactor ??= GetComponent<VesselImpactor>();
        }
    }
}