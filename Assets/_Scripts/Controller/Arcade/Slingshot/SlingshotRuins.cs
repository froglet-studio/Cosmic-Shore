using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Slingshot's garden ruins (<c>Arcade/SLINGSHOT.md</c>, "The ruins"): Spearline's seven kinds
    /// (<see cref="SpearlineRuins"/>, the one generator) laid round the Stoat's circuit, on its hardest rung only: a 150 u
    /// lane down every leg's flown line (Spearline's is 60 u; the I4 mouth is 26 u), nothing within 120 u
    /// of a ring (so nothing stands at a corner's moon), and only on legs of 380 u or more.
    ///
    /// <para>Pure: a function of the lap and the rung, so every peer lays the same ruins from the broadcast rings and
    /// <c>SlingshotRuinsTests</c> holds it. Decoration: nothing here reads or writes a vessel.</para>
    /// </summary>
    public static class SlingshotRuins
    {
        /// <summary>The lane (u) kept clear down every leg's flown line. Spearline's is 60 u; the Stoat is thrown off its
        /// line by its swing, and the first I4 race in the editor (2026-11-24) saw three AI Stoats break 55 of 350 ruin
        /// prisms on that lane. 150 u is the swing's widest orbit radius in the race model
        /// (<c>Tools/Build/stoat_orbit_sim.py</c>, <c>WIDE</c>). Yash, 2026-11-24: "widen the lane".</summary>
        public const float ClearRadius = 150f;

        /// <summary>The rung's ruins: Spearline's (none below I4) on the Stoat's wider lane.</summary>
        public static SpearlineRuinSettings ForIntensity(int intensity)
        {
            var s = SpearlineRuins.ForIntensity(intensity);
            return s.With(ClearRadius, SpearlineRuins.MinLegLength, SpearlineRuins.RingMargin);
        }

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
