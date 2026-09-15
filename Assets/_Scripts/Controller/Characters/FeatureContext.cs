using System.Collections.Generic;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Everything a feature generator is allowed to know: its site, the surface it grows from,
    /// its authored parameters, the head shape (for the few features that scale with an axis),
    /// a private random stream, and the genome's global proportions it may read. It emits into
    /// <see cref="Output"/> in SITE-LOCAL space — head units for embedded parts, ring units for
    /// seam parts (see <see cref="AttachmentContract"/>).
    /// </summary>
    public sealed class FeatureContext
    {
        public AttachmentSite Site;
        public IHeadSurface Surface;
        public FeatureParams Params;
        public HeadShape Shape;
        public CharacterRandom Rng;
        public float HairVolume;
        public float Age;
        public GearKind Gear;
        public string TraitId;
        public readonly List<MeshPart> Output = new();

        public MeshPart Begin(string name, CharacterMaterialSlot slot, bool projectUvs = false)
        {
            var part = new MeshPart($"{TraitId}.{name}", slot) { ProjectUvsOntoHead = projectUvs };
            Output.Add(part);
            return part;
        }
    }

    /// <summary>
    /// The one dispatch from <see cref="FeatureKind"/> to its generator. A new feature kind is a
    /// new file plus one case here; a new CLADE using an existing kind is no code at all.
    /// </summary>
    public static class FeatureCatalog
    {
        public static void Generate(FeatureKind kind, FeatureContext ctx)
        {
            switch (kind)
            {
                case FeatureKind.None: return;
                case FeatureKind.VertebrateEye: VertebrateEyeFeature.Generate(ctx); return;
                case FeatureKind.CompoundEye: CompoundEyeFeature.Generate(ctx); return;
                case FeatureKind.Pinna: PinnaFeature.Generate(ctx); return;
                case FeatureKind.Beak: BeakFeature.Generate(ctx); return;
                case FeatureKind.Mandibles: MandibleFeature.Generate(ctx); return;
                case FeatureKind.NoseLeaf: NoseLeafFeature.Generate(ctx); return;
                case FeatureKind.Antennae: AntennaeFeature.Generate(ctx); return;
                case FeatureKind.Crest: CrestFeature.Generate(ctx); return;
                case FeatureKind.Blowhole: BlowholeFeature.Generate(ctx); return;
                case FeatureKind.Whiskers: WhiskerFeature.Generate(ctx); return;
                case FeatureKind.HairCap: HairCapFeature.Generate(ctx); return;
                case FeatureKind.Fangs: FangFeature.Generate(ctx); return;
                case FeatureKind.Trunk: TrunkFeature.Generate(ctx); return;
                case FeatureKind.Goggles: GogglesFeature.Generate(ctx); return;
                default:
                    throw new System.InvalidOperationException($"FeatureCatalog: no generator for {kind}.");
            }
        }

        /// <summary>The site a kind attaches at when the trait names none.</summary>
        public static string DefaultSite(FeatureKind kind) => kind switch
        {
            FeatureKind.VertebrateEye => "Eye",
            FeatureKind.CompoundEye => "Eye",
            FeatureKind.Pinna => "EarSide",
            FeatureKind.Beak => "Muzzle",
            FeatureKind.Mandibles => "Mouth",
            FeatureKind.NoseLeaf => "NoseTip",
            FeatureKind.Antennae => "Brow",
            FeatureKind.Crest => "CrownFront",
            FeatureKind.Blowhole => "CrownBack",
            FeatureKind.Whiskers => "Cheek",
            FeatureKind.HairCap => "Crown",
            FeatureKind.Fangs => "MouthCorner",
            FeatureKind.Trunk => "NoseTip",
            FeatureKind.Goggles => "CrownFront",
            _ => string.Empty,
        };

        /// <summary>Whether a kind is a seam feature (base ring on the contract) or embedded.</summary>
        public static bool IsSeam(FeatureKind kind) => kind == FeatureKind.Beak;
    }
}
