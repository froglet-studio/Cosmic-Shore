using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Netcode;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Turn monitor for Landslide (the Rhino's Space game, LANDSLIDE.md): Cleave's prism monitor with Landslide's own
    /// target. The target - how many hostile prisms a domain's strikes must destroy - is resolved at
    /// <see cref="StartMonitor"/> from <see cref="EndConditionOverridesSO.GetLandslidePrismTarget"/> (FrogletTools >
    /// Game Modes > End Game Conditions; never a per-scene field), replicated to every client, and published to
    /// <see cref="GameDataSO.PrismTargetCount"/>, which the inherited Cleave milestones read. The turn ends (server-side)
    /// when the rule reports an active domain has reached it. One scalar for every rung: the plough scores two prisms a
    /// strike wherever the ribs are, and all four Cleave arenas hold 14,000-16,500 prisms.
    /// </summary>
    public class LandslidePrismTurnMonitor : TurnMonitor
    {
        readonly NetworkVariable<int> _netPrismTarget = new(0);

        void OnEnable()
        {
            _netPrismTarget.OnValueChanged += OnPrismTargetSynced;
        }

        void OnDisable()
        {
            _netPrismTarget.OnValueChanged -= OnPrismTargetSynced;
        }

        void OnPrismTargetSynced(int previousValue, int newValue)
        {
            if (newValue > 0)
                gameData.PrismTargetCount = newValue;
        }

        public override void StartMonitor()
        {
            base.StartMonitor();

            if (IsServer)
            {
                var overrides = EndConditionOverridesSO.Instance;
                int target = overrides != null
                    ? overrides.GetLandslidePrismTarget()
                    : EndConditionOverridesSO.DefaultLandslidePrismTarget;

                _netPrismTarget.Value = target;
                gameData.PrismTargetCount = target;

                CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, $"[LandslidePrismMonitor] Server set arena target: {target}");
            }
            else if (_netPrismTarget.Value > 0)
            {
                // Late start on a client that already replicated the value.
                gameData.PrismTargetCount = _netPrismTarget.Value;
            }

            UpdateRemainingUI();
        }

        public override bool CheckForEndOfTurn()
        {
            if (!IsServer) return false;

            // End condition delegated to the mode's ScoringRule: first active domain whose
            // destruction (HostilePrismsDestroyed) sum reaches the target breaks out and wins.
            return gameData.ScoringRule.IsObjectiveReached(gameData, out _);
        }

        protected override void RestrictedUpdate()
        {
            // Destruction is bursty (a plough down a rib lands several strikes in one frame), but a
            // 1s display tick is plenty - no per-stat event subscriptions to leak
            // (Docs/ScoringSystem/BUGS.md B15 class).
            UpdateRemainingUI();
        }

        void UpdateRemainingUI()
        {
            if (!onUpdateTurnMonitorDisplay || gameData.ScoringRule == null) return;

            int remaining = gameData.ScoringRule.Remaining(
                gameData, gameData.LocalPlayer?.Domain ?? Domains.Blue);
            onUpdateTurnMonitorDisplay.Raise(remaining.ToString());
        }
    }
}
