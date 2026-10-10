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

        // ── Combat petal transfer (IElementalLossRelay) ──────────────────────
        //
        // Element levels are OWNER state: NetElementLevels is owner-write, and a take from any
        // other copy of this hull changes nothing anybody reads. So a combat take is settled
        // here, on the owner, as the attacker's owner saw the hit (ElementalTransfer type doc):
        //
        //   attacker's owner ──RelayTakeToOwner──> [owner here?] settle
        //                    └─CombatTake_ServerRpc─> [server owns it?] settle
        //                                          └─CombatTake_ClientRpc ─(owner only)─> settle
        //   EJECT: settle ── mint locally ── PublishEject_ServerRpc ── PublishEject_ClientRpc ─(not owner)─> mint
        //   STEAL: settle ── payee.RelayGrantToOwner ──> [payee owned here?] grant
        //                                            └─GrantSteal_ServerRpc─> [server owns it?] grant
        //                                                                  └─GrantSteal_ClientRpc ─(owner only)─> grant
        //
        // The ServerRpc + targeted ClientRpc pair is the ExecuteJoust shape above, narrowed to one
        // recipient with ClientRpcParams (MultiplayerMiniGameControllerBase does the same). Every
        // hop runs on the main thread, as every NGO RPC does.

        public bool IsNetworked => IsSpawned;
        public bool IsOwnedHere => IsSpawned && IsOwner;

        public int RelayTakeToOwner(ElementalTransferForm form, int elementMask, float normalizedAmountPerElement,
                                    Vector3 impactVelocity, ElementalDebuffSources source, IElementalLossRelay payee)
        {
            if (normalizedAmountPerElement <= 0f || elementMask == 0) return 0;

            // A steal pays a hull that this relay can name on the wire. Anything else - an eject, or
            // a steal whose payee is not a network hull - ejects, the conserving fallback
            // ElementalTransfer.Apply also takes when a steal has nobody to pay.
            var payeeImpactor = form == ElementalTransferForm.Steal ? payee as NetworkVesselImpactor : null;
            bool steal = payeeImpactor != null && payeeImpactor.IsSpawned;

            if (IsOwner)
                return SettleTakeAsOwner(steal, elementMask, normalizedAmountPerElement, impactVelocity, source,
                                         steal ? payeeImpactor : null);

            if (steal)
                CombatSteal_ServerRpc(elementMask, normalizedAmountPerElement, source, payeeImpactor);
            else
                CombatEject_ServerRpc(elementMask, normalizedAmountPerElement, impactVelocity, source);
            return 0;
        }

        // RequireOwnership = false on every forwarding ServerRpc here: the CALLER is the attacker's
        // owner (or, for a grant, the victim's), which by construction does not own this hull. The
        // server only forwards; the owner's AccrueElementalLoss still clamps the take to what is
        // held and honours a ward.
        [ServerRpc(RequireOwnership = false)]
        void CombatEject_ServerRpc(int elementMask, float normalizedAmountPerElement, Vector3 impactVelocity,
                                   ElementalDebuffSources source)
        {
            // The server owns every AI hull and the host's own; settle without a further hop.
            if (IsOwner)
            {
                SettleTakeAsOwner(false, elementMask, normalizedAmountPerElement, impactVelocity, source, null);
                return;
            }

            CombatEject_ClientRpc(elementMask, normalizedAmountPerElement, impactVelocity, source, ToOwner());
        }

        [ClientRpc]
        void CombatEject_ClientRpc(int elementMask, float normalizedAmountPerElement, Vector3 impactVelocity,
                                   ElementalDebuffSources source, ClientRpcParams rpcParams = default)
        {
            // Ownership can move while the RPC is in flight (Hijack's swap). The pilot who was
            // shot no longer flies this hull, and its levels are no longer this machine's to publish.
            if (!IsOwner) return;
            SettleTakeAsOwner(false, elementMask, normalizedAmountPerElement, impactVelocity, source, null);
        }

        [ServerRpc(RequireOwnership = false)]
        void CombatSteal_ServerRpc(int elementMask, float normalizedAmountPerElement, ElementalDebuffSources source,
                                   NetworkBehaviourReference payeeRef)
        {
            if (IsOwner)
            {
                SettleStealFromWire(elementMask, normalizedAmountPerElement, source, payeeRef);
                return;
            }

            CombatSteal_ClientRpc(elementMask, normalizedAmountPerElement, source, payeeRef, ToOwner());
        }

        [ClientRpc]
        void CombatSteal_ClientRpc(int elementMask, float normalizedAmountPerElement, ElementalDebuffSources source,
                                   NetworkBehaviourReference payeeRef, ClientRpcParams rpcParams = default)
        {
            if (!IsOwner) return;   // ownership moved in flight, as above
            SettleStealFromWire(elementMask, normalizedAmountPerElement, source, payeeRef);
        }

        void SettleStealFromWire(int elementMask, float normalizedAmountPerElement, ElementalDebuffSources source,
                                 NetworkBehaviourReference payeeRef)
        {
            // A payee that despawned in flight cannot be paid; the take then ejects so the petals
            // stay in play rather than vanishing.
            payeeRef.TryGet(out NetworkVesselImpactor payee);
            SettleTakeAsOwner(payee != null, elementMask, normalizedAmountPerElement, Vector3.zero, source, payee);
        }

        int SettleTakeAsOwner(bool steal, int elementMask, float normalizedAmountPerElement, Vector3 impactVelocity,
                              ElementalDebuffSources source, NetworkVesselImpactor payee)
        {
            var victim = VesselStatusHere;
            if (victim == null) return 0;

            uint packed = ElementalTransfer.SettleTake(victim, elementMask, normalizedAmountPerElement, source);
            if (packed == 0u) return 0;   // warded, empty, or still accruing toward a whole petal

            if (steal && payee != null)
            {
                payee.RelayGrantToOwner(packed);
            }
            else
            {
                ElementalTransfer.EjectSettled(victim, packed, impactVelocity);
                PublishEject_ServerRpc(packed, impactVelocity);
            }
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
            ElementalTransfer.EjectSettled(VesselStatusHere, packedPetals, impactVelocity);
        }

        /// <summary>The PAY half of a relayed steal: this hull is the thief, and the victim's owner
        /// has settled what came loose. Granted on this hull's owner only, because a grant to any
        /// other copy is overwritten by the owner's NetElementLevels.</summary>
        public void RelayGrantToOwner(uint packedPetals)
        {
            if (packedPetals == 0u) return;
            if (IsOwner)
            {
                ElementalTransfer.GrantSettled(VesselStatusHere, packedPetals);
                return;
            }
            GrantSteal_ServerRpc(packedPetals);
        }

        [ServerRpc(RequireOwnership = false)]
        void GrantSteal_ServerRpc(uint packedPetals)
        {
            if (IsOwner)
            {
                ElementalTransfer.GrantSettled(VesselStatusHere, packedPetals);
                return;
            }
            GrantSteal_ClientRpc(packedPetals, ToOwner());
        }

        [ClientRpc]
        void GrantSteal_ClientRpc(uint packedPetals, ClientRpcParams rpcParams = default)
        {
            // Ownership moved in flight (Hijack's swap): the pilot who stole no longer flies this
            // hull. The petals already left the victim, so this is the one case that does not
            // conserve; it needs an ownership swap inside one round trip.
            if (!IsOwner) return;
            ElementalTransfer.GrantSettled(VesselStatusHere, packedPetals);
        }

        IVesselStatus VesselStatusHere => vesselImpactor ? vesselImpactor.Vessel?.VesselStatus : null;

        ClientRpcParams ToOwner() => new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
        };

        void OnValidate()
        {
            vesselImpactor ??= GetComponent<VesselImpactor>();
        }
    }
}