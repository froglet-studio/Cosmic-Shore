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
    /// <para>The omni dispatch (<see cref="VesselImpactor.ExecuteOmniCrystalImpact"/>) runs on every peer, so every peer
    /// counts the same collections toward <see cref="FulmenConfigSO.CrystalsToFire"/>, strikes with the same seed (from the collected crystal's id and pose) and grows the same root bolt; only the server
    /// (or an offline session) ignites prisms.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "FulmenByCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/FulmenByCrystalEffectSO")]
    public sealed class FulmenByCrystalEffectSO : VesselCrystalEffectSO
    {
        [Tooltip("The bolt: reach, chain, budgets, burn and look.")]
        [SerializeField] FulmenConfigSO config;

        public FulmenConfigSO Config => config;

        // Crystals each collector has gathered toward the next bolt (VesselImpactor instance id -> count). Runtime only. The
        // dispatch runs on every peer, so every peer counts the same collections and fires on the same one.
        readonly Dictionary<int, int> _charge = new();

        /// <summary>Crystals <paramref name="vesselImpactor"/> has toward its next bolt (0 after it fires).</summary>
        public int ChargeOf(VesselImpactor vesselImpactor) =>
            vesselImpactor && _charge.TryGetValue(vesselImpactor.GetInstanceID(), out int n) ? n : 0;

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
            if (!Charge(vesselImpactor)) return;

            var status = vesselImpactor.Vessel?.VesselStatus;
            var near = data.Origin.Valid ? data.Origin.Position : vesselImpactor.transform.position;

            SolitonNetwork.ResolveCell(near, out var centre, out float cellRadius);
            float range = config.RootRange(cellRadius);
            int crystalId = data.Origin.Valid ? data.Origin.CrystalId : int.MinValue;

            FulmenSystem.EnsureInstance().Strike(config, centre, range, Seed(data), IsAuthority(),
                status?.PlayerName, status != null ? status.Domain : default, crystalId);
        }

        /// <summary>The same on every peer: the collected crystal's id mixed with its quantised pose.</summary>
        static int Seed(in CrystalImpactData data)
        {
            if (!data.Origin.Valid) return 0x5EED;
            var p = data.Origin.Position;
            int h = FulmenSystem.ChildSeed(data.Origin.CrystalId, Mathf.RoundToInt(p.x));
            h = FulmenSystem.ChildSeed(h, Mathf.RoundToInt(p.y));
            return FulmenSystem.ChildSeed(h, Mathf.RoundToInt(p.z));
        }

        /// <summary>The burn is the server's (or this machine's, with no session running).</summary>
        static bool IsAuthority()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            return nm == null || !nm.IsListening || nm.IsServer;
        }
    }
}
