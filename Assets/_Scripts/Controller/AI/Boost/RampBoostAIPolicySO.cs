using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// AI boost for a hull whose boost is a GRADED RAMP (<see cref="RampBoostActionSO"/> - the Rhino):
    /// arm it on a straight at the objective, and end it before the turn.
    ///
    /// <para>The ramp is armed by the full-speed-straight GESTURE, which only an input strategy
    /// raises - and input strategies run only for a local human - so an AI Rhino never ramped at all
    /// and raced every Rhino-only mode at plain cruise (50 u/s against a human's 840). The doc that
    /// said "the Rhino's AI could already boost" was describing the human's gesture.</para>
    ///
    /// <para>Once armed the ramp GRADES ITSELF from the stick the pilot writes (the AI writes
    /// InputStatus like a human does, and the executor reads it on every peer), so this driver never
    /// has to modulate it: it arms on a straight, re-arms if the grading let it fall off, and ends it
    /// explicitly before the turn - a graded ramp's own release is the top of its slope, not an end
    /// (<see cref="RampBoostActionExecutor.ReleaseGesture"/>), so "let go" would not bleed a thing.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "RampBoostAIPolicy", menuName = "ScriptableObjects/AI/Boost Policy/Ramp Boost")]
    public sealed class RampBoostAIPolicySO : AIBoostPolicySO
    {
        [Header("Ramp")]
        [Tooltip("Shortest gap between two arms, seconds - each arm plays the engage sound.")]
        [SerializeField, Min(0f)] float minRearmSeconds = 0.5f;

        public override AIBoostDriver CreateDriver(IVesselStatus status, ActionExecutorRegistry executors)
        {
            var handler = status?.ActionHandler;
            if (handler == null || !handler.TryGetBoundAction<RampBoostActionSO>(out var action, out _)) return null;
            var executor = executors ? executors.Get<RampBoostActionExecutor>() : null;
            return executor ? new Driver(this, action, status, executors, executor) : null;
        }

        sealed class Driver : AIBoostDriver
        {
            readonly RampBoostAIPolicySO _policy;
            readonly RampBoostActionSO _action;
            readonly IVesselStatus _status;
            readonly ActionExecutorRegistry _executors;
            readonly RampBoostActionExecutor _executor;

            bool _wanted;
            float _armedAt = float.NegativeInfinity;

            public Driver(RampBoostAIPolicySO policy, RampBoostActionSO action, IVesselStatus status,
                          ActionExecutorRegistry executors, RampBoostActionExecutor executor)
            {
                _policy = policy;
                _action = action;
                _status = status;
                _executors = executors;
                _executor = executor;
            }

            public override bool Drives(ShipActionSO action) => action is RampBoostActionSO;

            public override void Tick(in AIBoostContext c)
            {
                float now = Time.time;
                if (_wanted)
                {
                    if (c.Restricted || !_policy.StillStraight(c) || _policy.TurnAhead(c))
                    {
                        _wanted = false;
                        _executor.End();   // bleed before the corner, not in it
                        return;
                    }
                    if (!_executor.IsEngaged && now - _armedAt >= _policy.minRearmSeconds) Arm(now);
                    return;
                }

                if (_policy.IsStraight(c) && _policy.HasRunway(c))
                {
                    _wanted = true;
                    Arm(now);
                }
            }

            public override void Release()
            {
                _wanted = false;
                _executor.End();
            }

            void Arm(float now)
            {
                _armedAt = now;
                _action.StartAction(_executors, _status);
            }
        }
    }
}
