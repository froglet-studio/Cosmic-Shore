using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CosmicShore.Editor.Codex
{
    /// <summary>
    /// Reads the project and produces codex entries - every ethirion (crystal) and every ecology
    /// species (flora and fauna) - then MERGES them into the live <see cref="CodexSO"/> under the
    /// field-ownership contract documented on <see cref="CodexEntry"/>.
    ///
    /// <para><b>The merge is the whole design.</b> A generator that rebuilds the asset from
    /// scratch is useless the moment a designer writes a paragraph of body copy, because the next
    /// scan eats it - so the harvester owns only the facts it can re-derive (wiring, numbers),
    /// proposes the things it can guess (name, accent), and never touches prose, ordering,
    /// discovery or the preview pose. Re-running it is therefore always safe, which is what makes
    /// it a tool rather than a one-off.</para>
    ///
    /// <para><b>Species are grouped by PREFAB, not by asset name.</b> Names lie: the fauna configs
    /// include a <c>WormColonyFaunaConfig</c> alongside four <c>Worm Colony &lt;Element&gt;</c>
    /// assets, and a name-prefix grouping files it as a fifth species. All five point at one
    /// prefab, which is the thing the player actually meets, so the prefab is the identity and the
    /// display name is settled by majority vote among the configs that share it.</para>
    /// </summary>
    public static class CodexHarvester
    {
        // ── Entry point ──────────────────────────────────────────────────────────

        /// <summary>
        /// Scan the project and fold the results into <paramref name="codex"/>. Marks the asset
        /// dirty when anything changed; the caller saves.
        /// </summary>
        public static CodexHarvestReport ScanAndMerge(CodexSO codex)
        {
            var report = new CodexHarvestReport();
            if (!codex)
            {
                report.Warnings.Add("No codex asset supplied.");
                return report;
            }

            var usage = BuildCellUsage(report);

            MergeList(codex, codex.Ethirions, BuildEthirionEntries(report), report);
            MergeList(codex, codex.Ecology, BuildEcologyEntries(usage, report), report);

            FlagOrphans(codex.Ethirions, report);
            FlagOrphans(codex.Ecology, report);

            if (report.AnyChange) EditorUtility.SetDirty(codex);
            return report;
        }

        // ── Ethirions ────────────────────────────────────────────────────────────

        /// <summary>
        /// One entry per element family: the four elemental crystals from the project's
        /// <see cref="ElementalCrystalSetSO"/>, plus Omni.
        ///
        /// <para>An ethirion has no variants of its own. Its SIZE is not a property of the
        /// crystal at all - a heart is sized to the LIFEFORM that carries it, authored per element
        /// in that species' variant tuning (Docs/ECOSYSTEM.md 40.2), so the size belongs on the
        /// flora and fauna entries where a reader can see whose heart it is. What this entry
        /// states instead is the BAND the whole roster spans, harvested from the shipped assets
        /// rather than restated, so it cannot drift from what was authored.</para>
        /// </summary>
        public static List<CodexEntry> BuildEthirionEntries(CodexHarvestReport report)
        {
            var entries = new List<CodexEntry>();

            var set = FindSingle<ElementalCrystalSetSO>();
            if (!set)
            {
                report.Warnings.Add(
                    "No ElementalCrystalSetSO found - the four elemental ethirions were skipped. " +
                    "Expected Assets/Resources/ElementalCrystalSet.asset.");
            }

            var capture = FindSingle<CrystalCaptureConfigSO>();
            var captureLine = capture
                ? $"Skim to collect · {capture.TotalDuration:0.00} s capture"
                : "Skim to collect";

            var elements = new[] { Element.Charge, Element.Mass, Element.Space, Element.Time };
            int speciesCount = CountEcologySpecies();

            if (set)
            {
                foreach (var element in elements)
                {
                    var crystal = set.GetPrefab(element);
                    if (!crystal)
                    {
                        report.Warnings.Add($"ElementalCrystalSet has no prefab for {element}.");
                        continue;
                    }

                    var entry = NewEntry(CodexKingdom.Ethirion, element.ToString());
                    entry.SourcePrefab = crystal.gameObject;
                    entry.AccentColor = AccentFor(element);
                    entry.DiscoveryKey = $"collect.ethirion.{Slug(element.ToString())}";

                    Add(entry.Stats, "Element", element.ToString());
                    Add(entry.Stats, "Kind", "Elemental — every lifeform's heart, and a loose pickup");
                    Add(entry.Stats, "Collection", captureLine);
                    Add(entry.Stats, "Dropped by", speciesCount > 0
                        ? $"{speciesCount} species — one of the four, per lifeform"
                        : null);
                    Add(entry.Stats, "Reward", "Raises the collecting vessel's " + element +
                                               " level, scaled by the crystal's world size");
                    Add(entry.Stats, "Model", MeshSummary(crystal.gameObject));
                    Add(entry.Stats, "Heart size", HeartSizeBandLine(set));

                    entries.Add(entry);
                }
            }

            var omni = FindOmniCrystalPrefab();
            if (omni)
            {
                var entry = NewEntry(CodexKingdom.Ethirion, "Omni");
                entry.SourcePrefab = omni;
                entry.AccentColor = AccentFor(Element.Omni);
                entry.DiscoveryKey = "collect.ethirion.omni";

                Add(entry.Stats, "Element", "Omni — no element");
                Add(entry.Stats, "Kind", "Free to every pilot, whatever their domain");
                Add(entry.Stats, "Collection", "Fly into it");
                Add(entry.Stats, "Used by", "Mode objectives, the Dolphin's blast trigger, the " +
                                            "Sparrow's missile reload, the Scarab's ball forge");
                Add(entry.Stats, "Model", MeshSummary(omni));
                entries.Add(entry);
            }
            else
            {
                report.Warnings.Add("No omni crystal prefab found (a Crystal carrying exactly an " +
                                    "OmniCrystalImpactor). The Omni ethirion was skipped.");
            }

            return entries;
        }

        /// <summary>
        /// The omni crystal prefab: a <see cref="Crystal"/> whose impactor is EXACTLY an
        /// <see cref="OmniCrystalImpactor"/>. The exact-type test matters -
        /// <see cref="TeamCrystalImpactor"/> derives from it, so an <c>is</c> check also matches
        /// every domain-locked crystal in the project.
        /// </summary>
        public static GameObject FindOmniCrystalPrefab()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("/Environment/")) continue;

                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!go || !go.GetComponent<Crystal>()) continue;

                var impactor = go.GetComponent<OmniCrystalImpactor>();
                if (impactor && impactor.GetType() == typeof(OmniCrystalImpactor)) return go;
            }
            return null;
        }

        // ── Ecology ──────────────────────────────────────────────────────────────

        /// <summary>
        /// One entry per SPECIES, its four elements folded in as variants. Flora and fauna are
        /// harvested through the same shape because a codex page is the same shape - the kingdoms
        /// differ only in which numbers are worth printing.
        /// </summary>
        public static List<CodexEntry> BuildEcologyEntries(
            Dictionary<Object, List<string>> usage, CodexHarvestReport report)
        {
            var entries = new List<CodexEntry>();

            var floraConfigs = LoadAll<FloraConfigurationSO>();
            var faunaConfigs = LoadAll<FaunaConfigurationSO>();

            foreach (var group in floraConfigs.Where(c => c.FloraPrefab)
                                              .GroupBy(c => c.FloraPrefab.gameObject))
                entries.Add(BuildFloraEntry(group.Key, group.ToList(), usage));

            foreach (var group in faunaConfigs.Where(c => c.FaunaPrefab)
                                              .GroupBy(c => c.FaunaPrefab.gameObject))
                entries.Add(BuildFaunaEntry(group.Key, group.ToList(), usage));

            foreach (var c in floraConfigs.Where(c => !c.FloraPrefab))
                report.Warnings.Add($"Flora config '{c.name}' has no prefab — no codex entry.");
            foreach (var c in faunaConfigs.Where(c => !c.FaunaPrefab))
                report.Warnings.Add($"Fauna config '{c.name}' has no prefab — no codex entry.");

            entries.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.Ordinal));
            return entries;
        }

        static CodexEntry BuildFloraEntry(GameObject prefab, List<FloraConfigurationSO> configs,
            Dictionary<Object, List<string>> usage)
        {
            var entry = NewEntry(CodexKingdom.Flora, SpeciesName(configs.Select(c => c.name), prefab));
            entry.SourcePrefab = prefab;
            entry.DiscoveryKey = $"see.{Slug(entry.DisplayName)}";

            var flora = prefab.GetComponent<Flora>();
            var budget = ProbeInt(flora, "maxTotalSpawnedObjects");
            var quota = SummarizeInt(configs.Where(c => c.GrowthPerOffspring > 0)
                                            .Select(c => (int?)c.GrowthPerOffspring));

            Add(entry.Stats, "Growth model", GrowthModel(flora));
            Add(entry.Stats, "Prisms per plant",
                budget.HasValue && budget.Value > 0 ? budget.Value.ToString("N0") : null);
            Add(entry.Stats, "Breeds", quota != null
                ? $"Yes — one child per {quota} prisms grown"
                : "No — a level-1 forest, seeded and grazed but never reproducing");
            Add(entry.Stats, "Population cap", SummarizeInt(configs.Select(c => (int?)c.MaxLivePopulation)));
            Add(entry.Stats, "Planted on", SummarizeText(configs.Select(c => c.PreferredSites.ToString())));
            Add(entry.Stats, "Found in", UsageLine(configs.Cast<Object>(), usage));

            foreach (var cfg in OrderByElement(configs, c => c.Element))
            {
                var variant = new CodexVariant
                {
                    Label = cfg.Element == Element.None ? cfg.name : cfg.Element.ToString(),
                    Element = cfg.Element,
                    SourceConfig = cfg,
                    SourcePrefab = prefab,
                };
                Add(variant.Stats, "Seed floor", Count(cfg.PopulationSize, "plant"));
                Add(variant.Stats, "Live cap", Count(cfg.MaxLivePopulation, "plant"));
                Add(variant.Stats, "Growth per child", cfg.GrowthPerOffspring > 0
                    ? $"{cfg.GrowthPerOffspring} prisms" : "Does not reproduce");
                Add(variant.Stats, "Children per birth", cfg.OffspringPerBirth > 1
                    ? cfg.OffspringPerBirth.ToString() : null);
                Add(variant.Stats, "Reproduction cooldown", Seconds(cfg.ReproductionCooldownSeconds));
                Add(variant.Stats, "Heart size",
                    HeartSizeLine(cfg.Variant != null ? cfg.Variant.HeartWorldScale : 0f));
                Add(variant.Stats, "Initial seeding", Count(cfg.InitialSpawnCount, "plant"));
                if (cfg.Element == Element.Charge)
                    Add(variant.Stats, "Elemental law", "Charge armours its leaves — grazing one " +
                                                        "costs two passes, because the first only sheds the shield");
                if (cfg.Element == Element.Time)
                    Add(variant.Stats, "Elemental law", "Time breeds at 1.25× the fleet rate; the " +
                                                        "other three run at 0.8×");
                entry.Variants.Add(variant);
            }

            return entry;
        }

        static CodexEntry BuildFaunaEntry(GameObject prefab, List<FaunaConfigurationSO> configs,
            Dictionary<Object, List<string>> usage)
        {
            var entry = NewEntry(CodexKingdom.Fauna, SpeciesName(configs.Select(c => c.name), prefab));
            entry.SourcePrefab = prefab;
            entry.DiscoveryKey = $"see.{Slug(entry.DisplayName)}";

            var fauna = prefab.GetComponent<Fauna>();
            Add(entry.Stats, "Behaviour", BehaviourModel(fauna));
            Add(entry.Stats, "Diet", fauna
                ? fauna.Diet == FaunaDiet.Predator
                    ? "Predator — eats other creatures; ignores prism mass"
                    : "Herbivore — grazes prism mass (flora canopy and vessel trails)"
                : null);
            Add(entry.Stats, "Starves after", Seconds(ProbeFloat(fauna, "starvationSeconds")));
            Add(entry.Stats, "Speed", SpeedLine(fauna));
            Add(entry.Stats, "Variants", "Four — one per element. A creature is its species " +
                                        "and its element, and nothing else");
            Add(entry.Stats, "Population cap", SummarizeInt(configs.Select(c => (int?)c.MaxLivePopulation)));
            Add(entry.Stats, "Found in", UsageLine(configs.Cast<Object>(), usage));

            foreach (var cfg in OrderByElement(configs, c => c.Element))
            {
                var variant = new CodexVariant
                {
                    Label = cfg.Element == Element.None ? cfg.name : cfg.Element.ToString(),
                    Element = cfg.Element,
                    SourceConfig = cfg,
                    SourcePrefab = prefab,
                };
                Add(variant.Stats, "Shoal size", Count(cfg.PopulationSize, "creature"));
                Add(variant.Stats, "Live cap", Count(cfg.MaxLivePopulation, "creature"));
                Add(variant.Stats, "Feeds per child", cfg.FeedsPerOffspring > 0
                    ? cfg.FeedsPerOffspring.ToString() : "Does not reproduce");
                Add(variant.Stats, "Children per birth", cfg.OffspringPerBirth > 1
                    ? cfg.OffspringPerBirth.ToString() : null);
                Add(variant.Stats, "Reproduction cooldown", Seconds(cfg.ReproductionCooldownSeconds));
                Add(variant.Stats, "Heart size",
                    HeartSizeLine(cfg.Variant != null ? cfg.Variant.HeartWorldScale : 0f));
                Add(variant.Stats, "Initial spawn", Count(cfg.InitialSpawnCount, "creature"));
                if (cfg.BandOuterRadius > 0f)
                    Add(variant.Stats, "Roams", $"{cfg.BandInnerRadius:0}–{cfg.BandOuterRadius:0} " +
                                                "units from the cell centre");
                entry.Variants.Add(variant);
            }

            return entry;
        }

        // ── Merge ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fold harvested entries into <paramref name="live"/>, matching on
        /// <see cref="CodexEntry.Id"/>. See the contract on <see cref="CodexEntry"/> - this method
        /// is that contract in code, and nothing else in the tool may write these fields.
        /// </summary>
        static void MergeList(CodexSO codex, List<CodexEntry> live, List<CodexEntry> harvested,
            CodexHarvestReport report)
        {
            foreach (var fresh in harvested)
            {
                var existing = codex.Find(fresh.Id);

                if (existing == null)
                {
                    live.Add(fresh);
                    report.Added.Add($"{fresh.Kingdom} · {fresh.DisplayName}");
                    continue;
                }

                if (existing.LockAutoHarvest)
                {
                    report.Locked.Add($"{existing.Kingdom} · {existing.DisplayName}");
                    continue;
                }

                if (ApplyHarvest(existing, fresh))
                    report.Updated.Add($"{existing.Kingdom} · {existing.DisplayName}");
            }
        }

        /// <summary>Returns true when anything actually changed.</summary>
        static bool ApplyHarvest(CodexEntry target, CodexEntry fresh)
        {
            bool changed = false;

            // Harvester-owned.
            if (target.Kingdom != fresh.Kingdom) { target.Kingdom = fresh.Kingdom; changed = true; }
            if (target.SourcePrefab != fresh.SourcePrefab) { target.SourcePrefab = fresh.SourcePrefab; changed = true; }

            // Filled only when empty - a human's value always wins.
            if (string.IsNullOrWhiteSpace(target.DisplayName) && !string.IsNullOrWhiteSpace(fresh.DisplayName))
            { target.DisplayName = fresh.DisplayName; changed = true; }
            if (target.AccentColor.a <= 0f && fresh.AccentColor.a > 0f)
            { target.AccentColor = fresh.AccentColor; changed = true; }
            if (string.IsNullOrWhiteSpace(target.DiscoveryKey) && !string.IsNullOrWhiteSpace(fresh.DiscoveryKey))
            { target.DiscoveryKey = fresh.DiscoveryKey; changed = true; }

            if (MergeStats(target.Stats, fresh.Stats)) changed = true;
            if (MergeVariants(target, fresh)) changed = true;

            return changed;
        }

        /// <summary>
        /// Replace the harvested rows, keep every authored row, and keep authored rows in place
        /// rather than herding them to the bottom - a designer who put a line of flavour between
        /// two numbers meant it to be there.
        /// </summary>
        static bool MergeStats(List<CodexStat> target, List<CodexStat> fresh)
        {
            var authored = new List<(int index, CodexStat stat)>();
            for (int i = 0; i < target.Count; i++)
                if (target[i].Authored) authored.Add((i, target[i]));

            var rebuilt = new List<CodexStat>(fresh);
            foreach (var (index, stat) in authored)
                rebuilt.Insert(Mathf.Clamp(index, 0, rebuilt.Count), stat);

            if (StatsEqual(target, rebuilt)) return false;

            target.Clear();
            target.AddRange(rebuilt);
            return true;
        }

        static bool StatsEqual(List<CodexStat> a, List<CodexStat> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i].Label != b[i].Label || a[i].Value != b[i].Value || a[i].Authored != b[i].Authored)
                    return false;
            return true;
        }

        /// <summary>
        /// Variants are matched on label. Per-variant IMAGES are kept (they are baked art, not
        /// harvested facts); wiring and stats are re-derived. A variant the project no longer
        /// produces is dropped - unlike an entry, a variant carries no prose worth protecting.
        /// </summary>
        static bool MergeVariants(CodexEntry target, CodexEntry fresh)
        {
            var keptImages = target.Variants
                .Where(v => v != null && v.Image)
                .ToDictionary(v => v.Label ?? string.Empty, v => v.Image, StringComparer.Ordinal);

            bool changed = target.Variants.Count != fresh.Variants.Count;

            foreach (var variant in fresh.Variants)
                if (keptImages.TryGetValue(variant.Label ?? string.Empty, out var image))
                    variant.Image = image;

            if (!changed)
            {
                for (int i = 0; i < fresh.Variants.Count && !changed; i++)
                {
                    var a = target.Variants[i];
                    var b = fresh.Variants[i];
                    changed = a == null || a.Label != b.Label || a.Element != b.Element ||
                              a.SourceConfig != b.SourceConfig || a.SourcePrefab != b.SourcePrefab ||
                              !StatsEqual(a.Stats, b.Stats);
                }
            }

            if (!changed) return false;

            target.Variants.Clear();
            target.Variants.AddRange(fresh.Variants);
            return true;
        }

        static void FlagOrphans(List<CodexEntry> live, CodexHarvestReport report)
        {
            foreach (var entry in live)
            {
                if (entry == null || entry.LockAutoHarvest) continue;
                if (!entry.SourcePrefab)
                    report.Orphans.Add($"{entry.Kingdom} · {entry.DisplayName} (no source prefab)");
            }
        }

        // ── Cell usage ───────────────────────────────────────────────────────────

        /// <summary>
        /// config asset → the cells that spawn it. "Where do I find one?" is the first question a
        /// player asks an encyclopedia, and it is not written down anywhere else in the project -
        /// it only exists as the join between a cell's spawn profile and a species config.
        /// </summary>
        static Dictionary<Object, List<string>> BuildCellUsage(CodexHarvestReport report)
        {
            var usage = new Dictionary<Object, List<string>>();

            foreach (var cell in LoadAll<CellConfigDataSO>())
            {
                if (!cell || !cell.SpawnProfile) continue;
                var cellName = string.IsNullOrWhiteSpace(cell.CellName) ? cell.name : cell.CellName;

                foreach (var cfg in cell.SpawnProfile.SupportedFloras)
                {
                    Record(cfg, cellName);
                    if (cfg) foreach (var alt in cfg.ElementPalette) Record(alt, cellName);
                }
                foreach (var cfg in cell.SpawnProfile.SupportedFaunas)
                {
                    Record(cfg, cellName);
                    if (cfg) foreach (var alt in cfg.ElementPalette) Record(alt, cellName);
                }
            }

            if (usage.Count == 0)
                report.Warnings.Add("No cell configs referenced any species — \"Found in\" will be blank.");

            return usage;

            void Record(Object cfg, string cellName)
            {
                if (!cfg) return;
                if (!usage.TryGetValue(cfg, out var list)) usage[cfg] = list = new List<string>();
                if (!list.Contains(cellName)) list.Add(cellName);
            }
        }

        static string UsageLine(IEnumerable<Object> configs, Dictionary<Object, List<string>> usage)
        {
            var cells = new List<string>();
            foreach (var cfg in configs)
                if (cfg && usage.TryGetValue(cfg, out var list))
                    foreach (var c in list) if (!cells.Contains(c)) cells.Add(c);

            if (cells.Count == 0) return "Released by hand — no cell seeds it";
            cells.Sort(StringComparer.Ordinal);
            return cells.Count <= 6
                ? string.Join(", ", cells)
                : string.Join(", ", cells.Take(6)) + $" +{cells.Count - 6} more";
        }

        // ── Naming ───────────────────────────────────────────────────────────────

        /// <summary>
        /// The species' display name: strip the trailing element and kingdom words off each
        /// config's asset name, then take the MAJORITY. Majority rather than "first" because one
        /// odd asset in a group (<c>WormColonyFaunaConfig</c>) must not out-vote four that agree.
        /// </summary>
        public static string SpeciesName(IEnumerable<string> configNames, GameObject prefab)
        {
            var votes = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var raw in configNames)
            {
                var name = StripSuffixes(raw);
                if (string.IsNullOrWhiteSpace(name)) continue;
                votes.TryGetValue(name, out var n);
                votes[name] = n + 1;
            }

            if (votes.Count == 0) return prefab ? prefab.name : "Unnamed";

            var best = votes.OrderByDescending(kv => kv.Value)
                            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                            .First();
            return best.Key;
        }

        static string StripSuffixes(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return name;
            var s = name.Trim();
            foreach (var element in new[] { " Charge", " Mass", " Space", " Time" })
                if (s.EndsWith(element, StringComparison.Ordinal))
                { s = s[..^element.Length]; break; }
            foreach (var kingdom in new[] { " Flora", " Fauna" })
                if (s.EndsWith(kingdom, StringComparison.Ordinal))
                { s = s[..^kingdom.Length]; break; }
            return s.Trim();
        }

        public static string Slug(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "unnamed";
            var sb = new StringBuilder(value.Length);
            bool lastDash = false;
            foreach (var c in value.Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c)) { sb.Append(c); lastDash = false; }
                else if (!lastDash && sb.Length > 0) { sb.Append('-'); lastDash = true; }
            }
            return sb.ToString().Trim('-') is { Length: > 0 } s ? s : "unnamed";
        }

        static CodexEntry NewEntry(CodexKingdom kingdom, string displayName) => new()
        {
            Id = $"{kingdom.ToString().ToLowerInvariant()}.{Slug(displayName)}",
            Kingdom = kingdom,
            DisplayName = displayName,
            UnlockedByDefault = true,
        };

        // ── Formatting helpers ───────────────────────────────────────────────────

        static void Add(List<CodexStat> stats, string label, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            stats.Add(new CodexStat(label, value));
        }

        static string Count(int value, string noun) =>
            value > 0 ? $"{value} {noun}{(value == 1 ? "" : "s")}" : null;

        static string Seconds(float value) => value > 0f ? $"{value:0.##} s" : null;

        /// <summary>
        /// One lifeform's authored heart size. A non-positive value is the "not authored"
        /// sentinel, and it is worth SAYING so rather than hiding the row: it means that
        /// species renders the platform default, which is a real (and usually unintended)
        /// state a reader of the codex should be able to see. Docs/ECOSYSTEM.md 40.2.
        /// </summary>
        static string HeartSizeLine(float authored) => authored > 0f
            ? $"{authored:0.00} world scale"
            : $"{LifeFormCrystal.DefaultHeartWorldScale:0.00} world scale (platform default — " +
              "this variant authors none)";

        /// <summary>
        /// The BAND every lifeform heart in the project spans, measured from the shipped assets
        /// rather than restated, so this line cannot drift from what was authored. It also names
        /// the ceiling, because the band's whole design property is that it stays under the world
        /// scale at which the collect reward saturates - past that, two visibly different hearts
        /// pay the same (Docs/ECOSYSTEM.md 40.2).
        /// </summary>
        static string HeartSizeBandLine(ElementalCrystalSetSO set)
        {
            float lo = float.MaxValue, hi = 0f;
            foreach (var cfg in LoadAll<FaunaConfigurationSO>())
                Consider(cfg && cfg.Variant != null ? cfg.Variant.HeartWorldScale : 0f);
            foreach (var cfg in LoadAll<FloraConfigurationSO>())
                Consider(cfg && cfg.Variant != null ? cfg.Variant.HeartWorldScale : 0f);

            void Consider(float v)
            {
                if (v <= 0f) return;
                if (v < lo) lo = v;
                if (v > hi) hi = v;
            }

            if (hi <= 0f)
                return set ? $"{set.DefaultHeartWorldScale:0.00} world scale (nothing authored)" : null;

            return $"{lo:0.00}–{hi:0.00} world scale across the roster — sized to the lifeform " +
                   $"that carries it, held under {ElementalCrystalSetSO.MaxSafeHeartWorldScale:0.0}";
        }

        static string SummarizeInt(IEnumerable<int?> values)
        {
            var set = values.Where(v => v.HasValue && v.Value > 0).Select(v => v.Value).Distinct().ToList();
            if (set.Count == 0) return null;
            set.Sort();
            return set.Count == 1 ? set[0].ToString() : $"{set[0]}–{set[^1]} (by element)";
        }

        static string SummarizeText(IEnumerable<string> values)
        {
            var set = values.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.Ordinal).ToList();
            if (set.Count == 0) return null;
            return set.Count == 1 ? set[0] : string.Join(" / ", set);
        }

        static IEnumerable<T> OrderByElement<T>(IEnumerable<T> items, Func<T, Element> element) =>
            items.OrderBy(i => element(i) switch
            {
                Element.Charge => 0, Element.Mass => 1, Element.Space => 2, Element.Time => 3, _ => 4,
            });

        static string GrowthModel(Flora flora) => flora == null ? null : flora.GetType().Name switch
        {
            "AssembledFlora" => "Lattice colony — tiles a repeating surface, one plant per site",
            "BranchingFlora" => "Branching — grows outward from a stem",
            "PhyllotacticFlora" => "Phyllotactic — whorled leaves on a spiral",
            var other => other,
        };

        static string BehaviourModel(Fauna fauna) => fauna == null ? null : fauna.GetType().Name switch
        {
            "LightFauna" => "Shoals — separation, cohesion and a shared goal",
            "Boid" => "Boid shoal — the classic flocking three rules",
            "QuadFish" => "Swimmer — steers to a goal on its own",
            "WormFauna" => "Colony — a chain of segments following the head; splits when cut",
            var other => other,
        };

        static string SpeedLine(Fauna fauna)
        {
            if (fauna == null) return null;
            var min = ProbeFloat(fauna, "minSpeed");
            var max = ProbeFloat(fauna, "maxSpeed");
            if (min <= 0f && max <= 0f) return null;
            return max > min ? $"{min:0.##}–{max:0.##} units/s" : $"{max:0.##} units/s";
        }

        static string MeshSummary(GameObject prefab)
        {
            if (!prefab) return null;
            int meshes = 0, tris = 0;
            bool countedTriangles = true;
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf || !mf.sharedMesh) continue;
                meshes++;

                // A mesh imported without Read/Write refuses triangle access. Editor-only code
                // still must not assume otherwise: report the part count alone rather than throw.
                if (mf.sharedMesh.isReadable) tris += mf.sharedMesh.triangles.Length / 3;
                else countedTriangles = false;
            }
            if (meshes == 0) return null;
            if (!countedTriangles) return meshes == 1 ? "1 part" : $"{meshes} parts";
            return meshes == 1 ? $"{tris:N0} triangles" : $"{meshes} parts, {tris:N0} triangles";
        }

        static Color AccentFor(Element element) => element switch
        {
            Element.Charge => new Color(0.98f, 0.75f, 0.18f, 1f),
            Element.Mass => new Color(0.93f, 0.27f, 0.38f, 1f),
            Element.Space => new Color(0.24f, 0.60f, 0.95f, 1f),
            Element.Time => new Color(0.62f, 0.42f, 0.94f, 1f),
            Element.Omni => new Color(0.62f, 0.83f, 0.24f, 1f),
            _ => new Color(0f, 0f, 0f, 0f),
        };

        // ── Serialized-field probes ──────────────────────────────────────────────
        //
        // Read by NAME through SerializedObject rather than by a direct field reference, because
        // most of these live on subclasses (LightFauna's tuning is on a LightFaunaDataSO it holds)
        // or are private. The cost is that a rename drops the stat instead of failing the build -
        // which is the right trade for an encyclopedia: a missing row is a missing row, and a
        // hard reference here would make every ecology field rename a compile error in a tool.

        static float ProbeFloat(Object target, string field)
        {
            var prop = FindProperty(target, field);
            return prop != null && prop.propertyType == SerializedPropertyType.Float ? prop.floatValue : 0f;
        }

        static int? ProbeInt(Object target, string field)
        {
            var prop = FindProperty(target, field);
            return prop != null && prop.propertyType == SerializedPropertyType.Integer ? prop.intValue : null;
        }

        /// <summary>
        /// Look for <paramref name="field"/> on the object, then one level down through any
        /// object-reference field it holds (a data SO). One level only - deeper is guessing.
        /// </summary>
        static SerializedProperty FindProperty(Object target, string field)
        {
            if (!target) return null;

            var so = new SerializedObject(target);
            var direct = so.FindProperty(field);
            if (direct != null) return direct;

            var iterator = so.GetIterator();
            while (iterator.NextVisible(true))
            {
                if (iterator.propertyType != SerializedPropertyType.ObjectReference) continue;
                var child = iterator.objectReferenceValue;
                if (!child || child is GameObject || child is Component) continue;

                var nested = new SerializedObject(child).FindProperty(field);
                if (nested != null) return nested;
            }
            return null;
        }

        // ── Asset lookup ─────────────────────────────────────────────────────────

        public static List<T> LoadAll<T>() where T : Object =>
            AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(a => a)
                .ToList();

        static T FindSingle<T>() where T : Object => LoadAll<T>().FirstOrDefault();

        static int CountEcologySpecies() =>
            LoadAll<FloraConfigurationSO>().Where(c => c && c.FloraPrefab)
                .Select(c => c.FloraPrefab.gameObject).Distinct().Count() +
            LoadAll<FaunaConfigurationSO>().Where(c => c && c.FaunaPrefab)
                .Select(c => c.FaunaPrefab.gameObject).Distinct().Count();
    }
}
