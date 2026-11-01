using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// FULMEN as an omni-crystal ability, for ANY vessel (<c>Environment/Fulmen/README.md</c>): collecting the crystal sets
    /// off a Fulmen chain at the collector's cell centre (<see cref="SolitonNetwork.ResolveCell"/>), shaped by
    /// <see cref="config"/>. No vessel-specific code: give a vessel Fulmen by adding this asset to its container's
    /// <c>vesselCrystalEffects</c>; take it away by removing it.
    ///
    /// <para><b>Network.</b> The omni dispatch (<see cref="VesselImpactor.ExecuteOmniCrystalImpact"/>) reaches every peer,
    /// but only the SERVER acts on it: it counts the charge toward <see cref="FulmenConfigSO.CrystalsToFire"/> (replicated
    /// on <see cref="NetworkVesselImpactor.FulmenCharge"/>, so a late joiner and a client HUD read the true count), decides
    /// the strike and sends it (<see cref="FulmenNetSync"/>). Clients ignore the dispatch and replay the server's strike,
    /// children and burns. Offline (a solo host) nothing is sent.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "FulmenByCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/FulmenByCrystalEffectSO")]
    public sealed class FulmenByCrystalEffectSO : VesselCrystalEffectSO
    {
        [Tooltip("The bolt: reach, chain, budgets, burn and look.")]
        [SerializeField] FulmenConfigSO config;

        public FulmenConfigSO Config => config;

        // Crystals each collector has gathered toward the next bolt (VesselImpactor instance id -> count). Runtime only,
        // SERVER only in a session (mirrored to the vessel's NetworkVesselImpactor.FulmenCharge).
        readonly Dictionary<int, int> _charge = new();

        /// <summary>Crystals <paramref name="vesselImpactor"/> has toward its next bolt (0 after it fires).</summary>
        public int ChargeOf(VesselImpactor vesselImpactor)
        {
            if (!vesselImpactor) return 0;
            if (FulmenNetSync.IsRemoteClient)
            {
                var relay = vesselImpactor.GetComponent<NetworkVesselImpactor>();
                return relay ? relay.FulmenCharge : 0;
            }
            return _charge.TryGetValue(vesselImpactor.GetInstanceID(), out int n) ? n : 0;
        }

        void OnEnable()
        {
            _charge.Clear();
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        void OnDisable() => SceneManager.sceneUnloaded -= OnSceneUnloaded;

        void OnSceneUnloaded(Scene _) => _charge.Clear();

        /// <summary>Counts this crystal; true (and the count resets) when it is the one that fires.</summary>
        bool Charge(VesselImpactor vesselImpactor)
        {
            int need = config.CrystalsToFire;
            if (need <= 1) return true;
            int id = vesselImpactor.GetInstanceID();
            _charge.TryGetValue(id, out int n);
            if (++n < need) { _charge[id] = n; return false; }
            _charge.Remove(id);
            return true;
        }

        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            if (!vesselImpactor) return;
            if (!config)
            {
                CSDebug.LogWarning($"[Fulmen] '{name}' has no FulmenConfigSO.", this);
                return;
            }
            // a client: the server counts and decides; its strike arrives by RPC (FulmenNetSync.ApplyStrike)
            if (FulmenNetSync.IsRemoteClient) return;

            var relay = vesselImpactor.GetComponent<NetworkVesselImpactor>();
            bool fire = Charge(vesselImpactor);
            if (relay) relay.SetFulmenCharge(ChargeOf(vesselImpactor));
            if (!fire) return;

            var status = vesselImpactor.Vessel?.VesselStatus;
            var near = data.Origin.Valid ? data.Origin.Position : vesselImpactor.transform.position;

            SolitonNetwork.ResolveCell(near, out var centre, out float cellRadius);
            float range = config.RootRange(cellRadius);
            int crystalId = data.Origin.Valid ? data.Origin.CrystalId : int.MinValue;

            string igniter = status?.PlayerName;
            var domain = status != null ? status.Domain : default;
            int seed = Seed(data);

            ushort netId = FulmenNetSync.ServerAllocate(relay, config, centre, igniter, domain);   // 0: no remote client
            if (FulmenSystem.EnsureInstance().Strike(config, centre, range, seed, true, igniter, domain, crystalId, netId)
                && netId != 0)
                FulmenNetSync.ServerSendStrike(netId, seed, range, crystalId);
        }

        /// <summary>The collected crystal's id mixed with its quantised pose (sent to clients in the strike).</summary>
        static int Seed(in CrystalImpactData data)
        {
            if (!data.Origin.Valid) return 0x5EED;
            var p = data.Origin.Position;
            int h = FulmenSystem.ChildSeed(data.Origin.CrystalId, Mathf.RoundToInt(p.x));
            h = FulmenSystem.ChildSeed(h, Mathf.RoundToInt(p.y));
            return FulmenSystem.ChildSeed(h, Mathf.RoundToInt(p.z));
        }
    }
}
