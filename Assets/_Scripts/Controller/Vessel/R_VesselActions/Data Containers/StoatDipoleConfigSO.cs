using CosmicShore.Data;
using CosmicShore.Gameplay;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The Stoat's flight-clock WARP, read by
    /// <see cref="CosmicShore.Gameplay.StoatPathfinderExecutor"/> (<c>R_VesselActions/STOAT_DIPOLE.md</c> §1b). The swing's
    /// own numbers are <see cref="StoatSlingConfigSO"/>'s.
    ///
    /// <para><b>Why the name.</b> This was the config of the Stoat's FIELD DIPOLE and its predicted-path PATHFINDER (the
    /// Stoat Flight Studio's round-15 field trajectory). The dipole left the kit on 2026-11-02 and its code, its path, Time 5's
    /// Foresight and every row they read were deleted on 2026-11-21 (Yash: "delete all other old pathfinding"), and the
    /// drawn ring and line's dot size and colours on 2026-11-22 (nothing is drawn). The type and
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

        [Header("Audio (FMOD) — every sound is an exposed, editable field")]
        [Tooltip("Played when the warp comes on. Empty = silent.")]
        [SerializeField] EventReference warpEvent;

        public ElementalFloat Boost => boost;
        public float BoostRise => boostRise;
        public float BoostFadeSeconds => boostFadeSeconds;
        public EventReference WarpEvent => warpEvent;
    }
}
