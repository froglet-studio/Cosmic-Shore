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

        /// <summary>Plant now? The objective <paramref name="offDegrees"/> off the course, <paramref name="distance"/> away,
        /// <paramref name="sinceLetGo"/> seconds after the last let-go.</summary>
        public bool WantsPlant(float offDegrees, float distance, float sinceLetGo) =>
            sinceLetGo >= slingIntervalSeconds && distance >= plantMinDistance && offDegrees >= plantMinTurnDegrees;

        /// <summary>Let go now? The course <paramref name="offDegrees"/> off the objective after
        /// <paramref name="plantedSeconds"/> planted.</summary>
        public bool WantsLetGo(float offDegrees, float plantedSeconds) =>
            offDegrees <= releaseConeDegrees || plantedSeconds >= maxPlantSeconds;

        public override AIBoostDriver CreateDriver(IVesselStatus status, ActionExecutorRegistry executors)
        {
            var thresher = executors ? executors.Get<ThresherExecutor>() : null;
            return thresher && status?.ActionHandler != null ? new Driver(this, status) : null;
        }

        sealed class Driver : AIBoostDriver
        {
            readonly ThresherSlingAIPolicySO _policy;
            readonly IVesselStatus _status;
            readonly List<ShipActionSO> _scratch = new(4);
            InputEvents? _plant, _winch;
            bool _planted, _winding;
            float _plantedAt, _letGoAt = float.NegativeInfinity;

            public Driver(ThresherSlingAIPolicySO policy, IVesselStatus status)
            {
                _policy = policy;
                _status = status;
            }

            public override bool Drives(ShipActionSO action) =>
                action is ThresherActionSO t && t.Control is ThresherActionSO.ThresherControl.Plant
                    or ThresherActionSO.ThresherControl.Winch;

            public override string Status => _planted ? "planted: swinging round the ball" : "waiting for a corner";

            public override void Tick(in AIBoostContext c)
            {
                var handler = _status.ActionHandler;
                if (!handler || !ResolveInputs(handler)) return;
                float now = Time.time;
                float off = Vector3.Angle(c.Heading, c.ToTarget);

                if (_planted)
                {
                    if (c.Restricted || _policy.WantsLetGo(off, now - _plantedAt)) LetGo(handler, now);
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
                _planted = _winding = false;
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
