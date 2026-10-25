using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// AI boost for a hull whose speed is SKIM ENERGY (the Squirrel: +boost per prism its skimmer
    /// grazes, decaying, reset by a hull ram). Outside Skim Race - whose dedicated
    /// <c>SkimRacePilot</c> plans a skim line - the generic autopilot never flies near mass on
    /// purpose, so it never charged at all.
    ///
    /// <para>The hull carries its own launch pad: the Boost Ring (<see cref="SquirrelTubeActionSO"/>)
    /// lays a ring of danger prisms ahead along the nose, and flying its hollow centre skims the
    /// whole wall. This lays one on a long, dead-straight leg while the skim boost is low, then holds
    /// the stick centred until the hull is through (a wingtip in a danger prism is a full stop and a
    /// boost reset). The gates are the ones <c>SkimRaceDriver</c>'s optional launch-ring branch uses,
    /// re-read off this hull's own ability so placement distance stays authored once.</para>
    ///
    /// <para>The ring is permanent conserved mass (no TTL), so the shipped asset sets
    /// requireAIPlayer - the menu's lava-lamp autopilot must not wall up Menu_Main.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "SkimRingAIPolicy", menuName = "ScriptableObjects/AI/Boost Policy/Skim Ring")]
    public sealed class SkimRingAIPolicySO : AIBoostPolicySO
    {
        [Header("Ring")]
        [Tooltip("Lay a ring only while the skim boost multiplier is below this (SkimRaceAIConfigSO." +
                 "RingBelowBoost).")]
        [SerializeField, Min(1f)] float ringBelowBoost = 3f;
        [Tooltip("Nose AND course must both be within this many degrees of the objective to lay - the " +
                 "ring is laid along the nose and flown along the course, and the hull must fit its " +
                 "centre. Much tighter than the general Align Degrees.")]
        [SerializeField, Range(0.5f, 10f)] float ringAlignDegrees = 2.5f;
        [Tooltip("Stick deflection allowed when laying.")]
        [SerializeField, Range(0f, 1f)] float ringStickBand = 0.15f;
        [Tooltip("Straight needed BEYOND the ring's placement distance, world units - the objective " +
                 "must not sit between the hull and the ring (SkimRaceDriver uses 60).")]
        [SerializeField, Min(0f)] float clearanceBeyondRing = 60f;
        [Tooltip("Keep the stick centred until this far PAST the ring's plane, world units.")]
        [SerializeField, Min(0f)] float holdLinePastRing = 25f;

        public override AIBoostDriver CreateDriver(IVesselStatus status, ActionExecutorRegistry executors)
        {
            var handler = status?.ActionHandler;
            if (handler == null || !handler.TryGetBoundAction<SquirrelTubeActionSO>(out var action, out _)) return null;
            var executor = executors ? executors.Get<SquirrelTubeActionExecutor>() : null;
            if (!executor) return null;
            return new Driver(this, action, executor, status, executors);
        }

        sealed class Driver : AIBoostDriver
        {
            const float MinPlanningSpeed = 30f;

            readonly SkimRingAIPolicySO _policy;
            readonly SquirrelTubeActionSO _action;
            readonly SquirrelTubeActionExecutor _executor;
            readonly IVesselStatus _status;
            readonly ActionExecutorRegistry _executors;

            bool _pressed;
            float _holdLineUntil;

            public Driver(SkimRingAIPolicySO policy, SquirrelTubeActionSO action,
                          SquirrelTubeActionExecutor executor, IVesselStatus status,
                          ActionExecutorRegistry executors)
            {
                _policy = policy;
                _action = action;
                _executor = executor;
                _status = status;
                _executors = executors;
            }

            public override bool Drives(ShipActionSO action) => action is SquirrelTubeActionSO;

            public override bool HoldsLine => Time.time < _holdLineUntil;

            public override void Tick(in AIBoostContext c)
            {
                // Press now, release next frame - the ability places on the press.
                if (_pressed) ReleasePress();

                if (HoldsLine || !_executor || !_executor.TubeReady) return;
                if (c.Restricted || c.BreakingOrbit) return;

                float boost = _status.IsBoosting ? _status.BoostMultiplier : 1f;
                if (boost >= _policy.ringBelowBoost) return;

                if (c.Stick > _policy.ringStickBand) return;
                if (c.AngleToTarget > _policy.ringAlignDegrees) return;
                if (Vector3.Angle(c.Forward, c.Heading) > _policy.ringAlignDegrees) return;

                // Where the ability will put it - the executor's own placement rule.
                float offset = Mathf.Max(_action.ForwardOffset, c.Speed * _action.LeadSeconds);
                if (c.Distance < offset + _policy.clearanceBeyondRing) return;

                _action.StartAction(_executors, _status);
                _pressed = true;

                // The executor refuses a press on cooldown; only hold the line for a ring that exists.
                if (_executor.TubeReady) return;
                _holdLineUntil = Time.time + (offset + _policy.holdLinePastRing) / Mathf.Max(c.Speed, MinPlanningSpeed);
            }

            public override void Release()
            {
                if (_pressed) ReleasePress();
                _holdLineUntil = 0f;
            }

            void ReleasePress()
            {
                _pressed = false;
                _action.StopAction(_executors, _status);
            }
        }
    }
}
