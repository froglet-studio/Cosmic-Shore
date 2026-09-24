using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Tuning for the WAKE — a warhead's SHOCKWAVE FRONT (<c>PrismWake</c>, <c>PrismWake.hlsl</c>,
    /// <c>HighPolyPrismMesh</c>, Docs/PRISM_ANIMATION.md §4.7.3).
    ///
    /// A round carrying a live warhead throws a thin spherical SHELL of rippled prisms outward,
    /// over and over, all the way to its target: born at the shell's own half-thickness and dying
    /// at exactly the radius that warhead's blast will reach. The prisms in that volume are swapped
    /// to a high-poly copy of the identical solid (<see cref="Subdivision"/>) so the surface
    /// RIPPLES instead of hinging, and the swap happens where the front is provably zero
    /// (<see cref="ResidencyMargin"/>) so it is never seen.
    ///
    /// <para><b>The REACH is not authored here, and that is the design.</b> It is the warhead's own
    /// blast radius, which the carrier reports live through <c>IPrismWakeCarrier</c> — so the front
    /// draws the weapon's real reach rather than a number somebody tuned to look like it, it grows
    /// with MASS exactly as the warhead does, and it cannot drift from the blast it is describing.
    /// Every length below is therefore a FRACTION of that reach rather than a world distance.</para>
    ///
    /// <para>The deformation itself is a GLOBAL shader uniform written once per frame — there is no
    /// per-prism animation state and no per-prism cost to pay for widening the reach. What DOES
    /// cost is the residency swap, bounded by <see cref="MaxResidentPrisms"/> and
    /// <see cref="Subdivision"/>, and that budget is SHARED across every live front — which is the
    /// arithmetic behind the design rule that this belongs to a FEW things (today: the Sparrow's
    /// HEAVY skyburst, and nothing else) rather than to every vessel.</para>
    ///
    /// <para><b>There is deliberately no SPEED window.</b> The first cut gated the effect on the
    /// carrier's speed and its engage speed was authored above the top speed of the hull the mode
    /// actually flew, so the window never opened — which reads on screen exactly like an effect
    /// that is too weak. A warhead's criterion was never speed anyway: it is whether the round is
    /// carrying a warhead at all, which <c>Projectile.WarheadBlastRadiusMultiplier</c> already
    /// answers (zero on the base rocket, zero on every round in the fleet that is not a skyburst).
    /// A gate that cannot be authored shut is worth more than one that can be authored wrong.</para>
    ///
    /// Place the asset at <c>Resources/PrismWakeConfig</c>. With no asset the defaults below
    /// apply, so the feature works out of the box.
    /// </summary>
    [CreateAssetMenu(fileName = "PrismWakeConfig", menuName = "ScriptableObjects/Rendering/Prism Wake Config")]
    public class PrismWakeConfigSO : ScriptableObject
    {
        /// <summary>
        /// Radians per unit of <c>s</c> in the front's wavelet — <c>2*pi</c> per whole cycle. The
        /// wavelet is <c>P(s) = (1-s^2)^2 * sin(2*pi*Q*s)</c> and its steepest slope is exactly
        /// <c>2*pi*Q</c>, at <c>s = 0</c> (the window is 1 and flat there, so the sine's own slope
        /// is the whole of it). That single number is the entire no-fold bound — see
        /// <see cref="FoldingAmplitude"/>.
        /// </summary>
        public const float TwoPi = 6.2831853071795862f;

        [Tooltip("Master switch. Off publishes an empty bank and swaps nothing, which makes the " +
                 "whole effect exactly free rather than merely cheap.")]
        [SerializeField] bool enabled = true;

        [Tooltip("How deep the ripple is, as a fraction of the front's own HALF-THICKNESS. Peak " +
                 "radial displacement is amplitude x halfThickness world units, so a thicker front " +
                 "carries a deeper ripple — which is what a front is. CLAMPED against folding: the " +
                 "bound is amplitude < 1/(2*pi*wavesInFront), one dimensionless number with no " +
                 "radius, reach or thickness in it, so retuning any of those cannot invalidate it.")]
        [Min(0f)]
        [SerializeField] float amplitude = 0.14f;

        [Tooltip("Whole wave cycles ACROSS the shell. This is the BANDWIDTH dial and the reason the " +
                 "effect reads as a front rather than as a corrugation: 1 is a single wavelet — one " +
                 "crest and one trough arriving and passing — where a high count fills the shell " +
                 "with a standing train. Whole numbers only: the sine has to vanish at both faces " +
                 "of the shell, which is what makes them seamless as they sweep through mass.")]
        [Min(1)]
        [SerializeField] int wavesInFront = 1;

        [Tooltip("The front's HALF-THICKNESS as a fraction of the warhead's reach. 0.25 makes the " +
                 "shell half the reach thick end to end. It is also the amplitude's unit (above) " +
                 "and the radius the front is BORN at, so the shell can never straddle the round's " +
                 "own centre — which is what makes the no-fold proof need only one condition.")]
        [Range(0.05f, 0.45f)]
        [SerializeField] float halfThicknessFraction = 0.25f;

        [Tooltip("How many fronts leave the round per second. Each one travels from the shell's own " +
                 "half-thickness out to the full reach and dies there; the next is already on its " +
                 "way. This is the PULSE RATE — raise it and the mass throbs, lower it and each " +
                 "front is a single readable sweep.")]
        [Range(0.1f, 8f)]
        [SerializeField] float pulsesPerSecond = 1.6f;

        [Tooltip("Quads per face axis on the high-poly prism the front swaps in: 12 is 1,728 " +
                 "triangles against the authored prism's 24. The mesh is SHARED, so every resident " +
                 "prism still draws in ONE instanced batch whatever this is — the cost is triangles, " +
                 "not draw calls.")]
        [Range(2, 32)]
        [SerializeField] int subdivision = 12;

        [Tooltip("Hard ceiling on how many prisms may hold the high-poly mesh at once, per frame, " +
                 "ACROSS EVERY LIVE FRONT. The budget is split evenly between them, so this is the " +
                 "number that makes granting the effect to N things DIVIDE the one that mattered " +
                 "rather than multiply the cost — and the reason the grant is a design call.")]
        [Range(0, 256)]
        [SerializeField] int maxResidentPrisms = 96;

        [Tooltip("Extra world units beyond the front's own reach at which a prism becomes resident. " +
                 "It exists so the mesh swap happens where the map provably cannot have moved a " +
                 "vertex: a prism that entered residency inside a live shell would POP.")]
        [Min(0f)]
        [SerializeField] float residencyMargin = 24f;

        [Tooltip("Seconds the effect takes to reach full strength once a round arms its warhead. " +
                 "A round leaving the bay should not arrive with a front already at full depth.")]
        [Min(0f)]
        [SerializeField] float engageSeconds = 0.15f;

        [Tooltip("Seconds the effect takes to fade once the round stops carrying a live warhead — " +
                 "a detonation, a despawn, a pool return. Continuity of existence: a front eases " +
                 "out rather than blinking off.")]
        [Min(0f)]
        [SerializeField] float releaseSeconds = 0.35f;

        public bool Enabled => enabled;

        /// <summary>Whole cycles across the shell; at least one, or there is no wavelet.</summary>
        public int WavesInFront => Mathf.Max(1, wavesInFront);

        /// <summary>
        /// The amplitude at which the map first folds: <c>1/(2*pi*Q)</c>. At exactly this value the
        /// radial stretch <c>a = 1 + A*P'(s)</c> reaches zero at <c>s = 0</c> and the surface turns
        /// inside out. Derived from <see cref="WavesInFront"/> rather than hard-coded, because the
        /// bound MOVES when the bandwidth does — a sharper pulse folds at a smaller amplitude, and
        /// a constant here would be silently wrong the first time somebody raised the cycle count.
        /// </summary>
        public float FoldingAmplitude => 1f / (TwoPi * WavesInFront);

        /// <summary>
        /// The authored amplitude, held at 90% of <see cref="FoldingAmplitude"/>. The 10% is margin
        /// against float arithmetic near the bound, not slack to spend.
        /// </summary>
        public float Amplitude => Mathf.Clamp(amplitude, 0f, 0.9f * FoldingAmplitude);

        public float HalfThicknessFraction => Mathf.Clamp(halfThicknessFraction, 0.05f, 0.45f);
        public float PulsesPerSecond => Mathf.Clamp(pulsesPerSecond, 0.1f, 8f);
        public int Subdivision => Mathf.Clamp(subdivision, 2, 32);
        public int MaxResidentPrisms => Mathf.Max(0, maxResidentPrisms);
        public float ResidencyMargin => Mathf.Max(0f, residencyMargin);
        public float EngageSeconds => Mathf.Max(0f, engageSeconds);
        public float ReleaseSeconds => Mathf.Max(0f, releaseSeconds);

        /// <summary>
        /// The front's half-thickness in world units for a warhead of this reach, and the radius the
        /// front is born at. One method so the publisher, the residency pass and every test read the
        /// same arithmetic.
        /// </summary>
        public float HalfThicknessFor(float reach) => Mathf.Max(1e-4f, reach * HalfThicknessFraction);

        /// <summary>
        /// The radius the residency pass must sweep for a warhead of this reach — the whole volume
        /// the map can EVER move a vertex in, plus <see cref="ResidencyMargin"/>.
        ///
        /// <para>It is <c>reach + sigma + margin</c> and not <c>reach + margin</c>, because the front
        /// dies AT the reach and the shell reaches <c>sigma</c> past its own centre: the outermost
        /// displaced vertex of a pulse's last frame sits at <c>reach + sigma</c>. With the margin
        /// alone, a prism entering the volume there would be swapped to the dense mesh at a radius
        /// where its vertices are already displaced, and it would POP — the one thing §4.2's
        /// invisible-swap contract forbids.</para>
        ///
        /// <para><b>The margin is an absolute distance and the shell is a FRACTION of the reach</b>,
        /// so "margin covers the overshoot" is a coincidence that holds at one authored pair and
        /// silently stops holding at the next: it was true by 0.2 of a unit at the shipped
        /// 0.25/24 pair and false by 19 units the first time the shell was thickened. Adding sigma
        /// makes the contract structural instead — the margin goes back to being a margin.</para>
        /// </summary>
        public float ResidencyRadiusFor(float reach) =>
            reach + HalfThicknessFor(reach) + ResidencyMargin;

        /// <summary>
        /// Where the front sits for a pulse <paramref name="u"/> of the way through its life, in
        /// world units from the round's centre: from the shell's own half-thickness (so it never
        /// straddles the centre — the second half of the no-fold proof) out to the full reach.
        /// </summary>
        public float FrontRadiusAt(float u, float reach)
        {
            float sigma = HalfThicknessFor(reach);
            return Mathf.Lerp(sigma, Mathf.Max(sigma, reach), Mathf.Clamp01(u));
        }

        /// <summary>
        /// A front's own strength envelope over its life, <c>4*S(u)*S(1-u)</c> with S the smoothstep
        /// polynomial: exactly zero — value AND slope — at birth and at the reach, exactly 1 in the
        /// middle. Both ends matter and for different reasons. At the reach it is continuity of
        /// existence: a front must not blink out at the edge of the blast volume. At BIRTH it is the
        /// recycle: the next front appears at the shell's half-thickness, where a full-strength
        /// arrival would pop, and a bump means nothing ever appears or disappears at all.
        /// </summary>
        public static float FrontEnvelope(float u)
        {
            if (u <= 0f || u >= 1f) return 0f;
            float v = 1f - u;
            float su = u * u * (3f - 2f * u);
            float sv = v * v * (3f - 2f * v);
            return 4f * su * sv;
        }

        /// <summary>Is this asset inside every range the map and the proof assume?</summary>
        public bool IsSane =>
            Amplitude >= 0f && Amplitude < FoldingAmplitude &&
            WavesInFront >= 1 &&
            HalfThicknessFraction > 0f && HalfThicknessFraction <= 0.45f &&
            PulsesPerSecond > 0f &&
            Subdivision >= 2 && Subdivision <= 32 &&
            MaxResidentPrisms >= 0 &&
            ResidencyMargin >= 0f;

        /// <summary>
        /// The no-fold bound, stated as the one comparison the HLSL header derives: the radial
        /// stretch <c>a = 1 + A*P'(s)</c> stays positive for every vertex, every reach and every
        /// thickness iff <c>A * 2*pi*Q &lt; 1</c>. Asserted by <c>PrismWakeTests</c> against the
        /// shipped asset and MEASURED over the whole authored range by
        /// <c>Tools/Shaders/verify_prism_wake.py</c>.
        /// </summary>
        public bool NeverFolds => Amplitude * TwoPi * WavesInFront < 1f;
    }
}
