using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Crystal manager specialized for Shape Drawing Mode.
    ///
    /// Inherits from CrystalManager and overrides ExplodeCrystal so that
    /// when a crystal is hit, instead of a random respawn, we fire OnWaypointCrystalHit.
    /// ShapeDrawingManager listens to this event and decides whether to spawn the next
    /// waypoint crystal or end the sequence.
    ///
    /// Place this component on the same GameObject as (or replacing) CrystalManager
    /// during Shape Drawing Mode. ShapeDrawingManager handles swapping managers.
    /// </summary>
    public class ShapeDrawingCrystalManager : CrystalManager
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
        /// Shape drawing crystals do NOT respawn. The sequence manager handles spawning
        /// the next crystal. This prevents the base CrystalManager from trying to
        /// calculate a new position (which fails when Cell is disabled).
        /// </summary>
        public override void RespawnCrystal(int crystalId)
        {
            // Just destroy the crystal — ShapeDrawingManager.HandleCrystalHit
            // will spawn the next waypoint crystal.
            if (cellData.TryGetCrystalById(crystalId, out var crystal) && crystal)
                crystal.DestroyCrystal();
        }

        /// <summary>
        /// Public helper: spawn a crystal at an exact world position with a known ID.
        /// Used by ShapeDrawingManager to place waypoint crystals precisely.
        /// </summary>
        public Crystal SpawnAtPosition(int crystalId, Vector3 worldPosition)
        {
            var crystal = SpawnLocal(crystalId, worldPosition);
            cellData.AddCrystalToList(crystal);
            return crystal;
        }
    }
}
