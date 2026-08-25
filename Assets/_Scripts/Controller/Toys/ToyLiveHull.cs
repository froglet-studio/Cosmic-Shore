using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Marks a toy mini hull that wears the SHIP'S OWN materials rather than a flat preview fill
    /// (<see cref="VesselModelBuilder.TryBuildLive"/>).
    ///
    /// It exists because the two kinds of mini hull must be re-tinted in opposite ways, and one
    /// list can hold both. A FLAT model owns a preview material built for it, so a domain change
    /// repaints that material directly. A LIVE model draws with shared PROJECT assets — the
    /// domain ship material, the vessel's body and window materials — so repainting them would
    /// recolour every ship in the game, in the editor, permanently. A live hull is re-tinted by
    /// swapping which shared material it points at and re-stamping the vision band's per-renderer
    /// mark instead.
    ///
    /// A marker rather than an inferred test (checking whether a material is a project asset) on
    /// purpose: the consequence of getting it wrong is corrupting shipped assets, and that is not
    /// a thing to leave to a heuristic.
    /// </summary>
    public sealed class ToyLiveHull : MonoBehaviour
    {
    }
}
