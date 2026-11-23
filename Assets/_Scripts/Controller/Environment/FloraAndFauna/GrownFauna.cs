using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A GROWN creature (<c>Tools/Build/fauna_kit</c>: the Ray, the Plume, the Lantern, the Ammon, the Nettle and every
    /// one after them). It is a <see cref="LightFauna"/> in every way but how it feels under a rider, and every number
    /// here is authored on the prefab by <c>fauna_kit/unity_port.py</c> from the creature's spec (its
    /// <c>possession</c> block, the numbers the lab flies it with), never typed here. A new creature needs no class of
    /// its own: it is this one with its own numbers.
    ///
    /// <para><b>How it eats</b> (<see cref="suction"/>): ahead of its mouth like every creature, on the wing (what
    /// passes round its mouth, the Ray), or on every side through its whole reach (the Plume, the Nettle).</para>
    ///
    /// <para><b>How it answers the sticks</b>: a share of every other creature's turn rate and its own ease into speed
    /// (<see cref="riddenTurnScale"/>, <see cref="riddenSpeedEase"/>: the Ray glides); a body with no up takes its
    /// rider's frame for its up and rolls as the rider rolls (<see cref="carriesRiderUp"/>, through
    /// <see cref="IFaunaLeashFrame"/>); and one that swims in PULSES kicks, then glides and turns in the glide
    /// (<see cref="pulsePeriod"/>: the Ammon's jets, the Nettle's bell).</para>
    ///
    /// <para><b>How big it is to a rider</b> (<see cref="riddenBodyRadius"/>, <see cref="aimConeDegrees"/>): a body
    /// that is all reach is attacked from, and attacks, well outside the crystal it is grown round.</para>
    ///
    /// <para>Everything else is the base's: the rider's cadence and bite count, every shield is food, the rider's own
    /// team's mass and creatures' bodies are never food, and what is eaten fills the stomach
    /// (<see cref="Fauna.EatNearest"/>). How the body MOVES is not here: the wave is the spindle shader's and the
    /// turn's answers are <see cref="GrownLimbDriver"/>'s.</para>
    /// </summary>
    public sealed class GrownFauna : LightFauna
    {
        /// <summary>Where a ridden creature's suction takes prisms from.</summary>
        public enum SuctionShape
        {
            /// <summary>A mouthful ahead of the mouth: every creature's default.</summary>
            Ahead = 0,
            /// <summary>What passes round the mouth, on every side, within <see cref="ramRadius"/>: feeding on the wing.</summary>
            Ram = 1,
            /// <summary>Every side, through the creature's whole reach.</summary>
            All = 2,
        }

        [Header("Feeding under a rider")]
        [Tooltip("Where a rider's suction takes prisms from: ahead of the mouth, round the mouth (on the wing), or every side through its whole reach.")]
        [SerializeField] SuctionShape suction = SuctionShape.Ahead;

        [Tooltip("Ram only: how far round its mouth it eats, in world units at body scale 1. A rider's Space scales it, and so does the body's own scale.")]
        [Min(0f)] [SerializeField] float ramRadius = 20f;

        [Header("Steering under a rider")]
        [Tooltip("The share of a rider's turn rate it answers with. 1 turns like every other creature; the Ray is 0.3.")]
        [Range(0.05f, 1f)] [SerializeField] float riddenTurnScale = 1f;

        [Tooltip("How fast its speed eases to the rider's, per second. Every other creature is 2.5; the Ray is 0.9.")]
        [Min(0.05f)] [SerializeField] float riddenSpeedEase = 2.5f;

        [Tooltip("It has no up: under a rider whose leash flies a frame (the Jewel Wasp) it takes that frame's up, so it rolls as the rider rolls.")]
        [SerializeField] bool carriesRiderUp = true;

        [Header("Swimming in pulses under a rider (0 = a steady swimmer)")]
        [Tooltip("Seconds from one kick to the next. 0 = it does not pulse.")]
        [Min(0f)] [SerializeField] float pulsePeriod;

        [Tooltip("The share of each pulse that is the kick.")]
        [Range(0.05f, 0.95f)] [SerializeField] float pulseKickShare = 0.3f;

        [Tooltip("Its speed during the kick, x the rider's.")]
        [Min(0f)] [SerializeField] float pulseKickSpeed = 2f;

        [Tooltip("Its speed during the glide, x the rider's.")]
        [Min(0f)] [SerializeField] float pulseGlideSpeed = 0.5f;

        [Tooltip("The share of a rider's turn rate it answers with during the glide (it turns in the glide, not the kick).")]
        [Range(0.05f, 1f)] [SerializeField] float pulseGlideTurnScale = 1f;

        [Header("Its size to a rider (0 = the base's)")]
        [Tooltip("The body's radius at body scale 1, in world units: a rider's camera stands back by it and a rider's reach for a vessel adds it. 0 = the root's scale, like every creature.")]
        [Min(0f)] [SerializeField] float riddenBodyRadius;

        [Tooltip("The half-angle, in degrees, of the cone ahead in which a rider's attack finds a vessel. 50 is every creature's; a body that is all reach attacks all round (180).")]
        [Range(5f, 180f)] [SerializeField] float aimConeDegrees = 50f;

        /// <summary>True while a pulsing swimmer is in the kick of its pulse.</summary>
        bool Kicking => pulsePeriod > 0f && Mathf.Repeat(Time.time, pulsePeriod) < pulsePeriod * pulseKickShare;

        protected override float LeashTurnScale => pulsePeriod > 0f && !Kicking ? pulseGlideTurnScale : riddenTurnScale;
        protected override float LeashSpeedEase => riddenSpeedEase;
        protected override float LeashSpeedScale => pulsePeriod > 0f ? (Kicking ? pulseKickSpeed : pulseGlideSpeed) : 1f;

        protected override Vector3 LeashUp(Vector3 heading) =>
            carriesRiderUp && Leash is IFaunaLeashFrame frame && frame.TryFrameUp(this, out var up) ? up : base.LeashUp(heading);

        public override float PossessionBodyRadius =>
            riddenBodyRadius > 0f ? riddenBodyRadius * Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.x)) : base.PossessionBodyRadius;

        public override float PossessionAimConeDegrees => aimConeDegrees;

        public override int PossessedSuction(float reachScale, int maxBites, bool allSides)
        {
            if (suction == SuctionShape.All) return base.PossessedSuction(reachScale, maxBites, true);
            if (suction != SuctionShape.Ram) return base.PossessedSuction(reachScale, maxBites, allSides);

            var rider = Possessor;
            if (rider == null || !IsSimAuthority || IsDying || maxBites <= 0) return 0;
            var index = PrismSpatialIndex.EnsureInstance();
            if (index == null || !index.IsAvailable) return 0;
            var mouth = PossessionMouth ? PossessionMouth : transform;
            float radius = ramRadius * Mathf.Max(0.1f, reachScale) * Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.x));
            Vector3 at = mouth.position;
            int found = index.QuerySphere(at, radius, PossessionScratch);
            return EatNearest(at, found, maxBites, rider, mouth);
        }
    }
}
