using CosmicShore.Data;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Goal-mouth trigger for Astro League. Place one at each end of the arena with a
    /// trigger collider spanning the mouth. Goal detection is server-authoritative: only
    /// the server's simulated ball can enter the trigger (non-server peers run a kinematic
    /// replica, and the report is gated on IsServer anyway). Attribution — which domain the
    /// goal counts for — is the controller's job (last striker, own-goal rules).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class AstroLeagueGoal : MonoBehaviour
    {
        [Tooltip("The domain that DEFENDS this goal. A ball entering here scores for the last attacking domain.")]
        [SerializeField] Domains defendingDomain = Domains.Jade;

        [Tooltip("Match controller notified when the ball crosses this goal line (server only).")]
        [SerializeField] AstroLeagueController controller;

        public Domains DefendingDomain => defendingDomain;
        public Vector3 MouthCenter => transform.position;

        BoxCollider _box;
        Vector3 _baseSize, _baseCenter;

        void Awake()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true;
            _box = col as BoxCollider;
            if (_box != null)
            {
                _baseSize = _box.size;
                _baseCenter = _box.center;
            }
        }

        /// <summary>
        /// Scale the goal-mouth trigger to match the intensity-scaled arena (the controller also
        /// repositions this goal to the scaled goal line). The mouth grows proportionally so the
        /// bigger arena keeps the same relative goal size.
        /// </summary>
        public void ScaleTrigger(float scale)
        {
            if (_box == null) return;
            _box.size = _baseSize * scale;
            _box.center = _baseCenter * scale;
        }

        void OnTriggerEnter(Collider other)
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening && !nm.IsServer) return;

            if (!other.TryGetComponent(out AstroLeagueBall ball)) return;
            if (ball.IsFrozen || ball.IsHidden) return;

            controller.HandleGoalServer(this, ball);
        }
    }
}
