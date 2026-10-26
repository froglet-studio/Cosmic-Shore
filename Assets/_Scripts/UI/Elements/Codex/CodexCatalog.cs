using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>The Codex's stops, in LB / RB order (Docs/MENU_REDESIGN_B.md section 88). Flora and Fauna are the two
    /// stops of the ECOLOGY chapter. Values index nothing serialized but the authored names, so they stay explicit.</summary>
    public enum CodexChapter
    {
        Ethirions = 0,
        Elements = 1,
        Flora = 2,
        Fauna = 3,
        Pilots = 4,
        Vessels = 5,
        Artifacts = 6,
        Tools = 7,
    }

    /// <summary>One line of facts on a page: a label and its value.</summary>
    public readonly struct CodexFact
    {
        public readonly string Label;
        public readonly string Value;
        public CodexFact(string label, string value) { Label = label; Value = value; }
    }

    /// <summary>One element cell on a page (a lifeform's variant): the element's ethirion, its name, a few lines.</summary>
    public readonly struct CodexCell
    {
        public readonly Sprite Icon;
        public readonly string Title;
        public readonly string Lines;
        public CodexCell(Sprite icon, string title, string lines) { Icon = icon; Title = title; Lines = lines; }
    }

    /// <summary>One row on a page (an ability, a vessel, a painting): an icon, a title and a line under it.</summary>
    public readonly struct CodexRow
    {
        public readonly Sprite Icon;
        public readonly string Title;
        public readonly string Line;
        public CodexRow(Sprite icon, string title, string line) { Icon = icon; Title = title; Line = line; }
    }

    /// <summary>The open page on the right of the Codex.</summary>
    public sealed class CodexPage
    {
        public Sprite Image;
        public string Kicker = string.Empty;
        public string Title = string.Empty;
        public string Body = string.Empty;
        public string FactsHeading = string.Empty;
        public readonly List<CodexFact> Facts = new();
        public string CellsHeading = string.Empty;
        public readonly List<CodexCell> Cells = new();
        public string RowsHeading = string.Empty;
        public readonly List<CodexRow> Rows = new();
    }

    /// <summary>One tile of the grid on the left, and the page it opens.</summary>
    public sealed class CodexItem
    {
        public Sprite Image;
        public string Name = string.Empty;
        public string Sub = string.Empty;
        public CodexPage Page;
    }

    /// <summary>
    /// Every word the Codex adds around the game's own data (CLAUDE.md: all player-facing text is authored). The data
    /// itself - a species' stats, a pilot's line, an ability's name - is already authored on its own asset.
    /// </summary>
    [Serializable]
    public sealed class CodexWords
    {
        [Tooltip("One name per element, Charge to Time.")]
        public string[] elementNames = { "Charge", "Mass", "Space", "Time" };
        [Tooltip("What each element is for, in a line, Charge to Time.")]
        public string[] elementLines = { "The strike you carry", "What you build", "How far you reach", "How fast you move" };
        [Tooltip("The element page's body. {element} is its name.")]
        public string elementBody = "Skim {element} ethirions or fly the Element Charger to raise it. Its ability wakes at level 5, and so does the hull's level-5 upgrade.";
        public string elementKicker = "Element";
        [Tooltip("The element page's list of every hull's ability.")]
        public string elementRowsHeading = "On every vessel";
        [Tooltip("A vessel's line on an element page. {ability} and {upgrade} are from the hull's ability map.")]
        public string elementRowTemplate = "{ability} - level 5: {upgrade}";
        [Tooltip("A vessel's line when its ability has no level-5 upgrade. {ability} is the ability.")]
        public string elementRowNoUpgrade = "{ability}";
        [Tooltip("The ability name for an element no ability is designed for yet.")]
        public string openSlot = "Open slot";

        public string ethirionKicker = "Ethirion";
        public string floraKicker = "Flora";
        public string faunaKicker = "Fauna";
        [Tooltip("{group} is the tool's category (Pilot, World, Creation).")]
        public string toolKicker = "Tool - {group}";
        public string vesselKicker = "Vessel";
        [Tooltip("{element} and {vessel} name the pilot's element and hull.")]
        public string pilotKicker = "{element} pilot - {vessel}";

        public string factsHeading = "Facts";
        [Tooltip("The lifeform page's four element cells: its heart is that element's ethirion.")]
        public string cellsHeading = "By element";
        [Tooltip("A line in an element cell. {label} and {value} are the variant's stat.")]
        public string cellLineTemplate = "{label}: {value}";
        [Tooltip("How many of a variant's stats an element cell shows.")]
        [Min(0)] public int cellLines = 3;
        [Tooltip("A lifeform's elemental laws (a variant's \"Elemental law\" stat).")]
        public string lawsHeading = "Elemental laws";
        public string abilitiesHeading = "Abilities";
        [Tooltip("An ability's line on a vessel page. {upgrade} is its level-5 upgrade.")]
        public string abilityLineTemplate = "Level 5: {upgrade}";
        public string toolVariantsHeading = "What it offers";
        public string pilotsHeading = "Pilots";

        [Tooltip("A pilot's flavour line. {quote} is SO_Captain.Flavor without its quote marks.")]
        public string quoteTemplate = "“{quote}”";
        public string signatureLabel = "Signature";
        public string fliesLikeLabel = "Flies like";
        public string answersToLabel = "Answers to";
        [Tooltip("The Answers to value. {element} is the pilot's element, {ability} the hull's ability for it.")]
        public string answersToTemplate = "{element} - {ability}";
        public string upgradeLabel = "Level-5 upgrade";
    }

    /// <summary>
    /// Builds the Codex's chapters from the game's own data, as a pure function so the rules can be tested without a
    /// scene: the ethirions, flora, fauna and tools from <see cref="CodexSO"/> (Docs/CODEX.md, Garrett's), the pilots from
    /// the Hangar's <see cref="SO_CaptainList"/>, the vessels from its <see cref="SO_VesselList"/>, and every ability and
    /// level-5 upgrade from the hull's <see cref="ElementalAbilityMapSO"/>. Every entry ships unlocked (Docs/CODEX.md
    /// section 5: nothing reads discovery yet), so every entry is listed.
    /// </summary>
    public static class CodexCatalog
    {
        static readonly Element[] Four = { Element.Charge, Element.Mass, Element.Space, Element.Time };
        /// <summary>The harvester's label for a variant's elemental law (CodexHarvester), matched, never shown from here.</summary>
        const string ElementalLawLabel = "Elemental law";
        static readonly Regex GroupPrefix = new(@"^\s*\d+\s*·\s*", RegexOptions.Compiled);

        /// <summary>The element of a stop index 0..3, Charge to Time.</summary>
        public static Element ElementAt(int index) => index >= 0 && index < Four.Length ? Four[index] : Element.None;

        /// <summary>A tool category without its ordering prefix ("1 · Pilot" reads "Pilot").</summary>
        public static string GroupName(string group) => string.IsNullOrEmpty(group) ? string.Empty : GroupPrefix.Replace(group, string.Empty);

        public static List<CodexItem> Build(CodexChapter chapter, CodexSO codex, SO_CaptainList captains, SO_VesselList vessels,
                                            Sprite[] petals, CodexWords words, Func<VesselClassType, ElementalAbilityMapSO> maps = null)
        {
            maps ??= ElementalAbilityMapSO.LoadFor;
            words ??= new CodexWords();
            return chapter switch
            {
                CodexChapter.Ethirions => Entries(codex, codex ? codex.Ethirions : null, words.ethirionKicker, words, petals, false),
                CodexChapter.Flora => Entries(codex, codex ? codex.EntriesOf(CodexKingdom.Flora) : null, words.floraKicker, words, petals, true),
                CodexChapter.Fauna => Entries(codex, codex ? codex.EntriesOf(CodexKingdom.Fauna) : null, words.faunaKicker, words, petals, true),
                CodexChapter.Tools => Tools(codex, words),
                CodexChapter.Elements => Elements(vessels, petals, words, maps),
                CodexChapter.Pilots => Pilots(captains, vessels, petals, words, maps),
                CodexChapter.Vessels => Vessels(captains, vessels, petals, words, maps),
                _ => new List<CodexItem>(),   // Artifacts: no artifact data in the game yet
            };
        }

        // ------------------------------------------------------------------ ethirions, flora, fauna
        static List<CodexItem> Entries(CodexSO codex, List<CodexEntry> entries, string kicker, CodexWords words, Sprite[] petals,
                                       bool byElement)
        {
            var items = new List<CodexItem>();
            if (entries == null) return items;
            foreach (var e in entries)
            {
                if (e == null) continue;
                var page = new CodexPage { Image = e.Image, Kicker = kicker, Title = e.DisplayName ?? string.Empty, Body = BodyOf(e) };
                AddFacts(page, e.Stats, words);
                if (byElement)
                {
                    page.CellsHeading = words.cellsHeading;
                    for (int i = 0; i < Four.Length; i++)
                    {
                        var v = e.FindVariant(Four[i]);
                        if (v == null) continue;
                        page.Cells.Add(new CodexCell(codex ? codex.VariantImage(e, v) : v.Image, Name(words, Four[i]), CellLines(v, words)));
                        string law = LawOf(v);
                        if (string.IsNullOrEmpty(law)) continue;
                        page.RowsHeading = words.lawsHeading;
                        page.Rows.Add(new CodexRow(Petal(petals, i), Name(words, Four[i]), law));
                    }
                }
                items.Add(new CodexItem { Image = e.Image, Name = e.DisplayName ?? string.Empty, Sub = string.Empty, Page = page });
            }
            return items;
        }

        static List<CodexItem> Tools(CodexSO codex, CodexWords words)
        {
            var items = new List<CodexItem>();
            if (!codex) return items;
            foreach (var e in codex.Tools)
            {
                if (e == null) continue;
                string group = GroupName(e.Group);
                var page = new CodexPage { Image = e.Image, Kicker = TemplateText.Fill(words.toolKicker, "group", group),
                                           Title = e.DisplayName ?? string.Empty, Body = BodyOf(e) };
                AddFacts(page, e.Stats, words);
                if (e.Variants is { Count: > 0 })
                {
                    page.RowsHeading = words.toolVariantsHeading;
                    foreach (var v in e.Variants)
                        if (v != null)
                            page.Rows.Add(new CodexRow(codex.VariantImage(e, v), v.Label, v.Stats is { Count: > 0 } ? v.Stats[0].Value : string.Empty));
                }
                items.Add(new CodexItem { Image = e.Image, Name = e.DisplayName ?? string.Empty, Sub = group, Page = page });
            }
            return items;
        }

        // ------------------------------------------------------------------ elements
        static List<CodexItem> Elements(SO_VesselList vessels, Sprite[] petals, CodexWords words, Func<VesselClassType, ElementalAbilityMapSO> maps)
        {
            var items = new List<CodexItem>();
            for (int i = 0; i < Four.Length; i++)
            {
                var el = Four[i];
                string name = Name(words, el);
                var page = new CodexPage { Image = Petal(petals, i), Kicker = words.elementKicker, Title = name,
                                           Body = TemplateText.Fill(words.elementBody, "element", name),
                                           RowsHeading = words.elementRowsHeading };
                if (vessels && vessels.VesselList != null)
                    foreach (var vessel in vessels.VesselList)
                    {
                        if (!vessel) continue;
                        var entry = maps(vessel.Class)?.GetEntry(el);
                        string ability = entry == null || string.IsNullOrEmpty(entry.AbilityLabel) ? words.openSlot : entry.AbilityLabel;
                        string line = entry != null && !string.IsNullOrEmpty(entry.UpgradeLabel)
                            ? TemplateText.Fill(words.elementRowTemplate, "ability", ability, "upgrade", entry.UpgradeLabel)
                            : TemplateText.Fill(words.elementRowNoUpgrade, "ability", ability);
                        page.Rows.Add(new CodexRow(vessel.IconActive, vessel.Name, line));
                    }
                string sub = i < (words.elementLines?.Length ?? 0) ? words.elementLines[i] : string.Empty;
                items.Add(new CodexItem { Image = Petal(petals, i), Name = name, Sub = sub, Page = page });
            }
            return items;
        }

        // ------------------------------------------------------------------ pilots
        /// <summary>Pilots in the vessel list's order, then Charge to Time. A pilot whose hull is not on the list is left out.</summary>
        public static List<SO_Captain> OrderedPilots(SO_CaptainList captains, SO_VesselList vessels)
        {
            var result = new List<SO_Captain>();
            if (!captains || captains.CaptainList == null) return result;
            var order = new Dictionary<VesselClassType, int>();
            if (vessels && vessels.VesselList != null)
                for (int i = 0; i < vessels.VesselList.Count; i++)
                    if (vessels.VesselList[i] && !order.ContainsKey(vessels.VesselList[i].Class)) order[vessels.VesselList[i].Class] = i;
            foreach (var c in captains.CaptainList)
                if (c && c.Vessel && order.ContainsKey(c.Vessel.Class)) result.Add(c);
            result.Sort((a, b) =>
            {
                int byHull = order[a.Vessel.Class].CompareTo(order[b.Vessel.Class]);
                return byHull != 0 ? byHull : ((int)a.PrimaryElement).CompareTo((int)b.PrimaryElement);
            });
            return result;
        }

        static List<CodexItem> Pilots(SO_CaptainList captains, SO_VesselList vessels, Sprite[] petals, CodexWords words,
                                      Func<VesselClassType, ElementalAbilityMapSO> maps)
        {
            var items = new List<CodexItem>();
            foreach (var p in OrderedPilots(captains, vessels))
            {
                string el = Name(words, p.PrimaryElement), hull = p.Vessel.Name;
                var page = new CodexPage
                {
                    Image = p.Image,
                    Kicker = TemplateText.Fill(words.pilotKicker, "element", el, "vessel", hull),
                    Title = p.Name ?? string.Empty,
                    Body = string.IsNullOrEmpty(p.Flavor) ? string.Empty : TemplateText.Fill(words.quoteTemplate, "quote", p.Flavor.Trim('“', '”', '"', '\'')),
                    FactsHeading = words.factsHeading,
                };
                AddFact(page, words.signatureLabel, p.Description);
                AddFact(page, words.fliesLikeLabel, p.AIBehaviorDescription);
                var entry = maps(p.Vessel.Class)?.GetEntry(p.PrimaryElement);
                if (entry != null && !string.IsNullOrEmpty(entry.AbilityLabel))
                    AddFact(page, words.answersToLabel, TemplateText.Fill(words.answersToTemplate, "element", el, "ability", entry.AbilityLabel));
                if (entry != null) AddFact(page, words.upgradeLabel, entry.UpgradeLabel);
                items.Add(new CodexItem { Image = p.Image, Name = p.Name ?? string.Empty, Sub = el, Page = page });
            }
            return items;
        }

        // ------------------------------------------------------------------ vessels
        static List<CodexItem> Vessels(SO_CaptainList captains, SO_VesselList vessels, Sprite[] petals, CodexWords words,
                                       Func<VesselClassType, ElementalAbilityMapSO> maps)
        {
            var items = new List<CodexItem>();
            if (!vessels || vessels.VesselList == null) return items;
            var pilots = OrderedPilots(captains, vessels);
            foreach (var v in vessels.VesselList)
            {
                if (!v) continue;
                var page = new CodexPage { Image = v.IconActive, Kicker = words.vesselKicker, Title = v.Name ?? string.Empty,
                                           Body = v.Description ?? string.Empty, RowsHeading = words.abilitiesHeading };
                var map = maps(v.Class);
                for (int i = 0; i < Four.Length; i++)
                {
                    var entry = map ? map.GetEntry(Four[i]) : null;
                    string ability = entry == null || string.IsNullOrEmpty(entry.AbilityLabel) ? words.openSlot : entry.AbilityLabel;
                    string line = entry != null && !string.IsNullOrEmpty(entry.UpgradeLabel)
                        ? TemplateText.Fill(words.abilityLineTemplate, "upgrade", entry.UpgradeLabel) : string.Empty;
                    page.Rows.Add(new CodexRow(Petal(petals, i), ability, line));
                }
                foreach (var p in pilots)
                    if (p.Vessel.Class == v.Class)
                    {
                        page.CellsHeading = words.pilotsHeading;
                        page.Cells.Add(new CodexCell(p.Image, p.Name, Name(words, p.PrimaryElement)));
                    }
                items.Add(new CodexItem { Image = v.IconActive, Name = v.Name ?? string.Empty, Sub = string.Empty, Page = page });
            }
            return items;
        }

        // ------------------------------------------------------------------ helpers
        static string BodyOf(CodexEntry e)
        {
            bool tag = !string.IsNullOrWhiteSpace(e.Tagline), desc = !string.IsNullOrWhiteSpace(e.Description);
            if (tag && desc) return e.Tagline.Trim() + "\n\n" + e.Description.Trim();
            return tag ? e.Tagline.Trim() : desc ? e.Description.Trim() : string.Empty;
        }

        static void AddFacts(CodexPage page, List<CodexStat> stats, CodexWords words)
        {
            if (stats == null) return;
            foreach (var s in stats)
                if (!s.IsEmpty) AddFact(page, s.Label, s.Value);
            if (page.Facts.Count > 0) page.FactsHeading = words.factsHeading;
        }

        static void AddFact(CodexPage page, string label, string value)
        {
            if (!string.IsNullOrWhiteSpace(value)) page.Facts.Add(new CodexFact(label ?? string.Empty, value.Trim()));
        }

        static string LawOf(CodexVariant v)
        {
            if (v.Stats != null)
                foreach (var s in v.Stats)
                    if (s.Label == ElementalLawLabel && !string.IsNullOrWhiteSpace(s.Value)) return s.Value.Trim();
            return string.Empty;
        }

        static string CellLines(CodexVariant v, CodexWords words)
        {
            if (v.Stats == null || words.cellLines <= 0) return string.Empty;
            var lines = new List<string>(words.cellLines);
            foreach (var s in v.Stats)
            {
                // A law is a sentence, not a number: it reads in the page's rows, never squeezed into a cell.
                if (s.IsEmpty || s.Label == ElementalLawLabel) continue;
                lines.Add(TemplateText.Fill(words.cellLineTemplate, "label", s.Label, "value", s.Value));
                if (lines.Count >= words.cellLines) break;
            }
            return string.Join("\n", lines);
        }

        static string Name(CodexWords words, Element el)
        {
            int i = Array.IndexOf(Four, el);
            return i >= 0 && words.elementNames != null && i < words.elementNames.Length ? words.elementNames[i] : el.ToString();
        }

        static Sprite Petal(Sprite[] petals, int i) => petals != null && i >= 0 && i < petals.Length ? petals[i] : null;
    }
}
