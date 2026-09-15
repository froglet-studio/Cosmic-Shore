using System;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    // Every feature's tunables, one class per generator, all in HEAD UNITS (head height ≈ 1)
    // so a clade author never thinks about a site's radius. Classes rather than structs so a
    // clade asset that omits a group deserialises to these defaults instead of to zeros.

    [Serializable] public class EyeParams
    {
        [Tooltip("Eyeball radius as a fraction of head height; the OrbitalSize axis scales it further.")]
        public float Radius = 0.062f;
        [Range(0.2f, 1f)] public float IrisFraction = 0.55f;
        [Range(0.2f, 1f)] public float LidOpen = 0.62f;
        [Tooltip("Outer-corner lift, degrees. Positive = upturned.")] public float CanthalTiltDeg = 3f;
        public float LidThickness = 0.011f;
        public PupilKind Pupil = PupilKind.Round;
        [Range(0.05f, 0.9f)] public float PupilSize = 0.32f;
        public Color IrisA = new Color(0.30f, 0.20f, 0.10f);
        public Color IrisB = new Color(0.35f, 0.50f, 0.55f);
        public Color Sclera = new Color(0.93f, 0.90f, 0.87f);
        [Tooltip("Compound eyes: dome radius as a fraction of head height.")] public float DomeRadius = 0.11f;
        [Tooltip("Compound eyes: how far the dome sits proud of the socket, 0..1 of its radius.")] public float DomeProud = 0.55f;
        [Tooltip("Pale rim around a compound eye's facet field.")] public Color CompoundRim = new Color(0.55f, 0.45f, 0.25f);
        public Color CompoundFacet = new Color(0.08f, 0.06f, 0.05f);
    }

    [Serializable] public class PinnaParams
    {
        public float Height = 0.17f;
        public float Width = 0.10f;
        [Range(0f, 1f)] public float Pointed = 0f;
        [Range(0f, 1f)] public float CupDepth = 0.45f;
        [Tooltip("Rim (helix) thickness as a fraction of width.")] public float RimThickness = 0.16f;
        [Range(0f, 1f)] public float Tragus = 0f;
        [Tooltip("Pitch of the pinna away from the head normal, degrees. Positive tips it forward.")] public float ForwardTiltDeg = 22f;
        [Tooltip("Roll of the pinna about the head normal, degrees. Positive tips the top outward.")] public float OutwardRollDeg = 10f;
        [Tooltip("Thickness of the ear shell.")] public float Thickness = 0.012f;
    }

    [Serializable] public class BeakParams
    {
        public float Length = 0.34f;
        [Range(0f, 1f)] public float Hook = 0.15f;
        [Tooltip("Base height as a fraction of the site radius.")] public float BaseHeight = 1.0f;
        [Tooltip("Base width as a fraction of the site radius.")] public float BaseWidth = 0.9f;
        [Range(0f, 1f)] public float TipSharpness = 0.6f;
        [Tooltip("Where the gape (jaw line) sits on the base, 0 bottom … 1 top.")] public float GapeHeight = 0.42f;
        [Tooltip("Ridge along the top (culmen), fraction of base height.")] public float Culmen = 0.15f;
        public Color KeratinA = new Color(0.05f, 0.05f, 0.06f);
        public Color KeratinB = new Color(0.12f, 0.11f, 0.10f);
    }

    [Serializable] public class MandibleParams
    {
        public float Length = 0.22f;
        public float RootRadius = 0.035f;
        [Range(0f, 1f)] public float Curl = 0.6f;
        [Tooltip("Sideways spread of the roots, fraction of head height.")] public float Spread = 0.10f;
        public Color KeratinA = new Color(0.06f, 0.04f, 0.03f);
        public Color KeratinB = new Color(0.20f, 0.12f, 0.06f);
    }

    [Serializable] public class NoseLeafParams
    {
        public float Height = 0.11f;
        public float Width = 0.07f;
        [Range(0f, 1f)] public float Spear = 0.7f;
        public float Thickness = 0.008f;
    }

    [Serializable] public class AntennaParams
    {
        public float Length = 0.28f;
        public float Radius = 0.012f;
        [Tooltip("Number of club plates (lamellae).")] public int Plates = 3;
        public float PlateLength = 0.07f;
        [Tooltip("Pitch of the stalk above the brow, degrees.")] public float ElevationDeg = 35f;
        [Tooltip("Yaw of the stalk away from the centre line, degrees.")] public float SplayDeg = 25f;
        public Color KeratinA = new Color(0.05f, 0.04f, 0.03f);
        public Color KeratinB = new Color(0.16f, 0.11f, 0.05f);
    }

    [Serializable] public class CrestParams
    {
        public int Feathers = 9;
        public float Length = 0.22f;
        public float Width = 0.04f;
        [Tooltip("Lean back from the crown normal, degrees.")] public float LeanDeg = 40f;
        [Tooltip("Fan angle each side of the centre line, degrees.")] public float FanDeg = 28f;
    }

    [Serializable] public class BlowholeParams
    {
        public float Radius = 0.045f;
        public float RimHeight = 0.012f;
    }

    [Serializable] public class WhiskerParams
    {
        public int PerSide = 4;
        public float Length = 0.22f;
        public float Radius = 0.0035f;
        [Tooltip("Vertical fan, degrees.")] public float FanDeg = 24f;
    }

    [Serializable] public class HairParams
    {
        [Tooltip("Hair shell thickness at full HairVolume.")] public float Thickness = 0.055f;
        [Tooltip("Polar angle of the hairline at the forehead, degrees.")] public float FrontHairlineDeg = 42f;
        [Tooltip("Polar angle of the hairline at the sides, degrees.")] public float SideHairlineDeg = 88f;
        [Tooltip("Polar angle of the hairline at the nape, degrees.")] public float BackHairlineDeg = 124f;
        [Range(0f, 1f)] public float Clumping = 0.5f;
        [Tooltip("Number of strand cards at full HairVolume. 0 = cap only.")] public int StrandCount = 700;
        [Tooltip("Strand length at full HairVolume, head units.")] public float StrandLength = 0.30f;
        [Tooltip("Strand card width, head units.")] public float StrandWidth = 0.036f;
        [Tooltip("Azimuth of the parting, degrees from the face toward the character's left. 0 = centre part.")] public float PartDeg = 26f;
        [Tooltip("How far the front strands fall over the forehead, 0..1.")] [Range(0f, 1f)] public float Fringe = 0.3f;
        [Tooltip("Eyebrow cards along the brow ridge. 0 = none.")] public int BrowCards = 60;
    }

    [Serializable] public class FangParams
    {
        public float Length = 0.05f;
        public float Radius = 0.012f;
        public Color Keratin = new Color(0.92f, 0.88f, 0.80f);
    }

    /// <summary>All groups on one trait; only the group its <see cref="FeatureKind"/> reads matters.</summary>
    [Serializable] public class FeatureParams
    {
        public EyeParams Eye = new EyeParams();
        public PinnaParams Pinna = new PinnaParams();
        public BeakParams Beak = new BeakParams();
        public MandibleParams Mandible = new MandibleParams();
        public NoseLeafParams NoseLeaf = new NoseLeafParams();
        public AntennaParams Antenna = new AntennaParams();
        public CrestParams Crest = new CrestParams();
        public BlowholeParams Blowhole = new BlowholeParams();
        public WhiskerParams Whisker = new WhiskerParams();
        public HairParams Hair = new HairParams();
        public FangParams Fang = new FangParams();
    }
}
