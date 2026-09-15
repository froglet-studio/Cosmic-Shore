using System;
using System.Globalization;
using System.Text;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A character is this small serializable value and nothing else: two clades, three weights,
    /// the human base proportions it rolled, a few global proportions, palette keys, and the
    /// trait expression the resolver derived from all of the above (stored so a label can show
    /// it and a hand-edit can be seen against it — the resolver re-derives it, it does not read
    /// it). Mesh and textures are derived from the genome deterministically:
    /// <see cref="CharacterResolver"/> → <see cref="CharacterAssembler"/> →
    /// <see cref="CharacterTexturePainter"/>. Hand a saved genome to someone else and they get
    /// the identical face.
    /// </summary>
    [Serializable]
    public struct CharacterGenome
    {
        public const int Version = 1;

        public int FormatVersion;
        public int Seed;

        [Tooltip("Clade keys (CladeSO.Key). Both empty = the pure-human control.")]
        public string CladeA;
        public string CladeB;
        public CharacterWeights Weights;

        [Tooltip("The human base proportions this individual rolled, in [-1, 1] per axis.")]
        public HeadShape BaseShape;

        // ---- global proportions (what a species does not decide) -------------------------
        [Range(0f, 1f)] public float Age;          // 0 young … 1 old: skin detail + lip/brow softening
        [Range(0f, 1f)] public float Fleshiness;   // 0 gaunt … 1 full: cheek/jaw softness, neck
        [Range(0f, 1f)] public float HairVolume;   // human crown only

        // ---- palette keys ---------------------------------------------------------------
        [Range(0f, 1f)] public float SkinTone;     // 0 pale … 1 deep (melanin)
        [Range(0f, 1f)] public float SkinWarmth;   // 0 cool/olive … 1 warm/ruddy
        [Range(0f, 1f)] public float HairShade;    // 0 black … 1 pale
        [Range(0f, 1f)] public float HairWarmth;   // 0 ash … 1 red/copper
        [Range(0f, 1f)] public float IrisKey;      // position along the covering's iris range
        [Range(0f, 1f)] public float MarkingKey;   // which marking variant / strength inside the clade's range
        public Domains Domain;
        [Tooltip("Pilot gear worn (goggles up / on). Placed by the resolver, never by a clade.")]
        public GearKind Gear;
        [Tooltip("Multiplies the clade coverings' base colour (fur, feather, scale, chitin). Alpha 0 = unset = as authored.")]
        public Color CoatTint;

        [Tooltip("Resolved expression, written by the resolver for labelling. Not an input.")]
        public string[] ExpressedTraits;

        /// <summary>The coat tint as a multiplier: white when unset.</summary>
        public Color CoatMultiplier => CoatTint.a > 0.001f ? new Color(CoatTint.r, CoatTint.g, CoatTint.b, 1f) : Color.white;

        public bool IsPureHuman => string.IsNullOrEmpty(CladeA) && string.IsNullOrEmpty(CladeB);

        public static CharacterGenome Empty => new CharacterGenome
        {
            FormatVersion = Version,
            CladeA = string.Empty,
            CladeB = string.Empty,
            Weights = CharacterWeights.EqualThirds,
            BaseShape = HeadShape.Neutral,
            ExpressedTraits = Array.Empty<string>(),
            Domain = Domains.Blue,
            CoatTint = new Color(0f, 0f, 0f, 0f),
        };

        /// <summary>Short label for a contact-sheet cell: "Felidae 0.33 / Corvidae 0.27 · H 0.40".</summary>
        public string Label()
        {
            if (IsPureHuman) return $"HUMAN · seed {Seed}";
            return $"{CladeA} {Weights.CladeA:0.00} / {CladeB} {Weights.CladeB:0.00} · H {Weights.Human:0.00}";
        }

        public string ToJson(bool pretty = true) => JsonUtility.ToJson(this, pretty);

        public static bool TryFromJson(string json, out CharacterGenome genome)
        {
            genome = Empty;
            if (string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                genome = JsonUtility.FromJson<CharacterGenome>(json);
            }
            catch (Exception)
            {
                return false;
            }
            if (genome.CladeA == null) genome.CladeA = string.Empty;
            if (genome.CladeB == null) genome.CladeB = string.Empty;
            if (!genome.BaseShape.IsInitialized) genome.BaseShape = HeadShape.Neutral;
            if (genome.ExpressedTraits == null) genome.ExpressedTraits = Array.Empty<string>();
            return true;
        }

        /// <summary>
        /// A stable content hash over every INPUT field (the expression list is excluded — it is
        /// derived). Used as the portrait-cache key, so two genomes that would render the same
        /// face share one file.
        /// </summary>
        public string ContentHash()
        {
            var sb = new StringBuilder(256);
            sb.Append(FormatVersion).Append('|').Append(Seed).Append('|')
              .Append(CladeA).Append('|').Append(CladeB).Append('|')
              .Append(Weights.Human.ToString("R", CultureInfo.InvariantCulture)).Append(',').Append(Weights.CladeA.ToString("R", CultureInfo.InvariantCulture)).Append(',').Append(Weights.CladeB.ToString("R", CultureInfo.InvariantCulture)).Append('|');
            for (int i = 0; i < HeadShape.AxisCount; i++) sb.Append(BaseShape[(HeadAxis)i].ToString("R", CultureInfo.InvariantCulture)).Append(',');
            sb.Append('|').Append(Age.ToString("R", CultureInfo.InvariantCulture)).Append(',').Append(Fleshiness.ToString("R", CultureInfo.InvariantCulture)).Append(',').Append(HairVolume.ToString("R", CultureInfo.InvariantCulture))
              .Append('|').Append(SkinTone.ToString("R", CultureInfo.InvariantCulture)).Append(',').Append(SkinWarmth.ToString("R", CultureInfo.InvariantCulture)).Append(',')
              .Append(HairShade.ToString("R", CultureInfo.InvariantCulture)).Append(',').Append(HairWarmth.ToString("R", CultureInfo.InvariantCulture)).Append(',')
              .Append(IrisKey.ToString("R", CultureInfo.InvariantCulture)).Append(',').Append(MarkingKey.ToString("R", CultureInfo.InvariantCulture)).Append('|').Append((int)Domain)
              .Append('|').Append((int)Gear).Append('|')
              .Append(CoatTint.r.ToString("R", CultureInfo.InvariantCulture)).Append(',').Append(CoatTint.g.ToString("R", CultureInfo.InvariantCulture)).Append(',')
              .Append(CoatTint.b.ToString("R", CultureInfo.InvariantCulture)).Append(',').Append(CoatTint.a.ToString("R", CultureInfo.InvariantCulture));
            return Fnv1a(sb.ToString());
        }

        static string Fnv1a(string s)
        {
            ulong h = 14695981039346656037UL;
            for (int i = 0; i < s.Length; i++)
            {
                h ^= s[i];
                h *= 1099511628211UL;
            }
            return h.ToString("x16");
        }
    }
}
