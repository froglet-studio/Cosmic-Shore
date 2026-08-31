using CosmicShore.Data;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Goal-mouth detector for Astro League. Server-authoritative and ACCURATE: instead of a fat
    /// trigger volume (which false-positives when the ball grazes, bounces near, or is teleported
    /// into the goal area), it polls the ball each physics tick and registers a goal only when the
    /// ball genuinely crosses the goal-line PLANE, INWARD, WITHIN the mouth circle. The back wall is
    /// solid, so we fire when the ball's LEADING EDGE reaches the goal plane (center distance = −radius)
    /// - that happens just before the ball would bounce off the wall, and the controller detonates it.
    ///
    /// Attribution - which domain the goal counts for - is the controller's job (last striker, own-goal
    /// rules); this only reports a clean, real crossing.
    /// </summary>
    public class AstroLeagueGoal : MonoBehaviour
    {
        [Tooltip("The domain that DEFENDS this goal. A ball crossing here scores for the last attacking domain.")]
        [SerializeField] Domains defendingDomain = Domains.Jade;

        [Tooltip("Match controller notified when the ball crosses this goal line (server only).")]
        [SerializeField] AstroLeagueController controller;

        [Tooltip("LEGACY fallback mouth radius (base / intensity-1), used only when the controller " +
                 "does not supply one. The shipping source is AstroLeagueSettingsSO.goalMouthRadius, " +
                 "handed in by Configure - the same number the arena draws its portal rings at, so " +
                 "the ring you aim at IS the mouth that scores. Do not tune this.")]
        [SerializeField] float mouthRadius = 62f;

        public Domains DefendingDomain => defendingDomain;
        public Vector3 MouthCenter => transform.position;
        /// <summary>The direction the ball must travel through the mouth to score here (the scoring direction).</summary>
        public Vector3 InwardNormal => _inwardNormal;

        AstroLeagueBall _ball;
        Vector3 _inwardNormal = Vector3.forward; // from arena center out through this goal
        float _scale = 1f;
        bool _passThrough; // central shared goal: score on CENTER crossing (no solid back wall)
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
        /// Pass <paramref name="explicitInwardNormal"/> for the central shared-goal layout, where the
        /// goal sits AT the arena center so the position-derived normal would be ambiguous - the scoring
        /// direction (which pass direction counts) is then set explicitly (e.g. ±Z). Set
        /// <paramref name="passThrough"/> for that same layout: it has no solid back wall, so the ball
        /// scores when its CENTER crosses the plane (not when its leading edge reaches a back wall).
        /// </summary>
        public void Configure(AstroLeagueBall ball, Vector3 arenaCenter, float scale,
            Vector3? explicitInwardNormal = null, bool passThrough = false, float baseMouthRadius = 0f)
        {
            _ball = ball;
            _scale = Mathf.Max(0.01f, scale);
            _passThrough = passThrough;
            if (baseMouthRadius > 0f) mouthRadius = baseMouthRadius;
            if (explicitInwardNormal.HasValue && explicitInwardNormal.Value.sqrMagnitude > 1e-4f)
            {
                _inwardNormal = explicitInwardNormal.Value.normalized;
            }
            else
            {
                Vector3 outward = transform.position - arenaCenter;
                _inwardNormal = outward.sqrMagnitude > 1e-4f ? outward.normalized : Vector3.forward;
            }
            _hasLast = false;
        }

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
            // End goal: fire when the ball's LEADING EDGE reaches the goal plane (center distance crosses
            // −radius), so a big ball scores before bouncing off the solid back wall behind the mouth.
            // Central pass-through goal: no back wall, so fire when the ball's CENTER crosses the plane.
            float threshold = _passThrough ? 0f : -_ball.BallWorldRadius();
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
