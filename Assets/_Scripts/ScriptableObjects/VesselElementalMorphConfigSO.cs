using System;
using CosmicShore.Data;
using DG.Tweening;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Shared feel spec for the elemental hull morphs: vessel models carry blend shapes on their
    /// skinned meshes labeled by element name (charge / mass / space / time — authored into the FBX),
    /// and <see cref="CosmicShore.Gameplay.VesselAnimation"/> discovers them by name and glides each
    /// one between its authored extremes as the vessel's effective element level moves through the
    /// [0, 10] progression band. One asset is the single source of truth for the morph feel so the
    /// fleet stays consistent instead of drifting between per-prefab copies.
    ///
    /// The element level is an integer in [-5, 15] (ResourceSystem.GetLevel); the model expresses
    /// only [0, 10] — the deficit band [-5, 0) holds the level-0 silhouette and the overcharge band
    /// (10, 15] holds the level-10 extreme.
    /// </summary>
    [CreateAssetMenu(
        fileName = "VesselElementalMorphConfig",
        menuName = "ScriptableObjects/Vessel/Elemental Morph Config")]
    public class VesselElementalMorphConfigSO : ScriptableObject
    {
        [Header("Morph feel (fleet-wide)")]
        [Tooltip("Seconds the hull takes to glide between element silhouettes on a level change. " +
                 "Keep it visible - continuity of existence applies to the vessel's own body: the " +
                 "model MORPHS between states, it never snaps.")]
        [Min(0f)] public float morphDuration = 0.75f;

        [Tooltip("Ease applied to the blend-shape glide.")]
        public Ease morphEase = Ease.InOutSine;

        /// <summary>Integer element level at (and below) which a shape rests at weight zero.</summary>
        public const int MorphFloorLevel = 0;
        /// <summary>Integer element level at (and above) which a shape holds its authored extreme.</summary>
        public const int MorphCeilingLevel = 10;

        /// <summary>The four elements a model can label shapes for (None/Omni are not morph targets).</summary>
        public static readonly Element[] MorphElements =
            { Element.Charge, Element.Mass, Element.Space, Element.Time };

        /// <summary>
        /// Maps an integer element level in the full effective band [-5, 15] to a normalized morph
        /// weight in [0, 1], clamping to the [0, 10] progression band. Pure for tests.
        /// </summary>
        public static float NormalizedMorphWeight(int level) =>
            Mathf.Clamp(level, MorphFloorLevel, MorphCeilingLevel) / (float)MorphCeilingLevel;

        static readonly char[] TokenDelimiters = { '_', '.', '-', ' ' };

        /// <summary>
        /// Resolves which element a blend shape is labeled for, or false when it isn't an element
        /// shape (art freely authors non-element shapes alongside — tendrils, jaws — and those stay
        /// untouched). Matching is by NAME, case-insensitive, because that is the labeling contract
        /// with art: "charge", "Mass", "blendShape1.space" all resolve; FBX deformer prefixes are
        /// judged by their tail, and compound names by whole tokens ("mass_hull" resolves,
        /// "massive_jaw" does not). A name whose tokens hit several elements is ambiguous → no match.
        /// </summary>
        public static bool TryResolveElement(string blendShapeName, out Element element)
        {
            element = Element.None;
            if (string.IsNullOrEmpty(blendShapeName)) return false;

            // FBX channels often arrive prefixed with the deformer name ("blendShape1.charge").
            int dot = blendShapeName.LastIndexOf('.');
            string tail = dot >= 0 ? blendShapeName.Substring(dot + 1) : blendShapeName;

            for (int i = 0; i < MorphElements.Length; i++)
            {
                if (string.Equals(tail, MorphElements[i].ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    element = MorphElements[i];
                    return true;
                }
            }

            // Token-bound fallback: an element name must stand alone between delimiters, so
            // "mass_hull" is labeled but "massive_jaw" / "timeline_flap" are just art.
            int matches = 0;
            var tokens = blendShapeName.Split(TokenDelimiters, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < MorphElements.Length; i++)
            {
                for (int t = 0; t < tokens.Length; t++)
                {
                    if (string.Equals(tokens[t], MorphElements[i].ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        element = MorphElements[i];
                        matches++;
                        break;
                    }
                }
            }
            if (matches == 1) return true;

            element = Element.None;
            return false;
        }

        /// <summary>Resources path of the shared asset (zero-wire default).</summary>
        public const string ResourcePath = "VesselElementalMorphConfig";

        static VesselElementalMorphConfigSO _default;

        /// <summary>
        /// Loads the shared config from Resources. Falls back to code defaults so the fleet still
        /// morphs if the asset goes missing — warned so the authorable asset gets restored.
        /// </summary>
        public static VesselElementalMorphConfigSO LoadDefault()
        {
            if (_default) return _default;

            _default = Resources.Load<VesselElementalMorphConfigSO>(ResourcePath);
            if (!_default)
            {
                Debug.LogError($"[VesselElementalMorphConfigSO] No config asset at Resources/{ResourcePath} - " +
                               "morphing with code defaults. Restore the asset so the feel stays authorable.");
                _default = CreateInstance<VesselElementalMorphConfigSO>();
            }
            return _default;
        }
    }
}
