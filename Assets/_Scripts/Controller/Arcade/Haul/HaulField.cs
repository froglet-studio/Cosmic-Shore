using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Haul's field, kept pure so the edit-mode tests and the generator's twin can hold it without a scene.
    ///
    /// <para><b>A field is BALES of neutral prisms</b> laid by the mode on every peer from the intensity alone: the
    /// geometry is Chum's reef (<see cref="ChumReef"/>: a centre prism and Fibonacci shells, on a golden-angle spiral a
    /// band of the way from the nucleus to the membrane), so no two prisms touch and the same field is laid on every
    /// machine. Only the ladder is Haul's own. The prisms are <c>Domains.Blue</c> (no team), so a Thresher's Mass Mode
    /// ball STEALS them into its colour (THRESHER.md 2b).</para>
    ///
    /// <para><b>The crust.</b> From I3 the outer shell of every bale is SHIELDED: a steal only sheds a shield, so a
    /// crusted bale takes two passes of the ball.</para>
    /// </summary>
    public static class HaulField
    {
        /// <summary>I1 Hayfield, I2 Harvest, I3 Granary, I4 Stronghold (HAUL.md). The generator mirrors this table.</summary>
        public static ChumReef.Rung ForIntensity(int intensity) => Mathf.Clamp(intensity, 1, 4) switch
        {
            1 => new ChumReef.Rung(10, 2, 0.30f, false),
            2 => new ChumReef.Rung(14, 2, 0.45f, false),
            3 => new ChumReef.Rung(14, 3, 0.45f, true),
            _ => new ChumReef.Rung(18, 3, 0.60f, true),
        };

        /// <summary>Every prism of the rung, bale by bale (cleared first). Pure: the same list on every peer.</summary>
        public static void Lay(int intensity, float nucleusRadius, float membraneRadius, List<ChumReef.Piece> into) =>
            ChumReef.Lay(ForIntensity(intensity), nucleusRadius, membraneRadius, into);
    }
}
