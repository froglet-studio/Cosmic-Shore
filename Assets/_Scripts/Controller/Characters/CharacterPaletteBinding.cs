using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// How the project's DOMAIN palette (Jade / Ruby / Gold — `Docs/PALETTE.md`, `SO_ColorSet`)
    /// reaches a person. This is deliberately ONE file with ONE switch, so the decision is visible
    /// and reversible:
    ///
    /// <list type="bullet">
    /// <item><see cref="DomainAccentMode.Accent"/> (DEFAULT): skin, scale, feather and chitin keep
    /// ranges of their own; the domain enters as an ACCENT — the iris tint, the markings tint and a
    /// small painted adornment at the temple — read through <c>SO_ColorSet.GetDomainSignalColor</c>,
    /// the same accessor every other domain-tinted UI uses. A person should not have teal skin.</item>
    /// <item><see cref="DomainAccentMode.DomainSkin"/>: the alternative, kept so it can be seen and
    /// chosen — the domain signal colour is also blended into the skin/covering base at
    /// <see cref="DomainSkinTint"/>. Set <see cref="Mode"/> to it and every portrait re-bakes that way.</item>
    /// </list>
    /// </summary>
    public static class CharacterPaletteBinding
    {
        public enum DomainAccentMode { Accent = 0, DomainSkin = 1 }

        /// <summary>The one decision.</summary>
        public const DomainAccentMode Mode = DomainAccentMode.Accent;

        public const float IrisTint = 0.45f;
        public const float MarkingTint = 0.22f;
        public const bool PaintAdornment = true;
        public const float DomainSkinTint = 0.30f;

        public struct DomainAccent
        {
            public Color Accent;
            public float IrisTint, MarkingTint, SkinTint;
            public bool Adornment;
        }

        /// <summary>
        /// Resolve the accent for a domain. <paramref name="colorSet"/> may be null (an offline
        /// harness, a test): then the shipped signal colours from PALETTE.md §2.4 are used, so the
        /// numbers can never silently diverge from what the live palette produces.
        /// </summary>
        public static DomainAccent Resolve(Domains domain, SO_ColorSet colorSet)
        {
            Color accent = colorSet != null ? colorSet.GetDomainSignalColor(domain) : FallbackSignal(domain);
            return new DomainAccent
            {
                Accent = accent,
                IrisTint = IrisTint,
                MarkingTint = MarkingTint,
                Adornment = PaintAdornment,
                SkinTint = Mode == DomainAccentMode.DomainSkin ? DomainSkinTint : 0f,
            };
        }

        /// <summary>PALETTE.md §2.4's measured `GetDomainSignalColor` table (Blue = the neutral sentinel: white).</summary>
        public static Color FallbackSignal(Domains domain) => domain switch
        {
            Domains.Jade => new Color(0.073f, 1.0f, 0.948f),
            Domains.Ruby => new Color(1.0f, 0.0f, 0.976f),
            Domains.Gold => new Color(1.0f, 0.657f, 0.0f),
            _ => Color.white,
        };
    }
}
