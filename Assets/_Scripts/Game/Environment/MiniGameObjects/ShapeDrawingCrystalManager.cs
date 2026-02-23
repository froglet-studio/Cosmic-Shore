using UnityEngine;

namespace CosmicShore.Game.ShapeDrawing
{
    /// <summary>
    /// Crystal manager specialized for Shape Drawing Mode.
    ///
    /// Inherits from LocalCrystalManager and overrides ExplodeCrystal so that
    /// when a crystal is hit, instead of a random respawn, we fire OnWaypointCrystalHit.
    /// ShapeDrawingManager listens to this event and decides whether to spawn the next
    /// waypoint crystal or end the sequence.
    ///
    /// Place this component on the same GameObject as (or replacing) LocalCrystalManager
    /// during Shape Drawing Mode. ShapeDrawingManager handles swapping managers.
    /// </summary>
    public class ShapeDrawingCrystalManager : CosmicShore.Game.LocalCrystalManager
    {
        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>
        /// Fired when a shape waypoint crystal is hit.
        /// Arg: the crystal's Id (== waypoint index + 1, since crystal IDs start at 1).
        /// </summary>
        public event System.Action<int> OnWaypointCrystalHit;

        // ── Overrides ─────────────────────────────────────────────────────────

        /// <summary>
        /// Intercepts the standard explode flow.
        /// We still explode the crystal visually, but we do NOT respawn randomly.
        /// Instead we fire OnWaypointCrystalHit so ShapeDrawingManager advances the sequence.
        /// </summary>
        public override void ExplodeCrystal(int crystalId, Crystal.ExplodeParams explodeParams)
        {
            // Run the visual explosion (audio + spent crystal VFX)
            if (cellData.TryGetCrystalById(crystalId, out var crystal))
            {
                if (crystal != null)
                    crystal.Explode(explodeParams);
            }

            // Notify the shape drawing manager — do NOT call RespawnCrystal here
            OnWaypointCrystalHit?.Invoke(crystalId);
        }

        /// <summary>
        /// Public helper: spawn a crystal at an exact world position with a known ID.
        /// Used by ShapeDrawingManager to place waypoint crystals precisely.
        /// </summary>
        public Crystal SpawnAtPosition(int crystalId, Vector3 worldPosition)
        {
            var crystal = Spawn(crystalId, worldPosition);
            cellData.AddCrystalToList(crystal);
            return crystal;
        }

        /// <summary>
        /// Destroy all currently tracked crystals immediately (no explosion VFX).
        /// Called when shape drawing ends or resets.
        /// </summary>
        public void DestroyAllCrystals()
        {
            var crystals = cellData.Crystals;
            for (int i = crystals.Count - 1; i >= 0; i--)
            {
                var c = crystals[i];
                if (c) c.DestroyCrystal();
            }
        }
    }
}