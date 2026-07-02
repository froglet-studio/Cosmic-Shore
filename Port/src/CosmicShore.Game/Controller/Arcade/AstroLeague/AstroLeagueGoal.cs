// Ported verbatim from Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueGoal.cs
// (AstroLeague arc). Mechanical substitutions only (README table).
using CosmicShore.Data;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Goal-mouth detector for Astro League. Server-authoritative and ACCURATE: instead of a fat
    /// trigger volume (which false-positives when the ball grazes, bounces near, or is teleported
    /// into the goal area), it polls the ball each physics tick and registers a goal only when the
    /// ball genuinely crosses the goal-line PLANE, INWARD, WITHIN the mouth circle. The back wall is
    /// solid, so we fire when the ball's LEADING EDGE reaches the goal plane (center distance = −radius)
    /// — that happens just before the ball would bounce off the wall, and the controller detonates it.
    ///
    /// Attribution — which domain the goal counts for — is the controller's job (last striker, own-goal
    /// rules); this only reports a clean, real crossing.
    /// </summary>
    public class AstroLeagueGoal : MonoBehaviour
    {
        [Tooltip("The domain that DEFENDS this goal. A ball crossing here scores for the last attacking domain.")]
        [SerializeField] Domains defendingDomain = Domains.Jade;

        [Tooltip("Match controller notified when the ball crosses this goal line (server only).")]
        [SerializeField] AstroLeagueController controller;

        [Tooltip("Mouth radius (base / intensity-1). The ball must cross the goal plane within this " +
                 "radius of the mouth center to count. Match the arena's goal-ring radius.")]
        [SerializeField] float mouthRadius = 26f;

        public Domains DefendingDomain => defendingDomain;
        public Vector3 MouthCenter => transform.position;

        AstroLeagueBall _ball;
        Vector3 _inwardNormal = Vector3.forward; // from arena center out through this goal
        float _scale = 1f;
        Vector3 _lastBallPos;
        bool _hasLast;

        void Awake()
        {
            // Detection is the FixedUpdate plane-crossing poll, not a physics trigger. Disable any
            // leftover scene collider so it can neither block the ball nor fire stray trigger events.
            if (TryGetComponent(out Collider col)) col.enabled = false;
        }

        /// <summary>
        /// Wire the ball + arena center + intensity scale. Called by the controller on every peer once
        /// the goal is positioned at its scaled goal line (so the inward normal is computed correctly).
        /// </summary>
        public void Configure(AstroLeagueBall ball, Vector3 arenaCenter, float scale)
        {
            _ball = ball;
            _scale = Mathf.Max(0.01f, scale);
            Vector3 outward = transform.position - arenaCenter;
            _inwardNormal = outward.sqrMagnitude > 1e-4f ? outward.normalized : Vector3.forward;
            _hasLast = false;
        }

        /// <summary>Kept for the controller's intensity-scale call: the mouth grows with the arena.</summary>
        public void ScaleTrigger(float scale) => _scale = Mathf.Max(0.01f, scale);

        void FixedUpdate()
        {
            // Server-authoritative: only the server simulates the ball and decides goals.
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening && !nm.IsServer) return;

            if (_ball == null || controller == null || _ball.IsFrozen || _ball.IsHidden)
            {
                _hasLast = false;
                return;
            }

            Vector3 cur = _ball.transform.position;
            if (!_hasLast)
            {
                _lastBallPos = cur;
                _hasLast = true;
                return;
            }

            Vector3 prev = _lastBallPos;
            _lastBallPos = cur;

            // Teleport guard: ignore implausible single-tick jumps (anti-clip eject, kickoff reset),
            // which must never count as a crossing.
            float maxStep = _ball.MaxSpeed * Time.fixedDeltaTime * 2f;
            if ((cur - prev).sqrMagnitude > maxStep * maxStep) return;

            Vector3 mouth = transform.position;
            // Fire when the ball's LEADING EDGE reaches the goal plane (center distance crosses −radius),
            // so a big ball scores before bouncing off the solid back wall behind the mouth.
            float threshold = -_ball.BallWorldRadius();
            float dPrev = Vector3.Dot(prev - mouth, _inwardNormal);
            float dCur = Vector3.Dot(cur - mouth, _inwardNormal);
            if (dPrev >= threshold || dCur < threshold) return; // not an inward crossing this tick

            // Crossing point on the threshold plane; its off-axis distance is the lateral miss.
            float t = Mathf.Clamp01((threshold - dPrev) / (dCur - dPrev));
            Vector3 cross = Vector3.Lerp(prev, cur, t);
            Vector3 rel = cross - mouth;
            Vector3 lateral = rel - Vector3.Dot(rel, _inwardNormal) * _inwardNormal;
            if (lateral.sqrMagnitude > mouthRadius * _scale * (mouthRadius * _scale)) return; // missed the mouth

            _hasLast = false; // avoid a second fire while the controller detonates/hides the ball
            controller.HandleGoalServer(this, _ball);
        }
    }
}
