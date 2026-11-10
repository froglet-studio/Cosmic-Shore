using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A mode's say over WHERE round an authored anchor a crystal stands. <see cref="CrystalManager"/> asks the one on
    /// its own GameObject (found in <c>Awake</c>) before it falls back to the platform's random shell
    /// (<c>anchorJitterRadius</c>). Asked on the server only - the position is then replicated like any other. A mode
    /// with none, every mode but Skim Race today, is exactly what it was.
    /// </summary>
    public interface ICrystalAnchorPlacement
    {
        /// <summary>Where a crystal spawned against <paramref name="anchor"/> stands. <paramref name="crystalPrefab"/>
        /// is the manager's crystal (its ring, if it is one, says the mouth). False = use the random shell.</summary>
        bool TryPlaceAroundAnchor(Vector3 anchor, Crystal crystalPrefab, out Vector3 position);
    }
}
