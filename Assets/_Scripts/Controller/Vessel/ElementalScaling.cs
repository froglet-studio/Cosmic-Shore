using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Maps a vessel's live elemental resource level (Charge / Mass / Space / Time) to a gameplay
    /// multiplier, so the four elements scale four per-vessel parameters — the "four elements map
    /// to four parameters" pillar.
    ///
    /// WHY THIS LIVES HERE (not on an ElementalFloat inside the action SO): in the R_ action
    /// architecture, <see cref="ShipActionSO"/> assets are SHARED and STATELESS ("vessel context is
    /// passed in each call"). A single asset is referenced by every vessel of that class, so an
    /// <see cref="ElementalFloat"/> field on the asset cannot hold per-vessel state — in multiplayer
    /// the last vessel to initialize would drive everyone's value. The per-vessel <b>executor</b>
    /// (a MonoBehaviour on the vessel) is the correct, multiplayer-safe place to read the vessel's
    /// own <see cref="ResourceSystem"/> and scale the SO's authored base value at use-time.
    ///
    /// The mapping is ANCHORED at the resting level (normalized 0 → integer level 0): the multiplier
    /// is exactly 1 there, so the authored base value is unchanged at game start. Elements only add
    /// or subtract power as the player's crystal-driven levels rise above / fall below the resting
    /// band — the intended elemental economy, and a guarantee that this cannot regress a vessel's
    /// baseline feel.
    /// </summary>
    public static class ElementalScaling
    {
        /// <summary>
        /// Normalized element level, where 0 == resting/base and 1 == integer level 10 (a full
        /// crystal charge). Extrapolates to the [-0.5, 1.5] band the ResourceSystem clamps to
        /// (deficit down to -5, overcharge up to +15). Returns 0 when no resource system exists,
        /// so callers fall back to their authored base value.
        /// </summary>
        public static float Level01(IVesselStatus status, Element element)
        {
            var rs = status?.ResourceSystem;
            return rs == null ? 0f : rs.GetNormalizedLevel(element);
        }

        /// <summary>
        /// A multiplier around 1.0 driven by an element's level. At the resting level (t == 0) the
        /// result is exactly 1 (base preserved); at level 10 it is <paramref name="atFull"/>; the
        /// deficit / overcharge extremes extrapolate linearly. Floored at <paramref name="minMul"/>
        /// so it never inverts or collapses to zero.
        /// </summary>
        public static float Multiplier(IVesselStatus status, Element element,
            float atFull = 2f, float minMul = 0.25f)
        {
            float t = Level01(status, element);             // 0 at resting, 1 at level 10
            float mul = Mathf.LerpUnclamped(1f, atFull, t);  // anchored at 1 when t == 0
            return Mathf.Max(minMul, mul);
        }

        /// <summary>
        /// The same lerp as <see cref="Multiplier"/> but with an EXPLICIT resting endpoint, for a
        /// parameter whose design deliberately does not sit at 1x when the element is at rest.
        ///
        /// <see cref="Multiplier"/> anchors at 1 so an element can only ever ADD to a vessel's
        /// authored baseline. That anchor is the right default and stays the default — but it also
        /// means an element can never be given ownership of a parameter's whole RANGE, only its
        /// upside. When a design says "this element takes the value from 0.75x at rest to 1.5x at
        /// level 10" (the Dolphin's Charge → blast thickness), the authored base is the value at
        /// the MIDDLE of the element's range rather than at its floor, and the rest level has to be
        /// authorable. Both endpoints are explicit here so that trade is visible in the asset
        /// instead of hidden in arithmetic.
        ///
        /// Extrapolates through the deficit / overcharge band exactly like <see cref="Multiplier"/>
        /// and is floored by <paramref name="minMul"/> the same way.
        /// </summary>
        public static float MultiplierFromRest(IVesselStatus status, Element element,
            float atRest, float atFull, float minMul = 0.25f)
        {
            float t = Level01(status, element);              // 0 at resting, 1 at level 10
            float mul = Mathf.LerpUnclamped(atRest, atFull, t);
            return Mathf.Max(minMul, mul);
        }

        /// <summary>
        /// Scales an authored base value by an element's level. Equivalent to
        /// <c>baseValue * Multiplier(...)</c>; base is preserved exactly at the resting level.
        /// </summary>
        public static float Scale(IVesselStatus status, Element element, float baseValue,
            float atFull = 2f, float minMul = 0.25f)
            => baseValue * Multiplier(status, element, atFull, minMul);

        /// <summary>
        /// The qualitative-tier threshold: integer level 5 (normalized 0.5) — the
        /// all-five-petals-white flower state on ElementalBarsView.
        /// </summary>
        public const int QualitativeUnlockLevel = 5;

        /// <summary>
        /// Raw local threshold read — true while the vessel's LIVE effective level meets the
        /// unlock threshold. This is the display/cosmetic query. Gameplay-outcome checks
        /// (piercing, shielded prisms, domain-sparing explosions) must go through
        /// <see cref="R_VesselElementalAbilityHandler.IsUpgradeActive"/> instead, which applies
        /// the latch/hysteresis policy and (in multiplayer) the replicated unlock state.
        /// </summary>
        public static bool MeetsQualitativeThreshold(IVesselStatus status, Element element,
            int threshold = QualitativeUnlockLevel)
        {
            var rs = status?.ResourceSystem;
            return rs != null && rs.GetLevel(element) >= threshold;
        }
    }
}
