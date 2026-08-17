using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Elemental integration (ecosystem roadmap): every LIVING fauna's embedded heart grants
    /// its elemental value to ALL vessels of the fauna's domain, and that power is lost the
    /// moment the fauna dies — the heart drops as a collectible crystal carrying the very
    /// same value. Kill + collect your own domain's fauna and you break even personally
    /// while your allies lose the buff; kill an opposing domain's fauna and you deny their
    /// buff AND can steal the value by collecting the drop.
    ///
    /// Value symmetry is structural, not tuned: each heart contributes
    /// SkimmerAdjustElementLevelByCrystalEffectSO.ComputeLevelGain(heart world scale, …) read
    /// from the effect array actually wired on the heart's own impactor — the EXACT effects
    /// the skim-collect path executes, so a heart whose drop cannot repay the value grants
    /// nothing. The buff keys off Fauna.LiveHeart, which nulls at the exact ActivateCrystal
    /// moment the drop becomes collectible. Zero new tunables — the existing crystal knobs
    /// govern both sides: levelPerUnitScale and maxLevelGainPerCrystal on the collect effect,
    /// and the heart's world size, which is the ONE level curve on ElementalCrystalSet
    /// (Docs/ECOSYSTEM.md §33) rather than a per-species number. That uniformity matters most
    /// here: before it, a domain fielding tadpoles (heart world scale 0.7) drew a fraction of
    /// the buff a domain fielding brittlestars (2.5) did for the same creature count, and
    /// nobody had authored that difference on purpose.
    ///
    /// One system per scene, hosted by the first Cell to initialize (EnsureExists) so it runs
    /// wherever fauna live — game scenes and Menu_Main freestyle alike (one HyperSea, one rule
    /// set). Applies via the ResourceSystem's dedicated fauna-buff layer (single-writer: this
    /// class), which obeys the maintained-mechanism law: the held layer SUSTAINS at most
    /// level 10, and pool increases above that arrive as temporary spikes (up to 15) that
    /// drain back down — every wave is felt, and the drain restores headroom to feel the
    /// next. Two triggers share one sweep: CellRuntimeDataSO.OnFaunaHeartsChanged (raised at
    /// lineage-assign and death) lands grants/revocations within a frame, and a periodic
    /// reconcile sweep (updateInterval) tracks heart growth, late-spawning vessels, vessel
    /// swaps, and domain re-picks (player.Domain read live each tick).
    ///
    /// Collider budget: zero — no colliders, no physics queries; a 1 Hz iteration over the
    /// existing Cell.LiveFauna registries and gameData.Players.
    ///
    /// Multiplayer caveat: fauna are client-local (no NetworkObject) and element levels are
    /// not replicated, so peers can disagree on exact buff values — the same accepted
    /// divergence the fauna simulation itself has. Each client is self-consistent.
    /// </summary>
    public class DomainFaunaBuffSystem : MonoBehaviour
    {
        [Header("Update Settings")]
        [Tooltip("How often (in seconds) the reconcile sweep re-sums the living fauna hearts " +
                 "and re-applies buffs. Spawn/death re-sums are event-driven and don't wait.")]
        [SerializeField] float updateInterval = 1f;

        [Header("Debug")]
        [SerializeField] bool debugLogging;

        [Inject] GameDataSO gameData;

        // Indexed by (int)Element - 1: Charge, Mass, Space, Time.
        static readonly Element[] AllElements =
            { Element.Charge, Element.Mass, Element.Space, Element.Time };

        // Per-domain summed heart values for the playable domains. Blue (the neutral
        // sentinel) grants and receives nothing.
        readonly Dictionary<Domains, float[]> _pool = new()
        {
            { Domains.Jade, new float[4] },
            { Domains.Ruby, new float[4] },
            { Domains.Gold, new float[4] },
        };

        CellRuntimeDataSO _runtime;
        float _lastUpdateTime;
        bool _sweepDirty;
        bool _warnedMissingGameData;

        /// <summary>
        /// Guarantees a scene with a living Cell has the buff system. A scene-authored
        /// instance is respected as-is; otherwise one is added to the host (the first cell
        /// to initialize). Every cell passes the shared runtime SO so the hearts-changed
        /// event is attached regardless of which cell won the race.
        /// </summary>
        public static DomainFaunaBuffSystem EnsureExists(
            GameObject host, GameDataSO gameData, CellRuntimeDataSO runtime)
        {
            var existing = FindFirstObjectByType<DomainFaunaBuffSystem>(FindObjectsInactive.Include);
            if (existing)
            {
                if (!existing.gameData) existing.gameData = gameData;
                existing.AttachRuntime(runtime);
                return existing;
            }

            var system = host.AddComponent<DomainFaunaBuffSystem>();
            system.gameData = gameData;
            system.AttachRuntime(runtime);
            CSDebug.Log("[DomainFaunaBuffSystem] Auto-created — living fauna hearts now empower their domain's vessels.");
            return system;
        }

        // Subscribes to the shared runtime SO's hearts-changed event (idempotent) so spawn
        // grants and death revocations land on the next Update instead of the next sweep.
        void AttachRuntime(CellRuntimeDataSO runtime)
        {
            if (!runtime || _runtime == runtime) return;
            if (_runtime) _runtime.OnFaunaHeartsChanged.OnRaised -= MarkPoolDirty;
            _runtime = runtime;
            _runtime.OnFaunaHeartsChanged.OnRaised += MarkPoolDirty;
        }

        void MarkPoolDirty() => _sweepDirty = true;

        void OnDestroy()
        {
            if (_runtime) _runtime.OnFaunaHeartsChanged.OnRaised -= MarkPoolDirty;
        }

        void Update()
        {
            // AddComponent runs OnEnable before EnsureExists can assign gameData, so the
            // missing-reference check lives here (first Update is after the assignment).
            if (gameData == null)
            {
                if (!_warnedMissingGameData)
                {
                    _warnedMissingGameData = true;
                    CSDebug.LogError("[DomainFaunaBuffSystem] GameDataSO is not assigned!");
                }
                return;
            }
            if (!_sweepDirty && Time.time - _lastUpdateTime < updateInterval) return;
            _sweepDirty = false;
            _lastUpdateTime = Time.time;

            RebuildPool();
            ApplyBuffs();
        }

        void OnDisable()
        {
            ClearAllFaunaBuffs();
        }

        // Sums every living heart's would-be collection gain into its domain's per-element pool.
        void RebuildPool()
        {
            foreach (var perElement in _pool.Values)
                Array.Clear(perElement, 0, perElement.Length);

            var cells = Cell.ActiveCellsSnapshot;
            for (int c = 0; c < cells.Count; c++)
            {
                var cell = cells[c];
                if (!cell) continue;

                var fauna = cell.LiveFauna;
                for (int i = 0; i < fauna.Count; i++)
                {
                    var creature = fauna[i];
                    if (!creature) continue;

                    var heart = creature.LiveHeart;
                    if (!heart || !heart.crystalProperties.IsElemental) continue;
                    if (!_pool.TryGetValue(creature.Domain, out var perElement)) continue;

                    int elementIndex = (int)heart.crystalProperties.Element - 1;
                    if (elementIndex < 0 || elementIndex >= perElement.Length) continue;

                    perElement[elementIndex] += ComputeHeartValue(heart);
                }
            }
        }

        void ApplyBuffs()
        {
            var players = gameData.Players;
            if (players == null) return;

            for (int p = 0; p < players.Count; p++)
            {
                var player = players[p];
                var rs = GetResourceSystem(player);
                if (rs == null) continue;

                // Domain is read live every tick so a domain re-pick re-keys the buff on
                // the next sweep. Blue (no team) has no pool entry → all buffs go to 0.
                _pool.TryGetValue(player.Domain, out var perElement);
                for (int i = 0; i < AllElements.Length; i++)
                    rs.SetFaunaBuffModifier(AllElements[i], perElement?[i] ?? 0f);

                if (debugLogging && perElement != null)
                    CSDebug.Log($"[DomainFaunaBuffSystem] {player.Name} ({player.Domain}): " +
                                $"C={perElement[0]:F2} M={perElement[1]:F2} " +
                                $"S={perElement[2]:F2} T={perElement[3]:F2}");
            }
        }

        void ClearAllFaunaBuffs()
        {
            var players = gameData ? gameData.Players : null;
            if (players == null) return;
            foreach (var player in players)
            {
                var rs = GetResourceSystem(player);
                if (rs != null) rs.ClearFaunaBuffModifiers();
            }
        }

        // The player→ResourceSystem accessor, hardened for the vessel-swap window: during a
        // menu swap the player can hold a destroyed-but-referenced vessel for a few frames,
        // and VesselStatus.ResourceSystem GetOrAdds on the dead GameObject (throws). The
        // UnityEngine.Object aliveness check filters that window; plain C# test doubles that
        // implement the interface without a Unity backing pass straight through.
        static ResourceSystem GetResourceSystem(IPlayer player)
        {
            var status = player?.Vessel?.VesselStatus;
            if (status == null) return null;
            if (status is UnityEngine.Object statusObject && !statusObject) return null;
            return status.ResourceSystem;
        }

        /// <summary>
        /// What this heart's dropped crystal would grant on collection — the EXACT mirror of
        /// ElementalCrystalImpactor.AcceptImpactee: only the effects wired on the heart's own
        /// impactor run at collect time, so only those fund the buff (summed, because
        /// AcceptImpactee executes EVERY wired effect). A heart whose drop cannot repay the
        /// value (no impactor, or no level effect in its wired array) grants nothing, keeping
        /// the symmetry structural even for misconfigured prefabs.
        /// </summary>
        static float ComputeHeartValue(Crystal heart)
        {
            var impactor = heart.GetComponentInChildren<ElementalCrystalImpactor>(true);
            if (!impactor) return 0f;

            var effects = impactor.CollectionEffects;
            if (effects == null) return 0f;

            float total = 0f;
            for (int i = 0; i < effects.Length; i++)
                if (effects[i] is SkimmerAdjustElementLevelByCrystalEffectSO levelEffect)
                    total += SkimmerAdjustElementLevelByCrystalEffectSO.ComputeLevelGain(
                        heart.transform.lossyScale.x,
                        levelEffect.LevelPerUnitScale, levelEffect.MaxLevelGainPerCrystal);
            return total;
        }
    }
}
