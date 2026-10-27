using System;
using CosmicShore.Data;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The seventeen CALLSIGNS (Docs/Pilots/CALLSIGNS.md §2, §6.3): per sight its word, a one-line
    /// meaning, its glyphs and the sting that plays when it opens, plus the look of the sight on LB
    /// (fade, suppression, marks). Every word is authored text (CLAUDE.md, player-facing text): the
    /// rows live in the asset at <c>Resources/Pilots/CallsignSet</c>, written by
    /// <c>Tools/Build/author_callsign_assets.py</c>, never as C# literals.
    /// </summary>
    [CreateAssetMenu(fileName = "CallsignSet", menuName = "ScriptableObjects/Pilots/Callsign Set")]
    public class CallsignSetSO : ScriptableObject
    {
        [Serializable]
        public class Callsign
        {
            [Tooltip("The sight this row describes. One row per PilotVision value except None.")]
            public PilotVision sight;

            [Tooltip("The callsign as the player reads it (ENSIGN).")]
            public string word;

            [Tooltip("What holding LB lights, in one line.")]
            public string meaning;

            [Tooltip("The callsign's glyph (from /ability-icon's kit). Empty until drawn.")]
            public Sprite glyph;

            [Tooltip("The glyph the hull's callsign plate shows while the pilot looks. Empty until drawn.")]
            public Sprite plateGlyph;

            [Tooltip("Plays once when the sight opens. Shipped empty (the FMOD rule).")]
            public EventReference openSound;
        }

        [SerializeField] Callsign[] callsigns = Array.Empty<Callsign>();

        [Header("The sight on LB")]
        [Tooltip("Seconds the sight takes to open fully once LB is held. Nothing pops.")]
        [Min(0.01f)]
        [SerializeField] float fadeInSeconds = 0.25f;

        [Tooltip("Seconds the sight takes to close once LB is released.")]
        [Min(0.01f)]
        [SerializeField] float fadeOutSeconds = 0.35f;

        [Header("Suppression (a post-processing volume weighted by LB)")]
        [Tooltip("Colour Adjustments saturation at full sight (-100 is grey).")]
        [Range(-100f, 0f)]
        [SerializeField] float suppressSaturation = -70f;

        [Tooltip("Colour Adjustments post-exposure at full sight, in EV (negative dims).")]
        [Range(-4f, 0f)]
        [SerializeField] float suppressExposure = -1f;

        [Tooltip("Priority of the suppression volume. Above the gameplay volume, so the sight wins while held.")]
        [SerializeField] float volumePriority = 50f;

        [Header("Marks (crystals, vessels and the other target sights)")]
        [Tooltip("A marked crystal's halo, world radius per unit of the crystal's scale.")]
        [Min(0.1f)]
        [SerializeField] float crystalMarkRadius = 6f;

        [Tooltip("A marked vessel's halo, world radius.")]
        [Min(0.1f)]
        [SerializeField] float vesselMarkRadius = 12f;

        [Tooltip("A crystal's mark colour. White, because elements have no colour (LOCKED); the crystal's own " +
                 "shape says which element it is.")]
        [SerializeField] Color crystalMarkColor = Color.white;

        [Tooltip("Halo brightness at full sight.")]
        [Min(0f)]
        [SerializeField] float markIntensity = 1.2f;

        [Tooltip("Smallest on-screen halo radius, as a fraction of the screen's half-height, so a far mark stays legible.")]
        [Range(0f, 0.5f)]
        [SerializeField] float markMinScreenRadius = 0.02f;

        [Header("Until pilots carry a sight")]
        [Tooltip("The sight a vessel uses when its pilot names none. Pilots get their sight from PilotSO.vision once " +
                 "the pilot types land (Docs/Pilots/CALLSIGNS.md §6.2); until then this is how a tester picks one. " +
                 "None = LB does nothing.")]
        [SerializeField] PilotVision unassignedSight = PilotVision.None;

        public float FadeInSeconds => fadeInSeconds;
        public float FadeOutSeconds => fadeOutSeconds;
        public float SuppressSaturation => suppressSaturation;
        public float SuppressExposure => suppressExposure;
        public float VolumePriority => volumePriority;
        public float CrystalMarkRadius => crystalMarkRadius;
        public float VesselMarkRadius => vesselMarkRadius;
        public Color CrystalMarkColor => crystalMarkColor;
        public float MarkIntensity => markIntensity;
        public float MarkMinScreenRadius => markMinScreenRadius;
        public PilotVision UnassignedSight => unassignedSight;
        public int Count => callsigns?.Length ?? 0;

        /// <summary>The row for <paramref name="sight"/>, or null.</summary>
        public Callsign For(PilotVision sight)
        {
            if (callsigns == null) return null;
            for (int i = 0; i < callsigns.Length; i++)
                if (callsigns[i] != null && callsigns[i].sight == sight) return callsigns[i];
            return null;
        }
    }
}
