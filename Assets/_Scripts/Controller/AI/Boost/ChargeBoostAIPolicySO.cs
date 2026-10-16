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
    /// <para><b>Re-drift at the peak</b> (2026-10-17). The drift FREEZES the speed it starts at
    /// (<c>VesselTransformer.holdSpeedWhileDrifting</c>, on for the Dolphin) and banks the charge again from what is
    /// left; a discharge climbs toward <c>cruise × m²</c> (347 u/s off a full meter) while its target decays with the
    /// meter. So the fastest a Dolphin can carry is the PEAK of a discharge, and the moment to drift again is the moment
    /// the speed stops climbing (the transformer's own target has fallen to the speed) - not when the meter is empty,
    /// which is where this driver used to wait, re-freezing a speed already sliding back toward cruise. On a straight
    /// the change is worth about 50%: 303 u/s average against 203 in the offline model
    /// (<c>Tools/Build/dolphin_drive_sim.py</c>). Every discharge is held to its peak this way, the ones this driver
    /// asks for on a straight and the ones the commit loop starts when the course swings off at a corner.</para>
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
        [Tooltip("Drift again once the discharge's speed is within this many u/s of the speed it is climbing toward " +
                 "(the transformer's target, which falls as the meter drains): the peak. The drift freezes that speed.")]
        [SerializeField, Min(0f)] float peakMarginUnits = 3f;
        [Tooltip("Hold the drift off at least this long after a discharge starts, seconds - the target rises on the " +
                 "release itself, and a frame-order lag must not read as the peak.")]
        [SerializeField, Min(0f)] float minDischargeSeconds = 0.2f;
        [Tooltip("Spend on a straight only when the discharge can lift the speed at least this many u/s above the speed " +
                 "the drift is holding (its ceiling is cruise × m², m = 1 + (max - 1) × meter) - a Dolphin already " +
                 "sliding near the top gains nothing from another release but the engage sound.")]
        [SerializeField, Min(0f)] float minPulseGainUnits = 30f;

        public override AIBoostDriver CreateDriver(IVesselStatus status, ActionExecutorRegistry executors)
        {
            var handler = status?.ActionHandler;
            if (handler == null || !handler.TryGetBoundAction<ChargeBoostActionSO>(out var action, out _)) return null;
            return new Driver(this, action, status);
        }

        /// <summary>
        /// The discharge has reached its peak: past <see cref="minDischargeSeconds"/>, and the speed is within
        /// <see cref="peakMarginUnits"/> of the target it climbs toward (the target falls as the meter drains, the
        /// speed rises toward it, and they meet at the top). Pure, for the tests.
        /// </summary>
        public bool AtPeak(float secondsDischarging, float speed, float trackedTarget) =>
            secondsDischarging >= minDischargeSeconds && speed >= trackedTarget - peakMarginUnits;

        /// <summary>
        /// The most a discharge of <paramref name="charge01"/> can climb to from a <paramref name="cruiseTarget"/>:
        /// both boost terms (<c>BoostMultiplier</c> and the pinned <c>ChargedBoostCharge</c>) start at
        /// m = 1 + (<paramref name="maxMultiplier"/> - 1) × charge, so the ceiling is cruise × m²
        /// (DOLPHIN_ENERGY_ECONOMY.md §2: 68 × 2.259² = 347 off a full meter).
        /// </summary>
        public static float DischargeCeiling(float cruiseTarget, float maxMultiplier, float charge01)
        {
            float m = 1f + (maxMultiplier - 1f) * Mathf.Clamp01(charge01);
            return cruiseTarget * m * m;
        }

        /// <summary>A release from <paramref name="speed"/> can lift it by <see cref="minPulseGainUnits"/>.</summary>
        public bool PulseWorthIt(float speed, float cruiseTarget, float maxMultiplier, float charge01) =>
            DischargeCeiling(cruiseTarget, maxMultiplier, charge01) - speed >= minPulseGainUnits;

        sealed class Driver : AIBoostDriver
        {
            const float DischargeStartGraceSeconds = 0.25f;

            readonly ChargeBoostAIPolicySO _policy;
            readonly ChargeBoostActionSO _action;
            readonly IVesselStatus _status;

            bool _holdOff;
            bool _releaseRequested;
            float _releasedAt;
            bool _wasDischarging;

            public Driver(ChargeBoostAIPolicySO policy, ChargeBoostActionSO action, IVesselStatus status)
            {
                _policy = policy;
                _action = action;
                _status = status;
            }

            public override bool HoldsOffCommit => _holdOff;

            public override string Status =>
                _holdOff ? $"discharging - drifts again at the peak ({_status.Speed:F0} u/s)"
                : _status.IsDrifting ? $"charging in the drift ({Charge01():P0}, spends at {_policy.spendAtCharge01:P0})"
                : "waiting for a commit drift to charge";

            public override bool TakeCommitReleaseRequest()
            {
                if (!_releaseRequested) return false;
                _releaseRequested = false;
                return true;
            }

            public override void Tick(in AIBoostContext c)
            {
                float now = Time.time;
                bool discharging = _status.IsChargedBoostDischarging;

                // A discharge this driver did not ask for (the commit loop let go at a corner) is held to its peak too.
                if (discharging && !_wasDischarging && !_holdOff)
                {
                    _holdOff = true;
                    _releasedAt = now;
                }
                _wasDischarging = discharging;

                if (_holdOff)
                {
                    // Hold the next commit off until the discharge peaks: the drift then freezes the most speed this
                    // charge can buy and banks again from what is left. The discharge flag rises on the release
                    // itself; the grace covers the frame order between this pilot's release and the executor
                    // starting its routine.
                    float since = now - _releasedAt;
                    var transformer = _status.VesselTransformer;
                    bool peaked = discharging && transformer != null
                                  && _policy.AtPeak(since, _status.Speed, transformer.CurrentThrottleTarget);
                    if (peaked || (!discharging && since > DischargeStartGraceSeconds) || since > _policy.maxHoldOffSeconds)
                        _holdOff = false;
                    return;
                }

                // Only a drift the commit loop is holding, still locked on the objective (the course
                // IS the heading while drifting), with a straight ahead worth spending on.
                if (!c.CommitDriftHeld || !_status.IsDrifting) return;
                if (c.Restricted || c.BreakingOrbit) return;
                if (c.AngleToTarget > _policy.AlignDegrees) return;
                // The discharge fires along the NOSE, and in a drift the nose is free of the course: spend only with the
                // nose on the objective too, or the boost throws the hull wide of the ring (2026-10-14 Regatta audit).
                if (Vector3.Angle(c.Forward, c.ToTarget) > _policy.AlignDegrees) return;
                if (!_policy.HasRunway(c)) return;
                float charge = Charge01();
                if (charge < _policy.spendAtCharge01) return;
                // While drifting the boost terms are cleared, so the transformer's target is the plain cruise.
                var cruise = _status.VesselTransformer;
                if (cruise != null && !_policy.PulseWorthIt(c.Speed, cruise.CurrentThrottleTarget, _action.MaxBoostMultiplier, charge))
                    return;

                _releaseRequested = true;
                _holdOff = true;
                _releasedAt = now;
            }

            public override void Release()
            {
                _holdOff = false;
                _releaseRequested = false;
                _wasDischarging = false;
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
