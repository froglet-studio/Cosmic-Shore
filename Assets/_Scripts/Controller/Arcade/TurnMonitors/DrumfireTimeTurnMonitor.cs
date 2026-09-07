using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Netcode;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Turn monitor for Drumfire: the match is a fixed stretch of TIME, not a race to a count.
    ///
    /// <para>The length is resolved at <see cref="StartMonitor"/> from
    /// <see cref="EndConditionOverridesSO"/> (FrogletTools &gt; Game Modes &gt; End Game
    /// Conditions; never a per-scene field), exactly like every other mode's end-game number -
    /// <see cref="RampagePrismTurnMonitor"/> is the sibling this is shaped after. It is then
    /// replicated, because each peer runs its own copy of the base class's elapsed-time loop and
    /// two peers reading different durations would disagree about when their local monitors stop.
    /// The turn END is still server-authoritative either way
    /// (<c>MultiplayerMiniGameControllerBase.HandleTurnEnd</c> is server-gated).</para>
    ///
    /// <para><see cref="TimeBasedTurnMonitor.PublishesSecondsRemaining"/> stays true, so the
    /// top bar's goal stack draws a CLOCK row (m:ss, no target, no bar) rather than reading the
    /// payload as an objective count - see Docs/GAME_MODE_TOPBAR.md.</para>
    /// </summary>
    public class DrumfireTimeTurnMonitor : NetworkTimeBasedTurnMonitor
    {
        readonly NetworkVariable<float> _netSeconds = new(0f);

        void OnEnable() => _netSeconds.OnValueChanged += OnSecondsSynced;

        void OnDisable() => _netSeconds.OnValueChanged -= OnSecondsSynced;

        void OnSecondsSynced(float previousValue, float newValue)
        {
            if (newValue > 0f) SetDuration(newValue);
        }

        public override void StartMonitor()
        {
            if (IsServer)
            {
                var overrides = EndConditionOverridesSO.Instance;
                float seconds = overrides != null
                    ? overrides.GetDrumfireSeconds()
                    : EndConditionOverridesSO.DefaultDrumfireSeconds;

                _netSeconds.Value = seconds;
                SetDuration(seconds);

                CSDebug.Log($"[DrumfireTimeMonitor] Server set match length: {seconds:0}s");
            }
            else if (_netSeconds.Value > 0f)
            {
                // Late start on a client that already replicated the value.
                SetDuration(_netSeconds.Value);
            }

            // AFTER the duration is authored: the base resets elapsed time and pushes the first
            // display tick, so seeding it first would broadcast the previous duration's clock.
            base.StartMonitor();
        }
    }
}
