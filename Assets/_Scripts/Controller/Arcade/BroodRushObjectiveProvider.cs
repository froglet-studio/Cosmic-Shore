using CosmicShore.UI;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Objective provider for Brood Rush: the cell's NUCLEUS.
    ///
    /// The nucleus is the whole mode. Control is decided only by the environment volume laid
    /// inside it (<see cref="Cell.DominantDomain"/> reads the nucleus-interior tally), and every
    /// 30 s wave the cell births under a genuine nucleus claim is a brood point for the claimant.
    /// The exterior is the feeding ground and never sways control, so a new pilot who drifts out
    /// to chase tadpoles or crystals is playing the wrong game - and the cell is large enough
    /// (SenseRadiusOverride 1000) that the way back is not always obvious. BROODRUSH.md has listed
    /// "a nucleus-pointing arrow would help new players" as a follow-up since the mode shipped.
    ///
    /// <para><b>Hidden while you are inside it.</b> Inside the nucleus there is nothing to point
    /// at - you are on the objective, laying the claim - and an arrow aimed at the centre of the
    /// sphere you are flying through would only spin. It returns the moment you leave.</para>
    ///
    /// <para>Not the wave. Fauna are client-local and are the score CLOCK, not the score: a wave
    /// is born from the nucleus claim rather than earned by reaching it, so pointing at tadpoles
    /// would lead a pilot away from the only thing that decides who they hatch for.</para>
    ///
    /// <para><b>Multiplayer.</b> The nucleus sits at the cell's own transform
    /// (<see cref="Cell.IsInsideNucleus"/> measures from it), which is scene-placed and identical
    /// on every peer, and the nucleus radius is derived locally from the spawned nucleus renderer.
    /// Nothing here reads server-only state.</para>
    /// </summary>
    public class BroodRushObjectiveProvider : MonoBehaviour, IObjectiveProvider
    {
        [Header("Dependencies")]
        [Inject] GameDataSO gameData;

        Cell _cell;

        public bool TryGetObjective(out Transform target)
        {
            target = null;

            if (gameData == null) return false;
            var vesselTf = gameData.LocalPlayer?.Vessel?.Transform;
            if (vesselTf == null) return false;

            // Re-resolve when the cached cell is gone (scene reload for replay).
            if (!_cell) _cell = Cell.FindNearestActiveCell(vesselTf.position, sceneCellsOnly: true);
            if (!_cell) return false;

            // On the objective already: nothing to point at.
            if (_cell.IsInsideNucleus(vesselTf.position)) return false;

            target = _cell.transform;
            return true;
        }
    }
}
