using System;

namespace CosmicShore.Data
{
    /// <summary>
    /// The diminishing-returns curve every artifact feeds on (Docs/Pilots/INVENTORY.md §2.3; Garrett, 2026-11-07: "it
    /// should accept infinite elements but with diminishing returns").
    ///
    /// <para><b>The curve is hyperbolic (saturating):</b></para>
    /// <code>level(c) = base + (ceiling - base) * c / (c + half)</code>
    /// <para>where <c>c</c> is every crystal ever fed, <c>base</c> the level the artifact was rolled with, <c>ceiling</c>
    /// its rolled ceiling and <c>half</c> its rolled "half-way" count: <c>half</c> crystals take it half the way from base
    /// to ceiling, <c>3 x half</c> three quarters, <c>9 x half</c> nine tenths. Every crystal still adds something, and
    /// nothing ever reaches the ceiling, so there is no cap to hit and no point where feeding stops working; the marginal
    /// gain <c>(ceiling - base) * half / (c + half)^2</c> falls with the square of the pile.</para>
    ///
    /// <para><b>Why not a logarithm.</b> <c>log(1 + c)</c> also diminishes but never stops growing, so a hoarder's
    /// artifact would outgrow the 0-10 band every transform is authored on. The hyperbola keeps every artifact inside the
    /// band (a transform's level-10 value is still the most it can ever do) while infinite input stays meaningful.</para>
    /// </summary>
    public static class ArtifactCurve
    {
        /// <summary>The level after <paramref name="invested"/> crystals.</summary>
        public static float Level(long invested, float baseLevel, float ceiling, float half)
        {
            if (invested <= 0 || ceiling <= baseLevel) return baseLevel;
            double c = invested;
            double h = Math.Max(1e-3, half);
            return (float)(baseLevel + (ceiling - baseLevel) * (c / (c + h)));
        }

        /// <summary>What the next <paramref name="more"/> crystals would add on top of <paramref name="invested"/>.</summary>
        public static float Gain(long invested, long more, float baseLevel, float ceiling, float half) =>
            Level(invested + Math.Max(0, more), baseLevel, ceiling, half) - Level(invested, baseLevel, ceiling, half);

        /// <summary>
        /// The crystals it takes, in total, to reach <paramref name="level"/>; <see cref="long.MaxValue"/> when the level is
        /// at or above the ceiling (never reached). 0 at or below base.
        /// </summary>
        public static long CrystalsFor(float level, float baseLevel, float ceiling, float half)
        {
            if (level <= baseLevel) return 0;
            if (level >= ceiling) return long.MaxValue;
            double f = (level - baseLevel) / (double)(ceiling - baseLevel);
            double c = Math.Ceiling(Math.Max(1e-3, half) * f / (1.0 - f) - 1e-9);
            return c >= long.MaxValue ? long.MaxValue : (long)c;
        }
    }
}
