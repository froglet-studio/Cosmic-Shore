using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Angler's autopilot LEAPFROG: the Time race's verb (Lantern Run, <c>Arcade/LANTERNRUN.md</c>). Until this, an AI
    /// Angler only cruised at 40 u/s: the prefab named the Stoat's skim-ring policy, inherited with the hull clone, which
    /// binds nothing on an Angler (ANGLER.md follow-up 2).
    ///
    /// <para><b>The loop, input-only.</b> Lined up on its objective, it HOLDS the right trigger (the pad's paint: the
    /// group whose turn it is balls up at the nose and the brush paints whatever lies along the nose - in Lantern Run, the
    /// lantern past the next ring) and LETS GO, which throws that group at the paint (or, with nothing painted, along the
    /// nose). Then, while the next dash would carry it TOWARD the objective, it presses DASH (the Time ability: 75% of the
    /// way to a group). The trigger is written to <see cref="IInputStatus.RightTriggerAnalog"/>, the channel a pad writes
    /// (the Rhino sword autopilot's shape); the dash goes through
    /// <see cref="R_VesselActionHandler.PerformShipControllerActionsReplicated"/> on the input this hull binds to
    /// <see cref="AnglerControl.Dash"/>. It READS the executor's next dash point and writes nothing else.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "AnglerLeapfrogAIPolicy", menuName = "ScriptableObjects/AI/Boost Policy/Angler Leapfrog")]
    public sealed class AnglerLeapfrogAIPolicySO : AIBoostPolicySO
    {
        [Header("The throw")]
        [Tooltip("Paint and throw only while the course is within this many degrees of the objective: the brush is 3 " +
                 "degrees, so the lantern past the ring paints only when the nose is on it.")]
        [SerializeField, Range(0.5f, 30f)] float throwAlignDegrees = 4f;
        [Tooltip("Throw only while the objective is at least this far away, u: closer, the hull just flies through.")]
        [SerializeField, Min(0f)] float minThrowDistance = 120f;
        [Tooltip("Seconds the trigger is held before the throw: long enough for the group to ball up at the nose.")]
        [SerializeField, Min(0.05f)] float holdSeconds = 0.45f;
        [Tooltip("Seconds after a throw before the next one, so the thrown group gets ahead before the next is sent.")]
        [SerializeField, Min(0f)] float throwIntervalSeconds = 0.6f;

        [Header("The dash")]
        [Tooltip("Dash only when the dash would carry the hull at least this far, u.")]
        [SerializeField, Min(1f)] float minDashDistance = 40f;
        [Tooltip("Dash only when the dash points within this many degrees of the objective: never backwards.")]
        [SerializeField, Range(0f, 120f)] float dashAheadDegrees = 50f;

        public float ThrowAlignDegrees => throwAlignDegrees;

        /// <summary>Start a throw now? The objective <paramref name="offDegrees"/> off the course, <paramref name="distance"/>
        /// away, <paramref name="sinceThrow"/> seconds after the last throw.</summary>
        public bool WantsThrow(float offDegrees, float distance, float sinceThrow) =>
            sinceThrow >= throwIntervalSeconds && distance >= minThrowDistance && offDegrees <= throwAlignDegrees;

        /// <summary>Let go (throw) after <paramref name="heldSeconds"/> held?</summary>
        public bool WantsRelease(float heldSeconds) => heldSeconds >= holdSeconds;

        /// <summary>Dash now? The dash would move the hull by <paramref name="dash"/>; the objective lies along
        /// <paramref name="toTarget"/>.</summary>
        public bool WantsDash(Vector3 dash, Vector3 toTarget) =>
            dash.magnitude >= minDashDistance && toTarget.sqrMagnitude > 1e-4f &&
            Vector3.Angle(dash, toTarget) <= dashAheadDegrees;

        public override AIBoostDriver CreateDriver(IVesselStatus status, ActionExecutorRegistry executors)
        {
            var angler = executors ? executors.Get<AnglerCommandExecutor>() : null;
            return angler && status?.ActionHandler != null
                ? new Driver(this, status, angler)
                : null;
        }

        sealed class Driver : AIBoostDriver
        {
            readonly AnglerLeapfrogAIPolicySO _policy;
            readonly IVesselStatus _status;
            readonly AnglerCommandExecutor _angler;
            readonly List<ShipActionSO> _scratch = new(4);
            InputEvents? _dash;
            bool _holding;
            float _heldAt, _thrownAt = float.NegativeInfinity;

            public Driver(AnglerLeapfrogAIPolicySO policy, IVesselStatus status, AnglerCommandExecutor angler)
            {
                _policy = policy;
                _status = status;
                _angler = angler;
            }

            public override bool Drives(ShipActionSO action) => action is AnglerActionSO a && a.Kind == AnglerControl.Dash;

            public override string Status => _holding ? "balling the group up to throw" : "leapfrogging its school";

            public override void Tick(in AIBoostContext c)
            {
                var handler = _status.ActionHandler;
                var input = _status.InputStatus;
                if (!handler || input == null || !_status.Transform) return;
                float now = Time.time;

                if (_holding)
                {
                    bool letGo = c.Restricted || _policy.WantsRelease(now - _heldAt);
                    input.RightTriggerAnalog = letGo ? 0f : 1f;
                    if (letGo)
                    {
                        _holding = false;
                        _thrownAt = now;
                    }
                    return;
                }

                if (c.Restricted) return;

                if (ResolveDash(handler) && _angler.DashReady && _angler.TryGetDashPoint(out Vector3 point) &&
                    _policy.WantsDash(point - _status.Transform.position, c.ToTarget))
                {
                    handler.PerformShipControllerActionsReplicated(_dash.Value);
                    return;
                }

                if (_policy.WantsThrow(Vector3.Angle(_status.Transform.forward, c.ToTarget), c.Distance, now - _thrownAt))
                {
                    _holding = true;
                    _heldAt = now;
                    input.RightTriggerAnalog = 1f;
                }
            }

            public override void Release()
            {
                if (_holding && _status?.InputStatus != null) _status.InputStatus.RightTriggerAnalog = 0f;
                _holding = false;
            }

            /// <summary>The input this hull binds to the Dash, read off its binding maps; retried until it resolves,
            /// because the maps fill after the executors initialise (vessel contract rule 6).</summary>
            bool ResolveDash(R_VesselActionHandler handler)
            {
                if (_dash.HasValue) return true;
                foreach (InputEvents candidate in System.Enum.GetValues(typeof(InputEvents)))
                {
                    _scratch.Clear();
                    handler.CollectBoundActions(candidate, _scratch);
                    foreach (var action in _scratch)
                        if (action is AnglerActionSO a && a.Kind == AnglerControl.Dash) { _dash = candidate; break; }
                    if (_dash.HasValue) break;
                }
                _scratch.Clear();
                return _dash.HasValue;
            }
        }
    }
}
