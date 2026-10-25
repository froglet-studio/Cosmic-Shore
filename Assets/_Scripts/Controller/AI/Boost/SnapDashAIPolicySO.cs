using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// AI boost for a THROTTLE hull (the Scarab). Its cruise-to-top band is the throttle ceiling,
    /// which an autopilot already holds - <see cref="ScarabVesselTransformer"/> reads the trigger
    /// as fully pulled under autopilot - so the only speed an AI Scarab was leaving on the table is
    /// the TIME-5 Snap Dash, which a human fires with a double-tap no autopilot can make.
    ///
    /// This fires it through <see cref="ScarabVesselTransformer.TryAutopilotSnapDash"/> (same upgrade
    /// gate, same numbers) on a straight with runway, at most once per cooldown. Below Time 5 the
    /// transformer refuses and nothing happens, exactly as for a human.
    /// </summary>
    [CreateAssetMenu(fileName = "SnapDashAIPolicy", menuName = "ScriptableObjects/AI/Boost Policy/Snap Dash")]
    public sealed class SnapDashAIPolicySO : AIBoostPolicySO
    {
        [Header("Dash")]
        [Tooltip("Shortest gap between two dashes, seconds. A human's double-tap costs a moment of " +
                 "released throttle (coast drag) per dash; this is the AI's equivalent price.")]
        [SerializeField, Min(0.2f)] float dashCooldownSeconds = 2f;
        [Tooltip("How often to re-ask while the dash is refused (no Time-5 upgrade), seconds.")]
        [SerializeField, Min(0.1f)] float refusedRetrySeconds = 0.5f;

        public override AIBoostDriver CreateDriver(IVesselStatus status, ActionExecutorRegistry executors)
        {
            if (status?.VesselTransformer is not ScarabVesselTransformer transformer) return null;
            return new Driver(this, transformer);
        }

        sealed class Driver : AIBoostDriver
        {
            readonly SnapDashAIPolicySO _policy;
            readonly ScarabVesselTransformer _transformer;
            float _nextTryAt;

            public Driver(SnapDashAIPolicySO policy, ScarabVesselTransformer transformer)
            {
                _policy = policy;
                _transformer = transformer;
            }

            public override void Tick(in AIBoostContext c)
            {
                float now = Time.time;
                if (now < _nextTryAt || !_transformer) return;
                if (!_policy.IsStraight(c) || !_policy.HasRunway(c)) return;

                _nextTryAt = now + (_transformer.TryAutopilotSnapDash()
                    ? _policy.dashCooldownSeconds
                    : _policy.refusedRetrySeconds);
            }

            // A dash is an impulse with its own duration; nothing to let go of.
            public override void Release() { }
        }
    }
}
