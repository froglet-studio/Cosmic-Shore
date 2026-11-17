using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Ray: a grown glider (<c>Tools/Build/fauna_kit/specs/ray.py</c>). It is a <see cref="LightFauna"/> in every
    /// way but its mouth. A ray feeds on the wing: it does not reach ahead for a mouthful, it eats what passes between
    /// its lobes. So a rider's suction (<see cref="Fauna.PossessedSuction"/>) takes the prisms round its mouth, on
    /// every side, within <see cref="ramRadius"/>, instead of a sphere pushed out ahead of it.
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
