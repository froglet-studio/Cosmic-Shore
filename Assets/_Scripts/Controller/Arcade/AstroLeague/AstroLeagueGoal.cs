using UnityEngine;

namespace CosmicShore.Game.Arcade.AstroLeague
{
    /// <summary>
    /// Goal zone trigger for Astro League.
    /// When the ball enters this trigger, a goal is scored for the attacking team.
    /// Place at each end of the arena with a box/sphere collider set to trigger.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class AstroLeagueGoal : MonoBehaviour
    {
        [Tooltip("The domain that DEFENDS this goal. Scoring here awards a point to the opposing team.")]
        [SerializeField] Domains defendingTeam = Domains.Jade;

        [Tooltip("The domain that SCORES when the ball enters this goal.")]
        [SerializeField] Domains scoringTeam = Domains.Ruby;

        [Header("Effects")]
        [SerializeField] ParticleSystem goalEffect;
        [SerializeField] AudioSource goalSound;

        void Awake()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            var ball = other.GetComponent<AstroLeagueBall>();
            if (ball == null) return;

            if (goalEffect != null)
                goalEffect.Play();

            if (goalSound != null)
                goalSound.Play();

            ball.NotifyGoalScored(scoringTeam);
        }
    }
}
