using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// AI boost for a hull whose boost is BOUGHT WITH THE DRIFT (<see cref="ChargeBoostActionSO"/> -
    /// the Dolphin): hold drift to bank charge, release to discharge it as speed
    /// (<c>DOLPHIN_ENERGY_ECONOMY.md §2</c>).
    ///
    /// <para><b>What was wrong without it.</b> The autopilot already drifts - its commit loop holds
    /// the drift while the course is locked on the objective, which banks the charge - but it lets
    /// go only when the course has swung OFF the objective: after it has passed through it, i.e. at
    /// the start of the turn onto the next one. Every discharge was therefore spent widening a
    /// corner, which is the one place the boost buys nothing.</para>
    ///
    /// <para><b>What this does.</b> Once the meter is full enough and the commit drift is still
    /// lined up with a real straight ahead of it, it asks the pilot to let go of the drift NOW, so
    /// the discharge runs down the straight at the objective, and it holds the next commit off
    /// until the discharge has run out (a re-drift cancels a running discharge). Charging stays the
    /// commit loop's job, exactly as before. No ability is pressed here, so nothing is taken from
    /// the cycler.</para>
    ///
    /// <para>A commit drift is also how the AI AIMS the Dolphin's crystal blast (the nose swings onto
    /// a rival or a mass cluster while the course stays on the crystal). Releasing it early trades
    /// that aim for speed, so modes scored by the blast list themselves in disabledInModes.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "ChargeBoostAIPolicy", menuName = "ScriptableObjects/AI/Boost Policy/Charge Boost")]
    public sealed class ChargeBoostAIPolicySO : AIBoostPolicySO
    {
        [Header("Charge")]
        [Tooltip("Spend once the boost meter (0..1) is at least this full. The peak speed is the " +
                 "SQUARE of the charge term (DOLPHIN_ENERGY_ECONOMY.md §2), so a part-filled meter is " +
                 "worth much less than its fraction - spend near full.")]
        [SerializeField, Range(0.05f, 1f)] float spendAtCharge01 = 0.95f;
        [Tooltip("Longest the pilot is kept off its commit drift after a release, seconds - a safety " +
                 "stop in case the discharge-ended state is never seen. Above the authored discharge " +
                 "time (2.5 s) plus its 1 s recharge cooldown.")]
        [SerializeField, Min(0.5f)] float maxHoldOffSeconds = 4f;

        public override AIBoostDriver CreateDriver(IVesselStatus status, ActionExecutorRegistry executors)
        {
            var handler = status?.ActionHandler;
            if (handler == null || !handler.TryGetBoundAction<ChargeBoostActionSO>(out var action, out _)) return null;
            return new Driver(this, action, status);
        }

        sealed class Driver : AIBoostDriver
        {
            const float DischargeStartGraceSeconds = 0.25f;

            readonly ChargeBoostAIPolicySO _policy;
            readonly ChargeBoostActionSO _action;
            readonly IVesselStatus _status;

            bool _holdOff;
            bool _releaseRequested;
            float _releasedAt;

            public Driver(ChargeBoostAIPolicySO policy, ChargeBoostActionSO action, IVesselStatus status)
            {
                _policy = policy;
                _action = action;
                _status = status;
            }

            public override bool HoldsOffCommit => _holdOff;

            public override bool TakeCommitReleaseRequest()
            {
                if (!_releaseRequested) return false;
                _releaseRequested = false;
                return true;
            }

            public override void Tick(in AIBoostContext c)
            {
                float now = Time.time;

                if (_holdOff)
                {
                    // Hold the next commit off for as long as the charge is being spent. The
                    // discharge flag rises on the release itself; the grace covers the frame order
                    // between this pilot's release and the executor starting its routine.
                    bool discharging = _status.IsChargedBoostDischarging;
                    float since = now - _releasedAt;
                    if ((!discharging && since > DischargeStartGraceSeconds) || since > _policy.maxHoldOffSeconds)
                        _holdOff = false;
                    return;
                }

                // Only a drift the commit loop is holding, still locked on the objective (the course
                // IS the heading while drifting), with a straight ahead worth spending on.
                if (!c.CommitDriftHeld || !_status.IsDrifting) return;
                if (c.Restricted || c.BreakingOrbit) return;
                if (c.AngleToTarget > _policy.AlignDegrees) return;
                if (!_policy.HasRunway(c)) return;
                if (Charge01() < _policy.spendAtCharge01) return;

                _releaseRequested = true;
                _holdOff = true;
                _releasedAt = now;
            }

            public override void Release()
            {
                _holdOff = false;
                _releaseRequested = false;
            }

            float Charge01()
            {
                var resources = _status.ResourceSystem;
                if (!resources) return 0f;
                int index = _action.BoostResourceIndex;
                if ((uint)index >= (uint)resources.Resources.Count) return 0f;
                var res = resources.Resources[index];
                return res != null ? Mathf.Clamp01(res.CurrentAmount) : 0f;
            }
        }
    }
}
