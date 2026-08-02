using UnityEngine;

namespace CosmicShore.Gameplay
{
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

        public FloraPlantingSite(Vector3 position, Vector3 up)
        {
            Position = position;
            Up = up.sqrMagnitude > 0.0001f ? up.normalized : Vector3.up;
        }
    }
}
