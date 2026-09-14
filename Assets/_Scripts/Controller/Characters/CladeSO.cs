using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    [Serializable]
    public struct AxisTarget
    {
        public HeadAxis Axis;
        [Range(-1f, 1f)] public float Value;
    }

    [Serializable]
    public struct AxisOverride
    {
        public HeadAxis Axis;
        [Range(-1f, 1f)] public float Value;
    }

    /// <summary>
    /// One rung of a clade's trait ladder. Order in the list IS the ladder: index 0 is expressed
    /// at the minimum weight, the last rung only near the maximum, unless a rung is flagged
    /// <see cref="Signature"/> (always expressed) or carries an explicit
    /// <see cref="MinWeight"/>.
    /// </summary>
    [Serializable]
    public class TraitDefinition
    {
        public string Id;
        [Tooltip("Present at ANY contribution level, and beats a non-signature trait for a slot.")]
        public bool Signature;
        [Tooltip("0 = derived from the rung's position on the ladder.")]
        public float MinWeight;
        public FeatureKind Feature;
        [Tooltip("Slots this trait CLAIMS. A claim with Feature = None removes whatever else would fill the slot.")]
        public List<FeatureSlot> Slots = new List<FeatureSlot>();
        [Tooltip("Head site the feature attaches at (HeadSiteSpec.Name). Empty = the slot's default site.")]
        public string Site;
        [Tooltip("Pitch of the feature frame about its Right axis, degrees (positive tips the top forward).")]
        public float SitePitchDeg;
        [Tooltip("Yaw about the frame's Up axis, degrees (mirrored on the right side).")]
        public float SiteYawDeg;
        [Tooltip("Roll about the site normal, degrees (mirrored on the right side).")]
        public float SiteRollDeg;
        [Tooltip("Axis values this trait FORCES when expressed (a beak flattens the lips and nose).")]
        public AxisOverride[] AxisOverrides = Array.Empty<AxisOverride>();
        public FeatureParams Params = new FeatureParams();
    }

    /// <summary>
    /// A source's surface: what its skin/fur/feathers/scales/chitin look like and where on the
    /// head that covering lives. Coverings are REGIONAL, not averaged — a clade's covering
    /// starts at its anchor and reaches toward the face as its weight rises — because 40% of a
    /// feather is not a feather.
    /// </summary>
    [Serializable]
    public class CoveringRecipe
    {
        public CoveringKind Kind = CoveringKind.Skin;
        [Tooltip("Base colour range; the genome's MarkingKey picks along it.")]
        public Color BaseA = new Color(0.75f, 0.58f, 0.48f);
        public Color BaseB = new Color(0.45f, 0.30f, 0.22f);
        public MarkingKind Marking = MarkingKind.None;
        public Color MarkingColor = Color.black;
        [Range(0f, 1f)] public float MarkingStrength = 0.5f;
        public float MarkingScale = 6f;
        [Range(0f, 1f)] public float Smoothness = 0.35f;
        [Range(0f, 1f)] public float DetailStrength = 0.5f;
        [Tooltip("Where the covering begins, as a polar angle from the crown, degrees. 0 = the crown; a skin-only clade uses 180 (everywhere).")]
        public float AnchorThetaDeg = 0f;
        [Tooltip("How far (degrees of polar angle) the covering reaches from its anchor toward the face at the MINIMUM weight.")]
        public float ReachDegAtMin = 40f;
        [Tooltip("... and at the MAXIMUM weight.")] public float ReachDegAtMax = 95f;
        [Tooltip("Whether this covering carries eyebrows / eyelash lines when it reaches the eyes.")]
        public bool PaintsBrows = true;
    }

    /// <summary>
    /// A clade IS data. Six ship (Felidae, Corvidae, Cetacea, Chiroptera, Coleoptera, Testudines)
    /// plus the Human source the config points at. Adding a seventh is a new asset in
    /// <c>Resources/Characters/Clades</c> — never a code change.
    /// </summary>
    [CreateAssetMenu(fileName = "Clade", menuName = "ScriptableObjects/Characters/Clade")]
    public class CladeSO : ScriptableObject
    {
        [Tooltip("Stable key a genome refers to this clade by. Never rename once genomes exist.")]
        public string Key;
        public string DisplayName;
        [Tooltip("The human source — the base every chimera blends from. Exactly one asset sets this.")]
        public bool IsHuman;

        [Header("Continuous")]
        [Tooltip("Where this clade pulls each head axis at FULL contribution. Unlisted axes pull toward neutral.")]
        public AxisTarget[] AxisTargets = Array.Empty<AxisTarget>();

        [Header("Categorical — the ladder, signature first")]
        public TraitDefinition[] Traits = Array.Empty<TraitDefinition>();

        [Header("Surface")]
        public CoveringRecipe Covering = new CoveringRecipe();

        public float AxisTargetFor(HeadAxis axis)
        {
            if (AxisTargets == null) return 0f;
            for (int i = 0; i < AxisTargets.Length; i++)
                if (AxisTargets[i].Axis == axis) return AxisTargets[i].Value;
            return 0f;
        }

        public TraitDefinition FindTrait(string id)
        {
            if (Traits == null) return null;
            for (int i = 0; i < Traits.Length; i++)
                if (Traits[i] != null && Traits[i].Id == id) return Traits[i];
            return null;
        }

        /// <summary>Every trait flagged as a signature (or the first rung when none is flagged).</summary>
        public IEnumerable<TraitDefinition> Signatures()
        {
            bool any = false;
            if (Traits != null)
                for (int i = 0; i < Traits.Length; i++)
                    if (Traits[i] != null && Traits[i].Signature) { any = true; yield return Traits[i]; }
            if (!any && Traits != null && Traits.Length > 0 && Traits[0] != null) yield return Traits[0];
        }
    }
}
