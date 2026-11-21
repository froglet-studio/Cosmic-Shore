using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>The Codex's stops, in LB / RB order (Docs/MENU_REDESIGN_B.md sections 88 and 97). Flora and Fauna are
    /// the two stops of the ECOLOGY chapter. Values index nothing serialized but the authored names, so they stay explicit.
    /// Section 90 (Shombith 2026-10-27): the etherions folded into ELEMENTS, and the pilots, vessels, artifacts and toys
    /// drill-downs became one LEXICON of cards.</summary>
    public enum CodexChapter
    {
        Elements = 0,   // the four elements, then Omni, prisms and spindles
        Flora = 1,
        Fauna = 2,
        Lexicon = 3,    // one card each: vessels, pilots, artifacts, toys
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
        /// <summary>The picture's live motion: a 4x4 sheet of frames (a crystal's element effect). Null: the still.</summary>
        public Texture2D Flipbook;
        /// <summary>The element this page is about: its selection motion and its shape badge. None: neither.</summary>
        public Element Element = Element.None;
        /// <summary>Colours the picture drifts through, slowly (a lifeform: the domains it is seen in). Null: none.</summary>
        public Color[] Palette;
        public string Kicker = string.Empty;
        public string Title = string.Empty;
        /// <summary>What it does in play: read first, right under the title (section 97: "gameplay info above lore").</summary>
        public string Gameplay = string.Empty;
        /// <summary>Its lore: read last, under every block of facts.</summary>
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
        public Texture2D Flipbook;
        public Element Element = Element.None;
        public Color[] Palette;
        public string Name = string.Empty;
        public string Sub = string.Empty;
        public CodexPage Page;
    }

    /// <summary>
    /// A card the Codex shows whole, from authored words alone: Omni, prisms and spindles in ELEMENTS, and each card of
    /// the LEXICON (vessels, pilots, artifacts, toys - section 97: "keep them as higher-level lexicon cards").
    /// </summary>
    [Serializable]
    public sealed class CodexCard
    {
        public string title = string.Empty;
        [Tooltip("The quiet line under the name on its tile. Empty: none.")]
        public string sub = string.Empty;
        public string kicker = string.Empty;
        [Tooltip("What it does in play: read first, under the title.")]
        [TextArea(2, 6)] public string gameplay = string.Empty;
        [Tooltip("Its lore: read last. Empty: none.")]
        [TextArea(2, 6)] public string lore = string.Empty;
        public Sprite image;
        [Tooltip("The picture's live motion, a 4x4 sheet of frames. Optional.")]
        public Texture2D flipbook;
    }

    /// <summary>
    /// Every word the Codex adds around the game's own data (CLAUDE.md: all player-facing text is authored). The data
    /// itself - a species' stats, an ability's name, a Toy Box species' description - is already authored on its own asset.
    /// </summary>
    [Serializable]
    public sealed class CodexWords
    {
        [Tooltip("One name per element, Charge to Time.")]
        public string[] elementNames = { "Charge", "Mass", "Space", "Time" };
        [Tooltip("What each element is for, in a line, Charge to Time.")]
        public string[] elementLines = { "The strike you carry", "What you build", "How far you reach", "How fast you move" };
        [Tooltip("The element page's gameplay, read first. {element} is its name.")]
        public string elementGameplay = "Skim {element} Etherions or fly the Element Charger to raise it. Its ability wakes at level 5, and so does the hull's level-5 upgrade.";
        [Tooltip("Each element's lore, Charge to Time, read last. Empty: none.")]
        public string[] elementLore = { "", "", "", "" };
        public string elementKicker = "Element";
        [Tooltip("The element page's list of every hull's ability.")]
        public string elementRowsHeading = "On every vessel";
        [Tooltip("A vessel's line on an element page. {ability} and {upgrade} are from the hull's ability map.")]
        public string elementRowTemplate = "{ability} - level 5: {upgrade}";
        [Tooltip("A vessel's line when its ability has no level-5 upgrade. {ability} is the ability.")]
        public string elementRowNoUpgrade = "{ability}";
        [Tooltip("The ability name for an element no ability is designed for yet.")]
        public string openSlot = "Open slot";
        [Tooltip("ELEMENTS after the four: Omni, prisms, spindles.")]
        public List<CodexCard> elementCards = new();

        public string floraKicker = "Flora";
        public string faunaKicker = "Fauna";
        public string factsHeading = "Facts";
        [Tooltip("The lifeform page's four element cells: its heart is that element's Etherion.")]
        public string cellsHeading = "By element";
        [Tooltip("A line in an element cell. {label} and {value} are the variant's stat.")]
        public string cellLineTemplate = "{label}: {value}";
        [Tooltip("How many of a variant's stats an element cell shows.")]
        [Min(0)] public int cellLines = 3;
        [Tooltip("A lifeform's elemental laws (a variant's \"Elemental law\" stat).")]
        public string lawsHeading = "Elemental laws";
        [Tooltip("The heading over a page's lore. Empty: none.")]
        public string loreHeading = "Lore";

        [Tooltip("The LEXICON: one card per idea (vessels, pilots, artifacts, toys).")]
        public List<CodexCard> lexicon = new();
    }

    /// <summary>
    /// Builds the Codex's chapters from the game's own data, as a pure function so the rules can be tested without a
    /// scene (Docs/MENU_REDESIGN_B.md section 97):
    /// <list type="bullet">
    /// <item>ELEMENTS - the four elements, each with its Etherion crystal and every hull's ability for it (from the hull's
    /// <see cref="ElementalAbilityMapSO"/>), then the authored cards (Omni, prisms, spindles);</item>
    /// <item>FLORA and FAUNA - every species the Toy Box's Spawn Matrix offers, in its order, each with its Toy Box
    /// description as its gameplay line and, where <see cref="CodexSO"/> (Docs/CODEX.md, Garrett's) has the species, its
    /// picture, facts and elemental laws; species only the Codex knows follow;</item>
    /// <item>LEXICON - the authored cards.</item>
    /// </list>
    /// Every entry ships unlocked (Docs/CODEX.md section 5: nothing reads discovery yet), so every entry is listed.
    /// </summary>
    public static class CodexCatalog
    {
        static readonly Element[] Four = { Element.Charge, Element.Mass, Element.Space, Element.Time };
        /// <summary>The harvester's label for a variant's elemental law (CodexHarvester), matched, never shown from here.</summary>
        const string ElementalLawLabel = "Elemental law";

        /// <summary>The element of a stop index 0..3, Charge to Time.</summary>
        public static Element ElementAt(int index) => index >= 0 && index < Four.Length ? Four[index] : Element.None;

        /// <param name="petals">The element icons, Charge to Time: the HUD's element shapes (an elemental law's icon).</param>
        /// <param name="roster">The Toy Box's Spawn Matrix: the species list Flora and Fauna follow. Optional.</param>
        /// <param name="elementFlipbooks">Each element's crystal in motion, Charge to Time. Optional.</param>
        /// <param name="lifePalette">The colours a lifeform's picture drifts through (the domains). Optional.</param>
        public static List<CodexItem> Build(CodexChapter chapter, CodexSO codex, SO_VesselList vessels, Sprite[] petals,
                                            CodexWords words, Func<VesselClassType, ElementalAbilityMapSO> maps = null,
                                            SpawnMatrixToyDefinitionSO roster = null, Texture2D[] elementFlipbooks = null,
                                            Color[] lifePalette = null)
        {
            maps ??= ElementalAbilityMapSO.LoadFor;
            words ??= new CodexWords();
            var items = chapter switch
            {
                CodexChapter.Elements => Elements(codex, vessels, words, maps, elementFlipbooks),
                CodexChapter.Flora => Lifeforms(codex, CodexKingdom.Flora, Names(roster ? roster.Flora : null, f => f?.Name), roster,
                                                words.floraKicker, words, petals),
                CodexChapter.Fauna => Lifeforms(codex, CodexKingdom.Fauna, Names(roster ? roster.Fauna : null, f => f?.Name), roster,
                                                words.faunaKicker, words, petals),
                CodexChapter.Lexicon => Cards(words.lexicon, words),
                _ => new List<CodexItem>(),
            };
            // A lifeform is nobody's on its page, and in the world it wears its owner's colour: the picture drifts through
            // the domains (Shombith 2026-10-26: "ecology need colors"). Elements stay colourless (the palette rule).
            if (lifePalette is { Length: > 0 } && chapter is CodexChapter.Flora or CodexChapter.Fauna)
                foreach (var item in items)
                    item.Palette = item.Page.Palette = lifePalette;
            return items;
        }

        // ------------------------------------------------------------------ elements
        static List<CodexItem> Elements(CodexSO codex, SO_VesselList vessels, CodexWords words,
                                        Func<VesselClassType, ElementalAbilityMapSO> maps, Texture2D[] flipbooks)
        {
            var items = new List<CodexItem>();
            for (int i = 0; i < Four.Length; i++)
            {
                var el = Four[i];
                string name = Name(words, el);
                // The element and its Etherion are one entry (section 97): the crystal is its picture, and none of the
                // harvested crystal stats (triangle counts, heart sizes) is shown - they were build notes, not play.
                var crystal = codex ? codex.EthirionFor(el) : null;
                var flip = flipbooks != null && i < flipbooks.Length ? flipbooks[i] : null;
                var page = new CodexPage
                {
                    Image = crystal?.Image, Flipbook = flip, Element = el, Kicker = words.elementKicker, Title = name,
                    Gameplay = TemplateText.Fill(words.elementGameplay, "element", name),
                    Body = i < (words.elementLore?.Length ?? 0) ? words.elementLore[i] ?? string.Empty : string.Empty,
                    RowsHeading = words.elementRowsHeading,
                };
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
                items.Add(new CodexItem { Image = page.Image, Flipbook = flip, Element = el, Name = name, Sub = sub, Page = page });
            }
            items.AddRange(Cards(words.elementCards, words));
            return items;
        }

        // ------------------------------------------------------------------ flora and fauna
        static List<string> Names<T>(T[] species, Func<T, string> name)
        {
            var names = new List<string>();
            if (species == null) return names;
            foreach (var s in species)
            {
                string n = name(s);
                if (!string.IsNullOrWhiteSpace(n) && !names.Contains(n)) names.Add(n);
            }
            return names;
        }

        /// <summary>"Worm Colony", "worm-colony" and "WormColony" are one species.</summary>
        static string Key(string name) => string.IsNullOrEmpty(name) ? string.Empty
            : Regex.Replace(name, @"[\s\-_]", string.Empty).ToLowerInvariant();

        static List<CodexItem> Lifeforms(CodexSO codex, CodexKingdom kingdom, List<string> rosterNames, SpawnMatrixToyDefinitionSO roster,
                                         string kicker, CodexWords words, Sprite[] petals)
        {
            var known = codex ? codex.EntriesOf(kingdom) : new List<CodexEntry>();
            var byKey = new Dictionary<string, CodexEntry>();
            foreach (var e in known)
                if (e != null && !byKey.ContainsKey(Key(e.DisplayName))) byKey[Key(e.DisplayName)] = e;

            var items = new List<CodexItem>();
            var used = new HashSet<CodexEntry>();
            // The Toy Box's list first, in its order: the Codex lists every species a player can release.
            foreach (var name in rosterNames)
            {
                byKey.TryGetValue(Key(name), out var entry);
                if (entry != null) used.Add(entry);
                items.Add(Lifeform(codex, entry, name, roster ? roster.DescriptionOf(name) : string.Empty, kicker, words, petals));
            }
            foreach (var e in known)
                if (e != null && !used.Contains(e))
                    items.Add(Lifeform(codex, e, e.DisplayName, string.Empty, kicker, words, petals));
            return items;
        }

        static CodexItem Lifeform(CodexSO codex, CodexEntry e, string name, string gameplay, string kicker, CodexWords words, Sprite[] petals)
        {
            var page = new CodexPage { Image = e?.Image, Kicker = kicker, Title = name ?? string.Empty,
                                       Gameplay = gameplay?.Trim() ?? string.Empty, Body = e != null ? BodyOf(e) : string.Empty };
            if (e != null)
            {
                AddFacts(page, e.Stats, words);
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
            return new CodexItem { Image = page.Image, Name = page.Title, Sub = string.Empty, Page = page };
        }

        // ------------------------------------------------------------------ authored cards
        static List<CodexItem> Cards(List<CodexCard> cards, CodexWords words)
        {
            var items = new List<CodexItem>();
            if (cards == null) return items;
            foreach (var c in cards)
            {
                if (c == null || string.IsNullOrWhiteSpace(c.title)) continue;
                var page = new CodexPage { Image = c.image, Flipbook = c.flipbook, Kicker = c.kicker ?? string.Empty, Title = c.title,
                                           Gameplay = c.gameplay ?? string.Empty, Body = c.lore ?? string.Empty };
                items.Add(new CodexItem { Image = c.image, Flipbook = c.flipbook, Name = c.title, Sub = c.sub ?? string.Empty, Page = page });
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
