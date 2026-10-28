#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The asset-side gate for Docs/ECOSYSTEM.md §40 — lifeform LEVELS are retired, and a
    /// lifeform is its species × element, authored once in that element's own variant block.
    ///
    /// <para>Its HEART is no longer one of the things a lifeform authors: every elemental crystal,
    /// a heart included, is one size (<c>ElementalCrystalSetSO.CrystalWorldScale</c>) and one petal
    /// (2026-10-26; <c>ElementalCrystalSizeTests</c> holds both). The per-variant
    /// <c>HeartWorldScale</c> is still serialized and nothing reads it, so the band tests that
    /// lived here (authored, enabled, under the reward cap) were retired with the band.</para>
    ///
    /// <list type="number">
    ///   <item><b>The species × element matrix is whole.</b> A lifeform is its species and its
    ///   element - all four, or the roster has a hole.</item>
    ///
    ///   <item><b>A retired level field coming back as data.</b> The level surface is gone
    ///   from C#, but Unity never prunes an unresolvable serialized field: an
    ///   <c>InitialLevel</c> left in an asset is dead weight that reads as live authoring
    ///   the next time somebody opens the file.</item>
    /// </list>
    /// </summary>
    public class LifeformHeartSizeTests
    {
        const string LifeformDir = "Assets/_SO_Assets/Lifeforms";
        const string AssetsRoot = "Assets";

        /// 28 species × 4 elements. Pinned so a rename, a move, or a parse regression cannot
        /// turn this whole suite into a green no-op over an empty set.
        ///
        /// <para>The set was 22 species (88) when this pin was written; the species since added
        /// each landed with their own generator.</para>
        const int CanonicalVariantCount = 112;

        /// The four droppable elements, as serialized (Element.None is 0, Omni 5).
        static readonly int[] DroppableElements = { 1, 2, 3, 4 };

        /// Serialized keys that the level retirement removed. A YAML key at any indentation.
        static readonly string[] RetiredLevelFields =
        {
            "InitialLevel",
            "LeafScalePerLevel",
            "BodyScalePerLevel",
            "LevelGrowSeconds",
            "FeedsPerLevel",
        };

        // ── asset reading ────────────────────────────────────────────────────

        static bool TryInt(string body, string key, out int value)
        {
            var m = Regex.Match(body, $@"^\s+{Regex.Escape(key)}: (?<v>-?\d+)\s*$", RegexOptions.Multiline);
            value = 0;
            return m.Success && int.TryParse(m.Groups["v"].Value, out value);
        }

        /// <summary>
        /// The body of the config's <c>Variant:</c> block — every line indented deeper than
        /// the block header. Scoping matters: a fauna variant nests an FMOD event several
        /// levels down, and the config carries sibling keys (SpreadElements, ElementPalette)
        /// at the block's own indentation that must NOT be swept in.
        /// </summary>
        static bool TryVariantBlock(string text, out string block)
        {
            // `\r?` / `[^\r\n]` are platform-critical, not tidiness. .gitattributes sets
            // `* text=auto`, so an .asset is LF in the repository and CRLF in a WINDOWS working
            // tree - and a literal `\n` in the pattern cannot match the `\r\n` there. Without
            // this the block was never found on Windows and every Variant assertion below passed
            // VACUOUSLY. Measured on .NET: 1 match on LF, 0 on CRLF; 1/1 once anchored this way.
            var m = Regex.Match(text, @"^  Variant:\r?\n(?<body>(?:^    [^\r\n]*\r?\n?)+)",
                                RegexOptions.Multiline);
            block = m.Success ? m.Groups["body"].Value : null;
            return m.Success;
        }

        sealed class Canonical
        {
            public string Name;
            public string Path;
            public int Element;
            public bool HasVariantBlock;
        }

        /// <summary>
        /// The canonical (species × element) assets: everything in _SO_Assets/Lifeforms that
        /// names one of the four droppable elements. Deliberately excludes the two
        /// element-less worm-colony configs: WormColonyConfig (the colony tuning SO, not a
        /// spawn config at all) and WormColonyFaunaConfig ("Worm Colony Kaiju" — Element.None,
        /// SpreadElements OFF, EMPTY palette, Variant disabled, so it rolls nothing and
        /// authors nothing). The second is a DORMANT authoring gap, and worth naming so the
        /// exclusion is not mistaken for a clean bill: nothing in Assets/ references its guid
        /// — the Spawn Matrix toy's "Worm Colony" entry lists the four canonical
        /// "Worm Colony &lt;Element&gt;" siblings, which all author 2.277 — so nothing spawns
        /// it today, but anything that did would render the platform default heart.
        /// LifeFormCrystalValidator reports it; it is an ecology call (give it an element, or
        /// a heart, or retire it), not a test fix.
        /// </summary>
        static List<Canonical> ReadCanonical()
        {
            Assert.IsTrue(Directory.Exists(LifeformDir), $"{LifeformDir} is missing.");

            var found = new List<Canonical>();
            foreach (string path in Directory.GetFiles(LifeformDir, "*.asset").OrderBy(p => p))
            {
                string text = File.ReadAllText(path).Replace("\r\n", "\n");
                if (!TryInt(text, "Element", out int element)) continue;
                if (!DroppableElements.Contains(element)) continue;

                var entry = new Canonical
                {
                    Name = System.IO.Path.GetFileNameWithoutExtension(path),
                    Path = path.Replace('\\', '/'),
                    Element = element,
                    HasVariantBlock = TryVariantBlock(text, out _),
                };
                found.Add(entry);
            }
            return found;
        }

        // ── (a) the species × element matrix is whole ───────────────────────────

        [Test]
        public void CanonicalLifeformSet_IsTheWholeSpeciesTimesElementMatrix()
        {
            var canonical = ReadCanonical();

            Assert.AreEqual(CanonicalVariantCount, canonical.Count,
                "The canonical (species × element) set changed size. If a species was added or " +
                "removed, update CanonicalVariantCount - a shrinking set silently turns the " +
                "matrix assertion below into a no-op.\n" +
                string.Join("\n", canonical.Select(c => "  " + c.Name)));

            foreach (var species in canonical.GroupBy(c => Regex.Replace(c.Name, @" (Charge|Mass|Space|Time)$", "")))
            {
                CollectionAssert.AreEquivalent(DroppableElements, species.Select(c => c.Element).ToArray(),
                    $"'{species.Key}' does not carry exactly one asset per droppable element. " +
                    "A lifeform is its species and its element — all four, or the roster has a hole.");
            }
        }

        // ── (b) the retired level surface is gone from the data too ──────────

        [Test]
        public void NoAssetStillCarriesARetiredLifeformLevelField()
        {
            var pattern = new Regex(
                $@"^\s*({string.Join("|", RetiredLevelFields.Select(Regex.Escape))}):",
                RegexOptions.Multiline);

            var offenders = new List<string>();
            foreach (string path in Directory.EnumerateFiles(AssetsRoot, "*.asset", SearchOption.AllDirectories))
            {
                string text;
                try { text = File.ReadAllText(path); }
                catch (IOException) { continue; }

                foreach (Match m in pattern.Matches(text))
                    offenders.Add($"  {path.Replace('\\', '/')}: {m.Groups[1].Value}");
            }

            Assert.IsEmpty(offenders,
                "These serialized fields were removed with lifeform levels (Docs/ECOSYSTEM.md §40). " +
                "Unity never prunes a modification it cannot resolve, so they linger in the YAML and " +
                "read as live authoring to the next person who opens the file. Delete the keys.\n" +
                string.Join("\n", offenders));
        }
    }
}
#endif
