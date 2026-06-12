using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Goal-mouth trigger for Astro League. When the match ball enters, awards a
    /// goal to the attacking domain via AstroLeagueBall.NotifyGoalScored.
    /// Place one at each end of the arena with a trigger collider spanning the mouth.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class AstroLeagueGoal : MonoBehaviour
    {
        [Tooltip("The domain that DEFENDS this goal. A ball entering here scores for the opposing domain.")]
        [SerializeField] Domains defendingDomain = Domains.Jade;

        [Tooltip("The domain awarded a goal when the ball enters this trigger.")]
        [SerializeField] Domains scoringDomain = Domains.Ruby;

        public Domains DefendingDomain => defendingDomain;
        public Domains ScoringDomain => scoringDomain;
        public Vector3 MouthCenter => transform.position;

        void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            var ball = other.GetComponent<AstroLeagueBall>();
            if (ball == null || ball.IsFrozen) return;

            ball.NotifyGoalScored(scoringDomain);
        }
    }
}
