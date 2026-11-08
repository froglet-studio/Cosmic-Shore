namespace CosmicShore.Data
{
    /// <summary>
    /// A unique artifact in 64 bits, for the vessel's networked loadout (<c>R_VesselActionHandler.NetArtifacts</c>;
    /// Docs/Pilots/INVENTORY.md §5). Every peer must fire the SAME payload, so the code carries the artifact's levels
    /// themselves, not an id the peer would need a record for:
    /// <code>
    /// bits  0-7   kind: the catalog index + 1 (0 = no artifact)
    /// bits  8-10  the attuned element
    /// bits 11-20  the attuned level x 100 (0.00-10.23)
    /// bits 21-36  the four fixed levels, 4 bits each, in steps of 0.2 (0.0-3.0), Mass, Space, Time, Charge
    /// bits 37-63  the low 27 bits of the seed (for the look: hue, facets, glyph)
    /// </code>
    /// <para>The fixed tracks are rolled in 0-3 (<see cref="ArtifactGenerator.FixedMax"/>), so 4 bits at 0.2 hold them
    /// to within 0.1; the attuned level to within 0.005. Both are far below what a transform's numbers can show.</para>
    /// </summary>
    public static class ArtifactWire
    {
        const int KindBits = 8, ElementBits = 3, LevelBits = 10, FixedBits = 4;
        const int ElementShift = KindBits;
        const int LevelShift = ElementShift + ElementBits;
        const int FixedShift = LevelShift + LevelBits;
        const int SeedShift = FixedShift + 4 * FixedBits;
        const ulong KindMask = (1UL << KindBits) - 1, ElementMask = (1UL << ElementBits) - 1,
            LevelMask = (1UL << LevelBits) - 1, FixedMask = (1UL << FixedBits) - 1;

        /// <summary>The most kinds a code can name (index 0..254).</summary>
        public const int MaxKinds = (int)KindMask;

        /// <summary>The code for <paramref name="artifact"/> at catalog index <paramref name="kindIndex"/>; 0 (no
        /// artifact) for a null record or an index out of range.</summary>
        public static ulong Pack(int kindIndex, ArtifactRecord artifact)
        {
            if (artifact == null || kindIndex < 0 || kindIndex >= MaxKinds) return 0;
            UniqueId.TrySplit(artifact.Id, out _, out ulong seed);
            return Pack(kindIndex, artifact.Element, artifact.Levels(), seed);
        }

        public static ulong Pack(int kindIndex, Element attuned, ArtifactLevels levels, ulong seed)
        {
            if (kindIndex < 0 || kindIndex >= MaxKinds) return 0;
            ulong code = (ulong)(kindIndex + 1) & KindMask;
            code |= ((ulong)attuned & ElementMask) << ElementShift;
            ulong level = (ulong)System.Math.Min(LevelMask, System.Math.Round(levels.Of(attuned) * 100.0));
            code |= level << LevelShift;
            for (int i = 0; i < 4; i++)
            {
                var e = ArtifactLevels.TrackElements[i];
                ulong q = (ulong)System.Math.Min(FixedMask, System.Math.Round(levels.Of(e) * 5.0));
                if (e == attuned) q = 0;
                code |= q << (FixedShift + i * FixedBits);
            }
            code |= (seed & ((1UL << (64 - SeedShift)) - 1)) << SeedShift;
            return code;
        }

        /// <summary>Reads a code back. False for 0 (no artifact).</summary>
        public static bool TryUnpack(ulong code, out int kindIndex, out Element attuned, out ArtifactLevels levels, out uint seedLow)
        {
            kindIndex = (int)(code & KindMask) - 1;
            attuned = (Element)((code >> ElementShift) & ElementMask);
            seedLow = (uint)(code >> SeedShift);
            float m = ((code >> FixedShift) & FixedMask) / 5f;
            float s = ((code >> (FixedShift + FixedBits)) & FixedMask) / 5f;
            float t = ((code >> (FixedShift + 2 * FixedBits)) & FixedMask) / 5f;
            float c = ((code >> (FixedShift + 3 * FixedBits)) & FixedMask) / 5f;
            levels = new ArtifactLevels(m, s, t, c).With(attuned, ((code >> LevelShift) & LevelMask) / 100f);
            return kindIndex >= 0;
        }
    }
}
