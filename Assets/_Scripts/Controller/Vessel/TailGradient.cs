using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The one composition of a <b>TAIL</b>'s (or a jet's) colour gradient — the sibling of
    /// <see cref="VesselFXWidth"/>, which owns the other half of "what a tail instance may
    /// override". Contract: <c>Docs/VESSEL_TAIL_AND_JETS.md</c>.
    ///
    /// A tail is painted HEAD-to-CORE from the domain's own trail pair
    /// (<c>TrailHighlightColor</c> → <c>TrailCoreColor</c>), and the streak's authored
    /// <b>alpha</b> curve is preserved — only the colour keys are rebuilt. That split is the
    /// whole rule: the domain owns the hue, the shared prefab owns how the ribbon fades out.
    ///
    /// <b>Why it is not a private method on <see cref="VesselTailAndJets"/> any more.</b> A
    /// vessel is no longer the only thing that carries a tail — the Sparrow's skyburst missile
    /// carries one too, for exactly the reason a vessel does (it is the thing other pilots
    /// need to see coming across a cell). Two independent transcriptions of "what a tail's
    /// gradient is" would drift the first time either is retuned, and a tail that reads
    /// differently depending on what is wearing it stops being one signal.
    ///
    /// The caller owns the alpha-key capture, deliberately: a vessel discovers a growing set of
    /// trails and keys them in a dictionary, a projectile has exactly one and keeps an array.
    /// Capturing here would mean shared mutable state keyed on objects nobody owns.
    /// </summary>
    static class TailGradient
    {
        /// <summary>
        /// Repaint <paramref name="trail"/> as <paramref name="head"/> at the emitter fading to
        /// <paramref name="core"/> at the far end, keeping <paramref name="alphaKeys"/> — which
        /// must be the keys captured from the trail BEFORE its first repaint, or the ribbon
        /// loses its authored fade-out the moment a domain changes.
        /// </summary>
        public static void Apply(TrailRenderer trail, Color head, Color core, GradientAlphaKey[] alphaKeys)
        {
            if (!trail || alphaKeys == null) return;

            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(head, 0f),
                    new GradientColorKey(core, 1f),
                },
                alphaKeys);
            trail.colorGradient = gradient;
        }
    }
}
