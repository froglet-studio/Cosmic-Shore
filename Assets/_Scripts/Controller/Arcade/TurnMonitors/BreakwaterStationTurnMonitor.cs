using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Turn monitor for Breakwater. The STATION COUNT - how many stations the course has, and
    /// therefore how many a pilot must thread to finish it - is resolved at
    /// <see cref="StartMonitor"/> from <see cref="EndConditionOverridesSO"/> (FrogletTools &gt;
    /// Game Modes &gt; End Game Conditions; never a per-scene field), synced to every client via
    /// NetworkVariable, and published to <see cref="GameDataSO.SwitchTargetCount"/>. Structural
    /// clone of <see cref="RaceGateTurnMonitor"/> reading its own overrides key and its own
    /// controller.
    ///
    /// <para>It is a SEPARATE CLASS rather than a reuse, and that is a measured conclusion rather
    /// than an oversight: Switchback's monitor names <c>GetSwitchbackGateTarget()</c> and finds a
    /// <c>SwitchbackController</c>, both of which are the two facts that would have to become
    /// parameters. Making them parameters is a shared base class whose only members are "which
    /// override key" and "which controller type" - a generic seam holding two constants, in
    /// exchange for a second file both modes must be read through. The metric underneath is
    /// genuinely shared and IS reused: a station is a switch threaded in order, so this publishes
    /// the same <c>GameDataSO.SwitchTargetCount</c> and the same
    /// <c>ScoringMetric.SwitchesThreaded</c> the goal stack and the launch panel already draw.</para>
    ///
    /// <para><b>Publishing the target is load-bearing, not cosmetic.</b>
    /// <c>MiniGameHUD.RefreshGoalStack</c> draws NOTHING - silently, no warning, no placeholder
    /// row - when the target is 0, so a monitor that computed the number correctly and never wrote
    /// it back would ship a mode whose objective readout is simply absent, on every client, with
    /// the race itself working perfectly. That failure looks like "the goal stack is not
    /// implemented for this mode" rather than like a missing assignment, which is why the write is
    /// duplicated on both the server branch and the late-start client branch below.</para>
    ///
    /// <para><b>The COURSE is the authority, not the override.</b> The controller asks the same
    /// overrides key for how many stations to lay, but a shell too tight for that many makes it
    /// back off (<c>BreakwaterController.GenerateAndBroadcastCourse</c>), and ~0.1% of seeds fail
    /// to walk at all - so the laid count can legitimately come in under the authored one. A
    /// target naming a station the course does not contain is unreachable, and an unreachable
    /// target is a match that cannot end: every pilot threads every station that exists, nobody
    /// satisfies <c>IsObjectiveReached</c>, and the turn runs forever with no clock to catch it
    /// (this mode races to a COUNT and authors no time monitor). So this reads the controller's
    /// <see cref="BreakwaterController.AuthoritativeStationCount"/> and falls back to the override
    /// only before the course exists. The two can then never disagree by construction rather than
    /// by agreement, and a disagreement is WARNED so a mis-authored override surfaces as a line in
    /// the log rather than as a slightly shorter race nobody notices.</para>
    ///
    /// <para>The display channel publishes the LOCAL player's OWN remaining stations
    /// (<c>ScoringRuleSO.RemainingForPlayer</c>). This mode folds a domain by its BEST pilot
    /// (<c>ScoringMetrics.BestByDomain</c>, inherited whole from the Switchback scoring rule), so
    /// a domain reading here would show a trailing teammate the ace's progress while their own
    /// objective arrow pointed several stations back - the readout and the arrow would be
    /// describing two different pilots.</para>
    ///
    /// <para><b><see cref="TurnMonitor.PublishesSecondsRemaining"/> is INHERITED false, not
    /// overridden.</b> Said here because the absence is the decision: that payload is a COUNT of
    /// stations remaining, and answering true makes <c>MiniGameHUD</c> draw the CLOCK row instead
    /// of the objective row - m:ss formatting, no glyph, no target - over a number that is
    /// neither seconds nor a time. This mode has no clock at all; it ends when a domain's lead
    /// runner threads the last station. An override returning the base default would also be the
    /// only structural difference from <see cref="RaceGateTurnMonitor"/>, which reads as a
    /// behavioural divergence between two monitors that are deliberately identical.</para>
    /// </summary>
    public class BreakwaterStationTurnMonitor : TurnMonitor
    {
        readonly NetworkVariable<int> _netStationTarget = new(0);

        void OnEnable()
        {
            _netStationTarget.OnValueChanged += OnStationTargetSynced;
        }

        void OnDisable()
        {
            _netStationTarget.OnValueChanged -= OnStationTargetSynced;
        }

        void OnStationTargetSynced(int previousValue, int newValue)
        {
            if (newValue > 0)
                gameData.SwitchTargetCount = newValue;
        }

        public override void StartMonitor()
        {
            base.StartMonitor();

            if (IsServer)
            {
                var overrides = EndConditionOverridesSO.Instance;
                int target = overrides != null
                    ? overrides.GetBreakwaterCrossingTarget()
                    : BreakwaterCourseSettings.CrossingTarget(
                          EndConditionOverridesSO.DefaultBreakwaterStationTarget,
                          BreakwaterCourseSettings.DefaultLaps);

                // The course has been generated since OnNetworkSpawn, so its length is known and
                // is the honest target - see the class summary. One scene lookup at turn start,
                // never a hot path.
                //
                // THE TARGET IS CROSSINGS, NOT STATIONS. A course is a start gate plus a closed
                // circuit flown once per lap, so fifteen stations are twenty-nine crossings; the
                // controller folds the two together in BreakwaterController.CrossingTarget so this
                // reads ONE number and cannot re-derive the laps arithmetic differently from the
                // detector that pays it.
                var controller = FindFirstObjectByType<BreakwaterController>(FindObjectsInactive.Include);
                int laid = controller != null ? controller.CrossingTarget : 0;
                if (laid > 0 && laid != target)
                {
                    CSDebug.LogWarning($"[BreakwaterStationMonitor] Authored target {target} crossings " +
                                       $"but the course is worth {laid} - racing to {laid}, the number " +
                                       "of crossings the stations that actually exist can offer.");
                    target = laid;
                }
                else if (laid > 0)
                {
                    target = laid;
                }

                _netStationTarget.Value = target;
                gameData.SwitchTargetCount = target;

                CSDebug.Log($"[BreakwaterStationMonitor] Server set station target: {target}");
            }
            else if (_netStationTarget.Value > 0)
            {
                // Late start on a client that already replicated the value.
                gameData.SwitchTargetCount = _netStationTarget.Value;
            }

            UpdateRemainingUI();
        }

        public override bool CheckForEndOfTurn()
        {
            if (!IsServer) return false;

            // Delegated to the mode's ScoringRule: the first active domain whose LEAD RUNNER has
            // threaded every station wins. The rule refuses to answer true on a target of 0, so a
            // client that has not yet replicated the value cannot end the turn early - and this
            // branch is server-only anyway, where the value was written above.
            return gameData.ScoringRule.IsObjectiveReached(gameData, out _);
        }

        protected override void RestrictedUpdate()
        {
            // Polled at _updateInterval rather than subscribed to each RoundStats' change event:
            // those live on the persistent Player object and a turn-end-gated unsubscribe leaks
            // into the next match (Docs/ScoringSystem/BUGS.md B15).
            UpdateRemainingUI();
        }

        void UpdateRemainingUI()
        {
            if (!onUpdateTurnMonitorDisplay || gameData.ScoringRule == null) return;

            int remaining = gameData.ScoringRule.RemainingForPlayer(
                gameData, gameData.LocalPlayer?.RoundStats);
            onUpdateTurnMonitorDisplay.Raise(remaining.ToString());
        }
    }
}
