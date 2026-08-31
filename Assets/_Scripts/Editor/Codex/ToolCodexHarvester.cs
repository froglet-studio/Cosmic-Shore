using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor.Codex
{
    /// <summary>
    /// Reads the project's <b>Tools</b> - the freestyle toys - and produces one codex entry per
    /// toy. The sibling of <see cref="CodexHarvester"/>'s ethirion and ecology passes; it merges
    /// through the same <c>MergeList</c> under the same field-ownership contract, so a re-scan is
    /// as safe here as it is anywhere else in the codex.
    ///
    /// <para><b>A tool has no prefab, and that is the whole difference.</b> A crystal and a
    /// creature are authored objects the scan can photograph; a toy is built at runtime by
    /// <see cref="ToyFactory"/> from its <see cref="ToyDefinitionSO"/>, so the DEFINITION is the
    /// asset that exists. Entries therefore carry <see cref="CodexEntry.SourceConfig"/> instead of
    /// <see cref="CodexEntry.SourcePrefab"/>, and their portrait is drawn rather than harvested
    /// (<see cref="ToolPortraitBuilder"/>).</para>
    ///
    /// <para><b>Facts are read per TYPE, by pattern match, not by name.</b> Two things follow from
    /// that and both are deliberate. A field rename is a compile error here rather than a silently
    /// dropped row - the opposite trade from the ecology probes, and the right one, because a toy
    /// definition is a handful of assets the editor assembly can already see. And a toy KIND with
    /// no case below is reported as a warning instead of harvested as an empty page: adding a toy
    /// without teaching the codex what it offers should be noisy.</para>
    /// </summary>
    public static class ToolCodexHarvester
    {
        /// <summary>
        /// One entry per <see cref="ToyDefinitionSO"/> in the project, whether or not it is in the
        /// shipped toybox - the encyclopedia describes what EXISTS, and "authored but not in your
        /// toybox" is itself a fact worth stating rather than a reason to hide a page.
        /// </summary>
        public static List<CodexEntry> BuildToolEntries(CodexHarvestReport report)
        {
            var definitions = CodexHarvester.LoadAll<ToyDefinitionSO>();
            var toybox = Resources.Load<ToyboxSO>("Toybox");

            if (definitions.Count == 0)
                report.Warnings.Add("No ToyDefinitionSO assets found — the Tools kingdom is empty.");
            if (!toybox)
                report.Warnings.Add(
                    "No Resources/Toybox asset — every tool will read as \"not in the toybox\". " +
                    "The runtime falls back to a code-built default toybox, so this is a codex " +
                    "gap rather than a broken game.");

            var inToybox = new HashSet<ToyDefinitionSO>();
            if (toybox)
                foreach (var t in toybox.Toys)
                    if (t) inToybox.Add(t);

            var entries = new List<CodexEntry>(definitions.Count);
            foreach (var definition in definitions)
                entries.Add(BuildEntry(definition, inToybox.Contains(definition), report));

            // Category first, then name. The window sorts the same way off CodexEntry.Group, so
            // this only decides the order NEW entries are appended in - but a codex asset whose
            // raw list order already matches what it draws is a much easier one to read in the
            // inspector or in a diff.
            entries.Sort((a, b) =>
            {
                int byGroup = string.Compare(a.Group, b.Group, System.StringComparison.Ordinal);
                return byGroup != 0
                    ? byGroup
                    : string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.Ordinal);
            });
            return entries;
        }

        static CodexEntry BuildEntry(ToyDefinitionSO definition, bool inToybox,
            CodexHarvestReport report)
        {
            var name = string.IsNullOrWhiteSpace(definition.DisplayName)
                ? definition.name
                : definition.DisplayName;

            // Keyed on the toy's own stable id, not its display name: renaming what a toy is
            // CALLED is an ordinary edit, and it must not orphan the page somebody wrote for it.
            var entry = CodexHarvester.NewEntry(CodexKingdom.Tool, name, definition.Id);
            entry.Group = GroupFor(definition.Category);
            entry.SourceConfig = definition;
            entry.DiscoveryKey = $"use.tool.{CodexHarvester.Slug(definition.Id)}";
            entry.Tagline = definition.Description;

            // The toy's authored accent IS its colour in the world - the sphere, the ring and the
            // label all wear it - so the page wears the same one. Alpha 0 stays the "unset"
            // sentinel, so a toy that authored no colour still falls through to the kingdom's.
            if (definition.AccentColor.a > 0f) entry.AccentColor = definition.AccentColor;

            CodexHarvester.Add(entry.Stats, "Kind", CategoryLine(definition.Category));
            CodexHarvester.Add(entry.Stats, "Activation",
                "Fly your vessel through its ring — the ring IS the trigger volume, drawn at its " +
                "own radius, so what you can see is what you can cross");
            CodexHarvester.Add(entry.Stats, "Objective",
                "None. A tool has no score, no end condition and nothing on a clock — it is a " +
                "thing to play with for as long as you like");
            CodexHarvester.Add(entry.Stats, "In your toybox", inToybox
                ? "Yes" + (definition.UnlockedByDefault ? " — unlocked from the start" : " — locked until earned")
                : "No — authored, but not in the shipped toybox");
            CodexHarvester.Add(entry.Stats, "Found at", PlacementLine(definition.PlacementAngleDegrees));

            AddKindFacts(entry, definition, report);
            return entry;
        }

        // ── Per-kind facts ───────────────────────────────────────────────────────

        /// <summary>
        /// The rows and variants only this KIND of tool can state. The switch is exhaustive over
        /// the shipped toys and its default arm WARNS: a new toy that reaches it gets a page with
        /// the shared rows and no content, which is the visible symptom this method exists to make
        /// impossible to ship quietly.
        /// </summary>
        static void AddKindFacts(CodexEntry entry, ToyDefinitionSO definition,
            CodexHarvestReport report)
        {
            switch (definition)
            {
                case VesselChangerToyDefinitionSO vessels: AddVesselChanger(entry, vessels); return;
                case DomainChangerToyDefinitionSO: AddDomainChanger(entry); return;
                case CellSelectorToyDefinitionSO cells: AddCellSelector(entry, cells); return;
                case ConveyorToyDefinitionSO conveyor: AddConveyor(entry, conveyor); return;
                case PaintingToyDefinitionSO paintings: AddPaintingGallery(entry, paintings); return;
                case LifeformMatrixToyDefinitionSO bench: AddLifeformMatrix(entry, bench); return;

                default:
                    report.Warnings.Add(
                        $"'{entry.DisplayName}' is a {definition.GetType().Name}, which " +
                        "ToolCodexHarvester has no case for — its page carries only the rows every " +
                        "tool shares. Add a case so it can state what it offers.");
                    return;
            }
        }

        static void AddVesselChanger(CodexEntry entry, VesselChangerToyDefinitionSO definition)
        {
            var hulls = new List<VesselClassType>();
            ToyVesselRoster.Resolve(definition.VesselCollection, hulls);

            CodexHarvester.Add(entry.Stats, "Form",
                "One station that opens into a matrix — fly it and the hulls bloom out ahead");
            CodexHarvester.Add(entry.Stats, "Offers", hulls.Count > 1
                ? $"{hulls.Count} hulls — {hulls.Count - 1} of them at a time, since a matrix never " +
                  "offers you the one you are already flying"
                : Count(hulls.Count, "hull"));
            CodexHarvester.Add(entry.Stats, "Roster", definition.VesselCollection is { Length: > 0 }
                ? "Authored on the tool"
                : "The shared curated roster — the fleet minus the classes with no ship yet");
            CodexHarvester.Add(entry.Stats, "What it keeps",
                "Your domain, your pose and your speed. Only the hull changes");

            var container = CodexHarvester.LoadAll<VesselPrefabContainer>().FirstOrDefault();
            foreach (var hull in hulls)
            {
                var variant = Variant(hull.ToString(), "Swap into this class");
                // The real ship prefab, so the icon is the hull rather than a label. Resolved from
                // the same container the spawn pipeline uses - a second roster here would be a
                // second thing to keep in step with the fleet.
                if (container && container.TryGetShipPrefab(hull, out var prefab) && prefab)
                    variant.SourcePrefab = prefab.gameObject;
                entry.Variants.Add(variant);
            }
        }

        static void AddDomainChanger(CodexEntry entry)
        {
            CodexHarvester.Add(entry.Stats, "Form",
                "A flip-set — one station per colour you are NOT, and using one flips it to the " +
                "colour you just left");
            CodexHarvester.Add(entry.Stats, "Offers",
                $"{PlayableDomains.Length} domains, {PlayableDomains.Length - 1} stations — " +
                "the colours you are not");
            CodexHarvester.Add(entry.Stats, "Authority",
                "Server-side. The change replicates to every peer, so the whole party sees it");
            CodexHarvester.Add(entry.Stats, "What it does not change",
                "Mass you have already laid. A prism keeps the colour it was laid in, so changing " +
                "domain leaves your old trail behind as another team's");

            foreach (var domain in PlayableDomains)
            {
                var variant = Variant(domain.ToString(), "Fly this colour");
                // No image is baked for these, deliberately: the variant IS a colour, and a PNG
                // of a flat fill is a file that says nothing a swatch does not.
                variant.AccentColor = ToyFactory.DomainAccentColor(domain);
                entry.Variants.Add(variant);
            }
        }

        /// <summary>Blue is the neutral / no-team sentinel and is never a pick.</summary>
        static readonly Domains[] PlayableDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };

        static void AddCellSelector(CodexEntry entry, CellSelectorToyDefinitionSO definition)
        {
            CodexHarvester.Add(entry.Stats, "Form",
                "One station that opens into a matrix of mini-cells — each a real scale model of " +
                "the world it builds, sampled from that world's own generator");
            CodexHarvester.Add(entry.Stats, "Offers", definition.Cells.Count > 0
                ? Count(definition.Cells.Count, "world")
                : "Every world the cell you are in can be — the tool reads the cell's own list " +
                  "rather than keeping a second one");
            CodexHarvester.Add(entry.Stats, "The swap",
                "The old world suctions away and the chosen one grows back behind a veil. " +
                "Nothing pops in or out");
            CodexHarvester.Add(entry.Stats, "Also the reset", definition.ClearLooseTrailMass
                ? "Pick the world you are already in and you get the same cycle on the same " +
                  "config — the accumulated freestyle trail goes with it"
                : "Pick the world you are already in to rebuild it; loose trail mass is kept");

            foreach (var cell in definition.Cells)
            {
                if (!cell) continue;
                var variant = Variant(
                    string.IsNullOrWhiteSpace(cell.CellName) ? cell.name : cell.CellName,
                    "Become this world");
                variant.SourceConfig = cell;
                entry.Variants.Add(variant);
            }
        }

        static void AddConveyor(CodexEntry entry, ConveyorToyDefinitionSO definition)
        {
            // Read by NAME here, unlike the rest of this file: the conveyor is the one definition
            // that exposes no public accessors, and adding some purely so an editor tool can read
            // them would be coupling the runtime to the codex rather than the other way round.
            var config = new SerializedObject(definition);
            int pool = Int(config, "poolSize");
            int perScene = Int(config, "prismBudgetPerScene");
            int tether = Int(config, "tetherPrisms");
            bool lifeforms = Bool(config, "lifeformScenes");

            CodexHarvester.Add(entry.Stats, "Form",
                "A run you leave for and come back from — not a matrix. Fly it once to go, and " +
                "the return station rides the end of your own trail");
            CodexHarvester.Add(entry.Stats, "What happens",
                "The cell is handed its bare canvas and a field of little worlds streams ahead of " +
                "you, wherever you fly, for as long as you fly");
            if (pool > 0 && perScene > 0)
                CodexHarvester.Add(entry.Stats, "The belt",
                    $"{pool} scenes of {perScene:N0} prisms — {pool * perScene:N0} in all, built " +
                    "once and then transported forever. The scene farthest behind you is the one " +
                    "that blooms ahead");
            CodexHarvester.Add(entry.Stats, "Tether",
                tether > 0
                    ? $"{tether:N0} prisms. Your trail follows you rather than accumulating, and " +
                      "the way home is always one tether-length behind"
                    : null);
            CodexHarvester.Add(entry.Stats, "You will find",
                lifeforms
                    ? "Structures, skimmable crystals, and flora and fauna released into the cell " +
                      "as ordinary citizens"
                    : "Structures and skimmable crystals");
            CodexHarvester.Add(entry.Stats, "Getting back",
                "Three ways, all the same thing: the return station, another pass through the " +
                "tool, or leaving freestyle");
        }

        static void AddPaintingGallery(CodexEntry entry, PaintingToyDefinitionSO definition)
        {
            var paintings = AuthoredPaintings(definition);

            CodexHarvester.Add(entry.Stats, "Form",
                "One station that opens into a gallery — fly a painting to start it");
            CodexHarvester.Add(entry.Stats, "Offers", paintings.Count > 0
                ? Count(paintings.Count, "painting")
                : $"{PaintingToyDefinitionSO.DefaultGalleryCatalog.Length} paintings — the " +
                  "built-in gallery, since none are authored on the tool");
            CodexHarvester.Add(entry.Stats, "How you paint",
                "Your own trail is the brush. Fly the dots in order; the gates recolour your " +
                "trail between strokes and lift the pen between them");
            CodexHarvester.Add(entry.Stats, "The marks",
                "Cones point at where you go next and mean the trail is ON; jacks end a stroke " +
                "and mean it is OFF");
            CodexHarvester.Add(entry.Stats, "Progress",
                "Saved. A half-finished painting survives a vessel swap, a game and a session — " +
                "the prisms you already drew grow back with it");
            CodexHarvester.Add(entry.Stats, "When it is done",
                "Share it as a page you can spin, or repaint it");

            foreach (var painting in paintings)
            {
                if (!painting) continue;
                var variant = Variant(
                    string.IsNullOrWhiteSpace(painting.DisplayName) ? painting.name : painting.DisplayName,
                    painting.Description);
                variant.SourceConfig = painting;
                entry.Variants.Add(variant);
            }
        }

        static void AddLifeformMatrix(CodexEntry entry, LifeformMatrixToyDefinitionSO definition)
        {
            int fauna = definition.Fauna?.Count(s => s != null) ?? 0;
            int flora = definition.Flora?.Count(s => s != null) ?? 0;

            var hulls = new List<VesselClassType>();
            ToyVesselRoster.Resolve(definition.VesselRoster, hulls);

            CodexHarvester.Add(entry.Stats, "Form",
                "One station that opens into three kingdoms; a kingdom opens its species, and a " +
                "species opens its four elements");
            CodexHarvester.Add(entry.Stats, "Offers",
                $"{fauna + flora} species and {hulls.Count} hulls, across three kingdoms");
            CodexHarvester.Add(entry.Stats, "What it releases",
                "The real thing, live into the cell around you — not a preview");
            CodexHarvester.Add(entry.Stats, "Afterwards",
                "Everything released is an ordinary citizen: lifeforms feed, starve, breed and " +
                "drop their crystal; a companion vessel flies and lays mass the food web grazes");
            CodexHarvester.Add(entry.Stats, "Elements",
                "A lifeform is its species and its element and nothing else — the four stations " +
                "wear the element's own crystal, because elements have shapes and domains have colours");

            entry.Variants.Add(Variant("Fauna", fauna > 0
                ? $"{Count(fauna, "species")} of creature to release"
                : "No species wired on this bench"));
            entry.Variants.Add(Variant("Flora", flora > 0
                ? $"{Count(flora, "species")} of plant to release"
                : "No species wired on this bench"));
            entry.Variants.Add(Variant("Vessels", hulls.Count > 0
                ? $"{Count(hulls.Count, "hull")} — each releases an AI companion in your own domain"
                : "No hulls offered"));
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        static List<PaintingDefinitionSO> AuthoredPaintings(PaintingToyDefinitionSO definition)
        {
            // Deliberately NOT ResolvePaintings(): that mints runtime SOs for the default gallery
            // and resolves every stroke, which is a lot of geometry to generate for a stat row -
            // and leaks the SOs unless the caller destroys them. The authored list is the fact;
            // the fallback is stated as a number from the catalog instead.
            var field = new SerializedObject(definition).FindProperty("paintings");
            var result = new List<PaintingDefinitionSO>();
            if (field == null || !field.isArray) return result;

            for (int i = 0; i < field.arraySize; i++)
                if (field.GetArrayElementAtIndex(i).objectReferenceValue is PaintingDefinitionSO p)
                    result.Add(p);
            return result;
        }

        static CodexVariant Variant(string label, string summary)
        {
            var variant = new CodexVariant { Label = label, Element = Element.None };
            CodexHarvester.Add(variant.Stats, "What it does", summary);
            return variant;
        }

        /// <summary>
        /// The sub-heading this tool files under. Numbered so the sections order themselves
        /// PILOT → WORLD → CREATION - lightest touch to heaviest, which is also the order a player
        /// meets them in - rather than alphabetically, which would open on Creation.
        /// </summary>
        static string GroupFor(ToyCategory category) => category switch
        {
            ToyCategory.Pilot => "1 · Pilot",
            ToyCategory.World => "2 · World",
            ToyCategory.Creation => "3 · Creation",
            _ => "9 · " + category,
        };

        static string CategoryLine(ToyCategory category) => category switch
        {
            ToyCategory.Pilot =>
                "Pilot — it changes YOU. The hull you fly or the colours you wear; the world is " +
                "exactly where you left it",
            ToyCategory.World =>
                "World — it changes WHERE YOU ARE. A world arrives or leaves, which is the " +
                "heaviest thing any tool does",
            ToyCategory.Creation =>
                "Creation — it LEAVES SOMETHING BEHIND that lives on without you: conserved mass, " +
                "or a population",
            _ => category.ToString(),
        };

        static string PlacementLine(float angleDegrees) => angleDegrees < 0f
            ? "Out by the cell membrane, spaced evenly with the other tools"
            : $"Out by the cell membrane, at {angleDegrees:0}° around the cell";

        static string Count(int value, string noun) =>
            value > 0 ? $"{value} {noun}{(value == 1 ? "" : "s")}" : null;

        static int Int(SerializedObject config, string field)
        {
            var prop = config.FindProperty(field);
            return prop != null && prop.propertyType == SerializedPropertyType.Integer
                ? prop.intValue : 0;
        }

        static bool Bool(SerializedObject config, string field)
        {
            var prop = config.FindProperty(field);
            return prop != null && prop.propertyType == SerializedPropertyType.Boolean &&
                   prop.boolValue;
        }
    }
}
