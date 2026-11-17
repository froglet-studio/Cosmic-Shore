using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Ray: a grown glider (<c>Tools/Build/fauna_kit/specs/ray.py</c>). It is a <see cref="LightFauna"/> in every
    /// way but its mouth. A ray feeds on the wing: it does not reach ahead for a mouthful, it eats what passes between
    /// its lobes. So a rider's suction (<see cref="Fauna.PossessedSuction"/>) takes the prisms round its mouth, on
    /// every side, within <see cref="ramRadius"/>, instead of a sphere pushed out ahead of it.
    ///
    /// <para>It GLIDES under a rider: it answers a turn at a fraction of every other creature's rate and is slow to gain
    /// speed and slow to lose it (<see cref="riddenTurnScale"/>, <see cref="riddenSpeedEase"/>: the numbers the lab
    /// flies it with). And it has no up: its two faces are the same, so under a Jewel Wasp it takes the rider's frame
    /// for its up and rolls as the rider rolls (<see cref="IFaunaLeashFrame"/>).</para>
    ///
    /// <para>Everything else about the mouthful is the base's: the rider's cadence and bite count, every shield is
    /// food, the rider's own team's mass and creatures' bodies are never food, and what is eaten fills the stomach
    /// (<see cref="Fauna.EatNearest"/>).</para>
    /// </summary>
    public sealed class RayFauna : LightFauna
    {
        [Header("Feeding on the wing")]
        [Tooltip("How far round its mouth a possessed Ray eats, in world units at body scale 1. A rider's Space scales it, and so does the body's own scale.")]
        [Min(0f)] [SerializeField] float ramRadius = 20f;

        [Header("The glide (a rider's steering)")]
        [Tooltip("The share of a rider's turn rate the Ray answers with. 1 turns like every other creature; the lab's Ray is 0.3.")]
        [Range(0.05f, 1f)] [SerializeField] float riddenTurnScale = 0.3f;

        [Tooltip("How fast its speed eases to the rider's, per second. Every other creature is 2.5; the lab's Ray is 0.9.")]
        [Min(0.05f)] [SerializeField] float riddenSpeedEase = 0.9f;

        [Tooltip("It has no up: under a rider whose leash flies a frame (the Jewel Wasp) it takes that frame's up, so it rolls as the rider rolls.")]
        [SerializeField] bool carriesRiderUp = true;

        protected override float LeashTurnScale => riddenTurnScale;
        protected override float LeashSpeedEase => riddenSpeedEase;

        protected override Vector3 LeashUp(Vector3 heading) =>
            carriesRiderUp && Leash is IFaunaLeashFrame frame && frame.TryFrameUp(this, out var up) ? up : base.LeashUp(heading);

        public override int PossessedSuction(float reachScale, int maxBites, bool allSides)
        {
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
