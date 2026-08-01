using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Turn monitor for Nucleus Rush ("Brood Rush"). The wave TARGET - how many fauna
    /// waves a domain must claim to win (default 3) - is resolved at
    /// <see cref="StartMonitor"/> from <see cref="EndConditionOverridesSO"/>
    /// (Tools &gt; Cosmic Shore &gt; End Game Conditions; never a per-scene field) and
    /// synced/published to <see cref="GameDataSO.GoalTargetCount"/> via the base target
    /// leg. End condition and the remaining display come from
    /// <see cref="ObjectiveTurnMonitor"/>. Brood points land on the 30s wave cadence, so
    /// the display refreshes on the 1s RestrictedUpdate tick instead of per-stat event
    /// subscriptions (no roster handlers to attach).
    /// </summary>
    public class NucleusRushWaveTurnMonitor : ObjectiveTurnMonitor
    {
        public override void StartMonitor()
        {
            base.StartMonitor();

            var overrides = EndConditionOverridesSO.Instance;
            int target = overrides != null
                ? overrides.GetNucleusRushWaveTarget()
                : EndConditionOverridesSO.DefaultNucleusRushWaveTarget;

            SyncTarget(target);
            if (IsServer)
                CSDebug.Log($"[NucleusRushWaveMonitor] Server set wave target: {target}");

            RaiseRemainingUI();
        }

        protected override void PublishTarget(int value) => gameData.GoalTargetCount = value;

        protected override void RestrictedUpdate()
        {
            RaiseRemainingUI();
        }
    }
}
