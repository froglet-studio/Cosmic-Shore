using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>Everything the texture layers read. Built once per character by the painter.</summary>
    public sealed class PaintContext
    {
        public CharacterBlueprint Blueprint;
        public FaceLandmarks Landmarks;
        public CharacterGenerationConfigSO Config;
        public CharacterPaletteBinding.DomainAccent Accent;
        public int Seed;
        public Color HumanSkin, HairColor, BrowColor;
        public float BeardShadow;   // 0..1, rolled per individual by the painter
        public Color CoatTint = Color.white;   // the genome's covering colour when CoatTinted
        public bool CoatTinted;

        /// <summary>Per-pixel geometry the layers share.</summary>
        public struct Pixel
        {
            public float U, V, ThetaDeg, Phi;   // Phi in radians, ThetaDeg in degrees from the crown
        }

        /// <summary>Normalised distance (1 = the site's ring) from a landmark in texture space.</summary>
        public static float LandmarkDistance(in Landmark l, float u, float v, float scaleU = 1f, float scaleV = 1f)
        {
            float du = u - l.Uv.x;
            if (du > 0.5f) du -= 1f; else if (du < -0.5f) du += 1f;
            float dv = v - l.Uv.y;
            float ru = Mathf.Max(1e-4f, l.UvRadius.x * scaleU), rv = Mathf.Max(1e-4f, l.UvRadius.y * scaleV);
            return GeometryKit.SafeSqrt((du / ru) * (du / ru) + (dv / rv) * (dv / rv));
        }

        public static float WrapU(float du) => du > 0.5f ? du - 1f : (du < -0.5f ? du + 1f : du);
    }
}
