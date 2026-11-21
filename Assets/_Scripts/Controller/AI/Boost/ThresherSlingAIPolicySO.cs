using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Thresher's autopilot SLING: the corner an AI Thresher used to take at cruise (THRESHERTIME.md §4, "an AI
    /// Thresher never slings"). When the objective sits at least <see cref="plantMinTurnDegrees"/> off the course and
    /// far enough away, it presses PLANT (the left trigger: the ball stops and the ship swings round it) and, with
    /// <see cref="windWhilePlanted"/>, WINCH (the right trigger: the chain pulls rigid and spins the ship up); it lets
    /// go once the course - the orbit's tangent - faces the objective within <see cref="releaseConeDegrees"/>, or after
    /// <see cref="maxPlantSeconds"/>. Released unaimed, the ship leaves along the tangent at the orbit's speed
    /// (<c>ThresherExecutor.LetGo</c>), the same let-go a human gets with no launch view up.
    ///
    /// <para>The design THRESHERTIME.md §4 left as the follow-up ("plant through the REPLICATED press when the next gate
    /// is ~70° off the nose, release when the orbit's tangent faces it"). Both edges go through
    /// <see cref="R_VesselActionHandler.PerformShipControllerActionsReplicated"/>, because the chain is simulated on
    /// every peer from the presses. The inputs are found by <see cref="ThresherActionSO.Control"/> and resolved again
    /// until the binding maps have filled (vessel contract rule 6). Before 2026-10-26 the prefab named the Squirrel's
    /// SkimRingAIPolicySO, inherited with the hull clone, which binds nothing on a Thresher.</para>
    ///
    /// <para><b>The aimed launch</b> (<see cref="aimedLaunch"/>, 2026-11-02, from Garrett's Thresher Time races: 85% of
    /// his planted time is it, launches within a few degrees of the ring at up to 4,600 u/s). Once the launch view is up
    /// (both triggers full on a fast enough spin, <c>ThresherExecutor.LaunchViewUp</c>) the driver hands the executor
    /// its objective every tick (<c>ThresherExecutor.AimAutopilotLaunch</c>) and lets go after
    /// <see cref="aimedHoldSeconds"/>; the executor arms the launch along the objective, so the ship leaves from the
    /// point of the circle whose tangent faces it, at the pilot's launch boost. Below the view the let-go is the plain
    /// one above. Off by default: a mode's learned variant turns it on when its people launch that way
    /// (<c>Tools/Build/learn_drives_from_flights.py</c>).</para>
    /// </summary>
    [CreateAssetMenu(fileName = "ThresherSlingAIPolicy", menuName = "ScriptableObjects/AI/Boost Policy/Thresher Sling")]
    public sealed class ThresherSlingAIPolicySO : AIBoostPolicySO
    {
        [Header("When to plant")]
        [Tooltip("Plant once the objective is at least this many degrees off the course: a corner the cruise circle " +
                 "takes wide. THRESHERTIME.md §4's ~70.")]
        [SerializeField, Range(0f, 180f)] float plantMinTurnDegrees = 70f;
        [Tooltip("Plant only while the objective is at least this far away, u: a planted orbit needs room before the ring.")]
        [SerializeField, Min(0f)] float plantMinDistance = 120f;
        [Tooltip("Seconds after a let-go before the next plant, so one corner is one sling.")]
        [SerializeField, Min(0f)] float slingIntervalSeconds = 1.5f;

        [Header("When to let go")]
        [Tooltip("Let go once the course (the orbit's tangent) is within this many degrees of the objective.")]
        [SerializeField, Range(1f, 90f)] float releaseConeDegrees = 15f;
        [Tooltip("Let go after this many seconds planted whatever the bearing, so an orbit the long way round " +
                 "never becomes a stall.")]
        [SerializeField, Min(0.2f)] float maxPlantSeconds = 3f;

        [Header("The winch")]
        [Tooltip("Pull the winch while planted: the chain goes rigid, reels the ship in and spins it up, so the let-go " +
                 "leaves faster. Off, the ship swings round the ball on a slack chain.")]
        [SerializeField] bool windWhilePlanted = true;

        [Header("The aimed launch")]
        [Tooltip("Aim the let-go: once the launch view is up (both triggers full on a fast enough spin), aim at the " +
                 "objective and let go after aimedHoldSeconds, so the ship leaves from the point of the circle whose " +
                 "tangent faces it at the launch boost - a pilot's crosshair launch. Off, every let-go is the plain one. " +
                 "Needs windWhilePlanted (the winch is the second full trigger).")]
        [SerializeField] bool aimedLaunch;
        [Tooltip("Seconds planted before an aimed let-go: the spin-up, since the launch leaves at the boost times the " +
                 "orbit's speed. Learned from people's aimed launches (the median plant).")]
        [SerializeField, Min(0f)] float aimedHoldSeconds = 1.5f;
        [Tooltip("Let go of an aimed launch only once its aim is within this many degrees of the objective: the orbit " +
                 "plane tilts onto the objective at the pilot's aimPitchRate, so a let-go before it has would launch off " +
                 "the ring. maxPlantSeconds still ends every plant.")]
        [SerializeField, Range(0.5f, 45f)] float aimToleranceDegrees = 3f;

        /// <summary>The aimed launch is on (and can come up: it needs the winch as its second full trigger).</summary>
        public bool AimsLaunch => aimedLaunch && windWhilePlanted;

        /// <summary>Let go of an AIMED launch now? <paramref name="plantedSeconds"/> planted under the launch view, its
        /// aim <paramref name="aimErrorDegrees"/> off the objective.</summary>
        public bool WantsAimedLetGo(float plantedSeconds, float aimErrorDegrees) =>
            plantedSeconds >= maxPlantSeconds
            || (plantedSeconds >= aimedHoldSeconds && aimErrorDegrees <= aimToleranceDegrees);

        /// <summary>Plant now? The objective <paramref name="offDegrees"/> off the course, <paramref name="distance"/> away,
        /// <paramref name="sinceLetGo"/> seconds after the last let-go.</summary>
        public bool WantsPlant(float offDegrees, float distance, float sinceLetGo) =>
            sinceLetGo >= slingIntervalSeconds && distance >= plantMinDistance && offDegrees >= plantMinTurnDegrees;

        /// <summary>Let go now? The course <paramref name="offDegrees"/> off the objective after
        /// <paramref name="plantedSeconds"/> planted.</summary>
        public bool WantsLetGo(float offDegrees, float plantedSeconds) =>
            offDegrees <= releaseConeDegrees || plantedSeconds >= maxPlantSeconds;

        /// <summary>Let go now, with the launch view NOT up? <see cref="WantsLetGo"/>, except that an aimed launcher first
        /// gives the spin <see cref="aimedHoldSeconds"/> to bring the view up: it plants with the ring nearly ahead (Garrett:
        /// 4-20° off), where the cone alone would let go on the plant's first tick.</summary>
        public bool WantsUnaimedLetGo(float offDegrees, float plantedSeconds) =>
            AimsLaunch
                ? plantedSeconds >= maxPlantSeconds || (plantedSeconds >= aimedHoldSeconds && offDegrees <= releaseConeDegrees)
                : WantsLetGo(offDegrees, plantedSeconds);

        public override AIBoostDriver CreateDriver(IVesselStatus status, ActionExecutorRegistry executors)
        {
            var thresher = executors ? executors.Get<ThresherExecutor>() : null;
            return thresher && status?.ActionHandler != null ? new Driver(this, status, thresher) : null;
        }

        sealed class Driver : AIBoostDriver
        {
            readonly ThresherSlingAIPolicySO _policy;
            readonly IVesselStatus _status;
            readonly ThresherExecutor _thresher;
            readonly List<ShipActionSO> _scratch = new(4);
            InputEvents? _plant, _winch;
            bool _planted, _winding, _aiming;
            float _plantedAt, _letGoAt = float.NegativeInfinity;

            public Driver(ThresherSlingAIPolicySO policy, IVesselStatus status, ThresherExecutor thresher)
            {
                _policy = policy;
                _status = status;
                _thresher = thresher;
            }

            public override bool Drives(ShipActionSO action) =>
                action is ThresherActionSO t && t.Control is ThresherActionSO.ThresherControl.Plant
                    or ThresherActionSO.ThresherControl.Winch;

            public override string Status => !_planted ? "waiting for a corner"
                : _aiming ? "planted: aiming the launch" : "planted: swinging round the ball";

            public override void Tick(in AIBoostContext c)
            {
                var handler = _status.ActionHandler;
                if (!handler || !ResolveInputs(handler)) return;
                float now = Time.time;
                float off = Vector3.Angle(c.Heading, c.ToTarget);

                if (_planted)
                {
                    float planted = now - _plantedAt;
                    if (c.Restricted) { LetGo(handler, now); return; }
                    // The aimed launch: under the launch view, aim at the objective every tick (the aim the let-go arms)
                    // and let go after the spin-up. Below the view, the plain let-go.
                    if (_policy.AimsLaunch && _winding && _thresher && _thresher.LaunchViewUp)
                    {
                        _aiming = true;
                        _thresher.AimAutopilotLaunch(c.ToTarget);
                        if (_policy.WantsAimedLetGo(planted, _thresher.AutopilotAimErrorDegrees)) LetGo(handler, now);
                        return;
                    }
                    if (_policy.WantsUnaimedLetGo(off, planted)) LetGo(handler, now);
                    return;
                }

                if (c.Restricted || c.BreakingOrbit) return;
                if (!_policy.WantsPlant(off, c.Distance, now - _letGoAt)) return;

                handler.PerformShipControllerActionsReplicated(_plant.Value);
                _planted = true;
                _plantedAt = now;
                if (_policy.windWhilePlanted && _winch.HasValue)
                {
                    handler.PerformShipControllerActionsReplicated(_winch.Value);
                    _winding = true;
                }
            }

            public override void Release()
            {
                var handler = _status?.ActionHandler;
                if (handler && (_planted || _winding)) LetGo(handler, Time.time);
            }

            void LetGo(R_VesselActionHandler handler, float now)
            {
                if (_planted && _plant.HasValue) handler.StopShipControllerActionsReplicated(_plant.Value);
                if (_winding && _winch.HasValue) handler.StopShipControllerActionsReplicated(_winch.Value);
                _planted = _winding = _aiming = false;
                _letGoAt = now;
            }

            /// <summary>The Plant (and Winch) inputs on THIS vessel, read off its binding maps; retried until the plant
            /// resolves, because the maps fill after the executors initialise.</summary>
            bool ResolveInputs(R_VesselActionHandler handler)
            {
                if (_plant.HasValue) return true;
                foreach (InputEvents candidate in System.Enum.GetValues(typeof(InputEvents)))
                {
                    _scratch.Clear();
                    handler.CollectBoundActions(candidate, _scratch);
                    foreach (var action in _scratch)
                    {
                        if (action is not ThresherActionSO t) continue;
                        if (t.Control == ThresherActionSO.ThresherControl.Plant && !_plant.HasValue) _plant = candidate;
                        else if (t.Control == ThresherActionSO.ThresherControl.Winch && !_winch.HasValue) _winch = candidate;
                    }
                }
                _scratch.Clear();
                return _plant.HasValue;
            }
        }
    }
}
