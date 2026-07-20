using System.Collections.Generic;
using CosmicShore.Data;
using Unity.Netcode;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Base for rule-delegated objective monitors (HexRace/CrystalCapture crystals, Joust
    /// jousts, NucleusRush waves). Owns, BY TYPE, the trio every objective monitor used to
    /// hand-copy (AUDIT 3.4):
    ///
    /// 1. The server-only end condition - sealed: the turn ends when the mode's
    ///    <see cref="ScoringRuleSO.IsObjectiveReached"/> reports a domain reached the target.
    /// 2. The "remaining" display - the LOCAL player's DOMAIN deficit via
    ///    <see cref="ScoringRuleSO.Remaining"/>.
    /// 3. The B15 roster-subscription lifecycle (Docs/ScoringSystem/BUGS.md): subclasses
    ///    attach per-stats handlers only through <see cref="SubscribeRoster"/>; detachment
    ///    runs off the base's own-record list (never gameData.RoundStatsList, which
    ///    SceneLoader clears before old-scene destruction) in StopMonitor and a SEALED
    ///    OnDestroy - so no subclass can reintroduce the leak.
    ///
    /// Plus the optional server→clients target-sync leg (a NetworkVariable published into
    /// a GameDataSO target field) shared by the crystal and wave monitors; Joust's
    /// per-peer-constant resolve simply never calls it.
    /// </summary>
    public abstract class ObjectiveTurnMonitor : TurnMonitor
    {
        // Stats this monitor actually subscribed to (B15 own-record).
        readonly List<IRoundStats> _subscribedStats = new();

        // Optional target-sync leg. Never written for monitors whose target is a
        // per-peer constant (Joust) - an unwritten NetworkVariable replicates nothing.
        readonly NetworkVariable<int> _netTarget = new(0);

        protected virtual void OnEnable()
        {
            _netTarget.OnValueChanged += OnTargetSynced;
        }

        protected virtual void OnDisable()
        {
            _netTarget.OnValueChanged -= OnTargetSynced;
        }

        void OnTargetSynced(int previousValue, int newValue)
        {
            if (newValue > 0)
                PublishTarget(newValue);
        }

        /// <summary>
        /// Server: replicate the resolved target and publish it locally. Call from
        /// StartMonitor after resolving the count (EndConditionOverridesSO - the
        /// Tools &gt; Cosmic Shore &gt; End Game Conditions authority). On clients this
        /// re-publishes an already-replicated value for late monitor starts.
        /// </summary>
        protected void SyncTarget(int resolvedTarget)
        {
            if (IsServer)
            {
                _netTarget.Value = resolvedTarget;
                PublishTarget(resolvedTarget);
            }
            else if (_netTarget.Value > 0)
            {
                PublishTarget(_netTarget.Value);
            }
        }

        /// <summary>
        /// Write the synced target into its GameDataSO field (CrystalTargetCount,
        /// GoalTargetCount, ...). No-op default for monitors that don't use the leg.
        /// </summary>
        protected virtual void PublishTarget(int value) { }

        // ── End condition (sealed - the rule is the single authority) ────────────

        public sealed override bool CheckForEndOfTurn()
        {
            if (!IsServer) return false;

            // End condition delegated to the mode's ScoringRule: an active domain's summed
            // metric reaching the target. Domain teammates finish the objective together.
            return gameData.ScoringRule.IsObjectiveReached(gameData, out _);
        }

        // ── Remaining display ────────────────────────────────────────────────────

        /// <summary>
        /// Raises the turn-monitor display with the LOCAL player's DOMAIN deficit (the
        /// rule owns target + sum). A null local player resolves to Domains.Blue
        /// (sum 0 → full target remaining).
        /// </summary>
        protected void RaiseRemainingUI()
        {
            if (!onUpdateTurnMonitorDisplay || gameData.ScoringRule == null) return;

            int remaining = gameData.ScoringRule.Remaining(
                gameData, gameData.LocalPlayer?.Domain ?? Domains.Blue);
            onUpdateTurnMonitorDisplay.Raise(remaining.ToString());
        }

        // ── B15 roster-subscription lifecycle (owned by the base, by type) ───────

        /// <summary>Attach this monitor's per-stats handlers to one roster entry.</summary>
        protected virtual void AttachStatsHandlers(IRoundStats stats) { }

        /// <summary>Detach exactly what <see cref="AttachStatsHandlers"/> attached.</summary>
        protected virtual void DetachStatsHandlers(IRoundStats stats) { }

        /// <summary>
        /// Subscribes the handlers across the current roster, once per stats record
        /// (idempotent - StartMonitor can run more than once per turn).
        /// </summary>
        protected void SubscribeRoster()
        {
            foreach (var stats in gameData.RoundStatsList)
            {
                if (stats == null || _subscribedStats.Contains(stats)) continue;
                AttachStatsHandlers(stats);
                _subscribedStats.Add(stats);
            }
        }

        void DetachAll()
        {
            foreach (var stats in _subscribedStats)
            {
                if (stats == null) continue;
                DetachStatsHandlers(stats);
            }
            _subscribedStats.Clear();
        }

        public override void StopMonitor()
        {
            DetachAll();
            base.StopMonitor();
        }

        /// <summary>
        /// SEALED safety net: detaching from the persistent RoundStats must never depend
        /// on the turn ending or on a subclass remembering to override this (B15).
        /// </summary>
        public sealed override void OnDestroy()
        {
            DetachAll();
            base.OnDestroy();
        }
    }
}
