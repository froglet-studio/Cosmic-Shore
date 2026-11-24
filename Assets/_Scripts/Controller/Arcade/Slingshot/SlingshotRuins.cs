using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Slingshot's garden ruins (<c>Arcade/SLINGSHOT.md</c>, "The ruins"): Spearline's seven kinds
    /// (<see cref="SpearlineRuins"/>, the one generator) laid round the Stoat's circuit, on its hardest rung only and on
    /// Spearline's own clearances: a 60 u lane down every leg's flown line (the I4 mouth is 26 u), nothing within 120 u
    /// of a ring (so nothing stands at a corner's moon), and only on legs of 380 u or more.
    ///
    /// <para>Pure: a function of the lap and the rung, so every peer lays the same ruins from the broadcast rings and
    /// <c>SlingshotRuinsTests</c> holds it. Decoration: nothing here reads or writes a vessel.</para>
    /// </summary>
    public static class SlingshotRuins
    {
        /// <summary>The rung's ruins: Spearline's, unchanged (none below I4).</summary>
        public static SpearlineRuinSettings ForIntensity(int intensity) => SpearlineRuins.ForIntensity(intensity);

        /// <summary>Every ruin prism of <paramref name="lap"/> on this rung into <paramref name="parts"/> (cleared
        /// first); the turning ones are also named in <paramref name="wheels"/>.</summary>
        public static void Lay(IReadOnlyList<RaceGate> lap, int intensity, List<SpearlineRuinPart> parts,
                               List<SpearlineRuinWheel> wheels)
        {
            parts.Clear();
            wheels.Clear();
            SpearlineRuins.Lay(lap, ForIntensity(intensity), parts, wheels);
        }
    }
}
