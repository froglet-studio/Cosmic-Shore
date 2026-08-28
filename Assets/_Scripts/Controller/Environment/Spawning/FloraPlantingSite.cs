using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What KIND of ground a planting site is. An environment describes its ground; a flora
    /// config (<c>FloraConfigurationSO.PreferredSites</c>) declares where that species grows;
    /// the match is made when the site is dealt. Nothing is scripted - a species with no
    /// preference takes whatever is next, and a preference with no matching ground silently
    /// falls back, so a garden that removes its pool doesn't mute its reeds.
    /// </summary>
    [System.Flags]
    public enum FloraSiteKind
    {
        None = 0,
        /// <summary>Open soil - a terrace bed, a border. Grows up, room to spread.</summary>
        Bed = 1 << 0,
        /// <summary>The foot of something climbable - a column, a trellis, an arch.</summary>
        Climb = 1 << 1,
        /// <summary>A suspended container. The normal points DOWN; what roots here trails.</summary>
        Basket = 1 << 2,
        /// <summary>A water margin - pool rim, channel edge.</summary>
        Water = 1 << 3,
        /// <summary>A high narrow perch - a trellis crown, a wall head. Exposed, little soil.</summary>
        Ledge = 1 << 4,

        Any = Bed | Climb | Basket | Water | Ledge,
    }

    /// <summary>
    /// A spot an authored cell environment has prepared for a plant: a bed, a trellis foot, a
    /// hanging basket. Position is in the environment's own generation space (which the Cell
    /// parents at its local origin, so it is also cell-local); <see cref="Up"/> is the surface
    /// normal the flora should grow away from.
    ///
    /// <b>Why this exists.</b> Flora normally disperse themselves across a random shell of the
    /// membrane (<see cref="Flora.ResolvePlantRadius"/>) - correct for an unstructured cell,
    /// wrong for a GARDEN, where the whole point is that the architecture and the planting are
    /// one composition. Rather than let a garden environment spawn its own flora (a parallel
    /// system - the Cell owns the ecology), the environment publishes SITES and the cell's
    /// ordinary spawner plants into them through the ordinary spawn path. The flora are
    /// ordinary food-web citizens the moment they exist: grazeable, joustable, starvable,
    /// crystal-dropping. Nothing here spawns, removes, or ages a prism.
    /// </summary>
    public readonly struct FloraPlantingSite
    {
        public readonly Vector3 Position;
        public readonly Vector3 Up;
        public readonly FloraSiteKind Kind;

        public FloraPlantingSite(Vector3 position, Vector3 up, FloraSiteKind kind = FloraSiteKind.Bed)
        {
            Position = position;
            Up = up.sqrMagnitude > 0.0001f ? up.normalized : Vector3.up;
            Kind = kind == FloraSiteKind.None ? FloraSiteKind.Bed : kind;
        }
    }
}
