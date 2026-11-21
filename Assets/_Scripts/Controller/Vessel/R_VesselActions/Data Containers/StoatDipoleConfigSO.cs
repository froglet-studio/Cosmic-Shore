using CosmicShore.Data;
using CosmicShore.Gameplay;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The Stoat's flight-clock WARP and the colours of the swing's drawn reach ring and exit line, read by
    /// <see cref="CosmicShore.Gameplay.StoatPathfinderExecutor"/> (<c>R_VesselActions/STOAT_DIPOLE.md</c> §1b). The swing's
    /// own numbers are <see cref="StoatSlingConfigSO"/>'s.
    ///
    /// <para><b>Why the name.</b> This was the config of the Stoat's FIELD DIPOLE and its predicted-path PATHFINDER (the
    /// Stoat Flight Studio's round-15 field trajectory). The dipole left the kit on 2026-11-02 and its code, its path, Time 5's
    /// Foresight and every row they read were deleted on 2026-11-21 (Yash: "delete all other old pathfinding"). The type and
    /// its asset keep their names so the prefab, the studio map and the tools that read the warp keep their references.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "StoatDipoleConfig", menuName = "ScriptableObjects/Vessel Actions/Stoat Dipole Config")]
    public sealed class StoatDipoleConfigSO : ScriptableObject
    {
        [Header("The warp (Time)")]
        [Tooltip("Time is rate: the warp boost, a multiple of the hull's flight clock while the swing warps it — 2.5× at rest, " +
                 "3.75× at level 5, 5× at level 10, floored at 1 (never a slow-down). The swing takes catchWarpShare of it.")]
        [SerializeField] ElementalFloat boost = ElementalFloat.Multiplier(2.5f, 5f, Element.Time, 1.25f);

        [Tooltip("How fast the boost arrives, per second.")]
        [SerializeField, Range(0.1f, 60f)] float boostRise = 20f;

        [Tooltip("Seconds the boost takes to fade once nothing warps the flight.")]
        [SerializeField, Range(0.01f, 5f)] float boostFadeSeconds = 0.6f;

        [Header("The drawn reach ring and exit line (the local human pilot only)")]
        [Tooltip("The smallest a far dot may get on screen, pixels, so it stays readable.")]
        [SerializeField, Range(0f, 10f)] float worldDotMinPixels = 2.5f;

        [Tooltip("The ring's and line's colour before the swing starts (blue-grey).")]
        [SerializeField] Color openColor = new(150f / 255f, 166f / 255f, 194f / 255f, 1f);

        [Tooltip("The ring's and line's colour while the swing turns you, and the fling's ripple (lime).")]
        [SerializeField] Color warpedColor = new(157f / 255f, 1f, 46f / 255f, 1f);

        [Header("Audio (FMOD) — every sound is an exposed, editable field")]
        [Tooltip("Played when the warp comes on. Empty = silent.")]
        [SerializeField] EventReference warpEvent;

        public ElementalFloat Boost => boost;
        public float BoostRise => boostRise;
        public float BoostFadeSeconds => boostFadeSeconds;
        public float WorldDotMinPixels => worldDotMinPixels;
        public Color OpenColor => ColorTrace.Retrace(ColorTrace.World, openColor);
        public Color WarpedColor => ColorTrace.Retrace(ColorTrace.World, warpedColor);
        public EventReference WarpEvent => warpEvent;
    }
}
