using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One tunable number of the Ark-escort AI (<see cref="EscortGeneRegistry"/>): its key, default, range and the
    /// value in force. Roles hold a static reference and read <see cref="Value"/> every time they need it, so a value
    /// set at run start (or mid-run) is picked up without any re-arming.
    /// </summary>
    public sealed class EscortGene
    {
        public readonly string Key;
        public readonly float Default;
        public readonly float Min;
        public readonly float Max;
        public readonly string Doc;

        /// <summary>The value in force, always within [<see cref="Min"/>, <see cref="Max"/>].</summary>
        public float Value;

        internal EscortGene(string key, float def, float min, float max, string doc)
        {
            Key = key;
            Min = Mathf.Min(min, max);
            Max = Mathf.Max(min, max);
            Default = Mathf.Clamp(def, Min, Max);
            Value = Default;
            Doc = doc ?? string.Empty;
        }

        public int Int => Mathf.RoundToInt(Value);

        /// <summary>A 0/1 gene read as a switch.</summary>
        public bool On => Value >= 0.5f;

        public static implicit operator float(EscortGene gene) => gene.Value;
    }

    /// <summary>
    /// The named registry of every number the Ark-escort roles (<see cref="ArkEscortPilot"/>, <c>*Role</c>) can be
    /// tuned by: <c>"ai.sparrow.standoff"</c> and the like, each with a default and a range. A parameter file read at
    /// run start sets them by key through <see cref="TrySet"/> (the <c>-arkEscortParams</c> hook in the run calls it);
    /// an evolution loop does the same between runs. <see cref="ResetAll"/> puts every gene back to its default.
    ///
    /// <para>All genes are declared here, as static fields, so the whole set exists the moment anything touches the
    /// registry - a setter never has to wait for a role to be constructed. <see cref="Describe"/> lists them.</para>
    /// </summary>
    public static class EscortGeneRegistry
    {
        // Declared FIRST: the gene fields below register into it during the same static initialisation.
        static readonly Dictionary<string, EscortGene> s_genes = new(StringComparer.OrdinalIgnoreCase);
        static readonly List<EscortGene> s_order = new();

        /// <summary>Declare a gene. A second declaration of the same key returns the first (its range is kept).</summary>
        public static EscortGene Define(string key, float def, float min, float max, string doc = null)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("A gene needs a key.", nameof(key));
            if (s_genes.TryGetValue(key, out var existing)) return existing;
            var gene = new EscortGene(key, def, min, max, doc);
            s_genes[key] = gene;
            s_order.Add(gene);
            return gene;
        }

        /// <summary>Set a gene by key; the value is clamped to its range. False for a key nobody declared.</summary>
        public static bool TrySet(string key, float value)
        {
            if (string.IsNullOrEmpty(key) || !s_genes.TryGetValue(key, out var gene)) return false;
            if (float.IsNaN(value) || float.IsInfinity(value)) return false;
            gene.Value = Mathf.Clamp(value, gene.Min, gene.Max);
            return true;
        }

        public static bool TryGet(string key, out float value)
        {
            if (!string.IsNullOrEmpty(key) && s_genes.TryGetValue(key, out var gene))
            {
                value = gene.Value;
                return true;
            }
            value = 0f;
            return false;
        }

        public static bool TryGetGene(string key, out EscortGene gene) =>
            s_genes.TryGetValue(key ?? string.Empty, out gene);

        /// <summary>Every gene back to its default.</summary>
        public static void ResetAll()
        {
            foreach (var g in s_order) g.Value = g.Default;
        }

        /// <summary>Every gene, in declaration order.</summary>
        public static IReadOnlyList<EscortGene> All => s_order;

        /// <summary>Set every <c>key=value</c> pair of <paramref name="pairs"/> (a flat JSON object or a
        /// <c>key=value;key=value</c> line); returns how many keys were known.</summary>
        public static int ApplyPairs(IEnumerable<KeyValuePair<string, float>> pairs)
        {
            int applied = 0;
            if (pairs == null) return 0;
            foreach (var kv in pairs)
                if (TrySet(kv.Key, kv.Value)) applied++;
            return applied;
        }

        /// <summary>A line per gene: key, value, default, range, note. For logs and the tuning docs.</summary>
        public static string Describe()
        {
            var sb = new StringBuilder();
            foreach (var g in s_order)
                sb.Append(g.Key).Append(" = ").Append(g.Value.ToString("0.###"))
                  .Append(" (default ").Append(g.Default.ToString("0.###"))
                  .Append(", range ").Append(g.Min.ToString("0.###")).Append("..").Append(g.Max.ToString("0.###")).Append(')')
                  .Append(string.IsNullOrEmpty(g.Doc) ? "" : " - " + g.Doc).Append('\n');
            return sb.ToString();
        }

        // ── Shared by every seat (ArkEscortContext / ArkEscortRun) ───────────────────────────────────────────────

        public static readonly EscortGene GuardRadius = Define("ai.escort.guardRadius", 900f, 400f, 1400f,
            "how far from the Ark a hostile creature counts as a threat");
        public static readonly EscortGene RetargetSeconds = Define("ai.escort.retargetSeconds", 0.5f, 0.2f, 2f,
            "seconds between threat re-rankings");
        public static readonly EscortGene RetargetMargin = Define("ai.escort.retargetMargin", 0.15f, 0f, 0.5f,
            "a seat keeps its target unless a free threat beats it by this share of the top score");
        public static readonly EscortGene ThreatWeightDistance = Define("ai.escort.threatW_dist", 1f, 0f, 1f,
            "threat score weight: nearness to the hull");
        public static readonly EscortGene ThreatWeightClosing = Define("ai.escort.threatW_closing", 0.5f, 0f, 1f,
            "threat score weight: closing speed on the Ark");
        public static readonly EscortGene StationRadius = Define("ai.escort.stationRadius", 260f, 120f, 600f,
            "the idle station's distance ahead of and beside the Ark");
        public static readonly EscortGene LeashFactor = Define("ai.escort.leashFactor", 1.4f, 1f, 3f,
            "the wingmate leash as a multiple of guardRadius (capped by the toy's WingmateLeash)");
        public static readonly EscortGene ArkLaneMargin = Define("ai.escort.arkLaneMargin", 40f, 10f, 120f,
            "padding around the hull a weapon's lane must clear");

        // ── Sparrow (destroyer) ──────────────────────────────────────────────────────────────────────────────────

        public static readonly EscortGene SparrowStandoff = Define("ai.sparrow.standoff", 150f, 60f, 300f,
            "the firing position's distance from the creature, on its Ark side");
        public static readonly EscortGene SparrowLeadSeconds = Define("ai.sparrow.leadSeconds", 0.4f, 0f, 1.5f,
            "seconds of the creature's motion to lead");
        public static readonly EscortGene SparrowRocketMinRange = Define("ai.sparrow.rocketMinRange", 60f, 40f, 120f,
            "no rocket at a creature closer than this (it would burst in the hull's own face)");
        public static readonly EscortGene SparrowRocketMaxRange = Define("ai.sparrow.rocketMaxRange", 220f, 120f, 320f,
            "no rocket at a creature farther than this (the fuze's reach)");
        public static readonly EscortGene SparrowRocketAimDeg = Define("ai.sparrow.rocketAimDeg", 8f, 1f, 20f,
            "the creature must be within this many degrees of the nose for a rocket");
        public static readonly EscortGene SparrowRocketCooldown = Define("ai.sparrow.rocketCooldown", 1.5f, 0.5f, 6f,
            "seconds between rockets");
        public static readonly EscortGene SparrowRocketBlastRadius = Define("ai.sparrow.rocketBlastRadius", 170f, 60f, 260f,
            "the rocket's destructive blast radius the Ark must be kept out of");
        public static readonly EscortGene SparrowGunAimDeg = Define("ai.sparrow.gunAimDeg", 12f, 2f, 25f,
            "the creature must be within this many degrees of the nose for the guns");
        public static readonly EscortGene SparrowGunRange = Define("ai.sparrow.gunRange", 240f, 100f, 260f,
            "guns fire at a creature up to this far");
        public static readonly EscortGene SparrowSweepWhenThreatsBelow = Define("ai.sparrow.nucleusSweepWhenThreatsBelow", 1f, 0f, 4f,
            "with fewer threats than this, sweep a hostile nucleus with the guns (0 never)");
        public static readonly EscortGene SparrowSweepRadiusFrac = Define("ai.sparrow.nucleusSweepRadiusFrac", 0.6f, 0.2f, 0.9f,
            "the sweep's radius as a share of the nucleus radius");

        // ── Squirrel (cultivator) ────────────────────────────────────────────────────────────────────────────────

        public static readonly EscortGene SquirrelJoustMargin = Define("ai.squirrel.joustMargin", 10f, 1f, 60f,
            "commit to a joust only this much faster (u/s) than the creature");
        public static readonly EscortGene SquirrelJoustMaxTurnDeg = Define("ai.squirrel.joustMaxTurnDeg", 70f, 15f, 120f,
            "commit to a joust only with the heart within this many degrees of the nose");
        public static readonly EscortGene SquirrelRunupDistance = Define("ai.squirrel.runupDistance", 260f, 100f, 600f,
            "length of the straight flown to gain speed before a joust");
        public static readonly EscortGene SquirrelCultivate = Define("ai.squirrel.cultivate", 1f, 0f, 1f,
            "1: with no threats, race to a hostile nucleus and lay mass until the world turns (0: hold station)");
        public static readonly EscortGene SquirrelCultivateWhenThreatsBelow = Define("ai.squirrel.cultivateWhenThreatsBelow", 1f, 0f, 6f,
            "cultivate only with fewer threats than this");
        public static readonly EscortGene SquirrelPacifyMargin = Define("ai.squirrel.pacifyMargin", 1.3f, 1f, 2f,
            "the nucleus is won when own volume exceeds the hostile volume by this factor");
        public static readonly EscortGene SquirrelWeaveRadiusFrac = Define("ai.squirrel.weaveRadiusFrac", 0.6f, 0.2f, 0.9f,
            "the nucleus weave's radius as a share of the nucleus radius");
        public static readonly EscortGene SquirrelNucleusLeashExtra = Define("ai.squirrel.nucleusLeashExtra", 600f, 0f, 1500f,
            "extra leash past the nucleus while cultivating");
        public static readonly EscortGene SquirrelRingOnRunup = Define("ai.squirrel.ringOnRunup", 1f, 0f, 1f,
            "1: press the boost ring on a joust run-up");

        // ── Dolphin (blaster) ────────────────────────────────────────────────────────────────────────────────────

        public static readonly EscortGene DolphinReach = Define("ai.dolphin.reach", 1200f, 200f, 2400f,
            "the cone's reach assumed when choosing a crystal (round 2: 800 -> 1200, the d3 Dolphin squad blasted too rarely)");
        public static readonly EscortGene DolphinReachUse = Define("ai.dolphin.reachUse", 0.9f, 0.1f, 1f,
            "a crystal counts only if the threats are within reach x this of it (round 2: 0.7 -> 0.9)");
        public static readonly EscortGene DolphinApproachAlignDeg = Define("ai.dolphin.approachAlignDeg", 50f, 5f, 60f,
            "preferred: approach direction within this many degrees of crystal-to-threat (round 2: 35 -> 50)");
        public static readonly EscortGene DolphinConeArkClearance = Define("ai.dolphin.coneArkClearance", 120f, 0f, 300f,
            "padding around the hull the cone's axis must clear");
        public static readonly EscortGene DolphinCrystalSearchRadius = Define("ai.dolphin.crystalSearchRadius", 2500f, 300f, 4000f,
            "crystals farther than this from the Ark are not candidates");
        public static readonly EscortGene DolphinWakeOffset = Define("ai.dolphin.wakeOffset", 150f, 20f, 300f,
            "with nothing to blast, skim the Ark's wake this far astern of the hull");

        // ── Serpent (walls) ──────────────────────────────────────────────────────────────────────────────────────

        public static readonly EscortGene SerpentWallStandoff = Define("ai.serpent.wallStandoff", 320f, 150f, 600f,
            "the wall seed's distance from the Ark toward the threats (past the hull plus the steal reach)");
        public static readonly EscortGene SerpentWallLateralSpread = Define("ai.serpent.wallLateralSpread", 80f, 0f, 200f,
            "sideways offset per seat so two Serpents wall side by side");
        public static readonly EscortGene SerpentWallPlaceRadius = Define("ai.serpent.wallPlaceRadius", 50f, 20f, 120f,
            "cloak when within this of the wall point");
        public static readonly EscortGene SerpentWallCooldown = Define("ai.serpent.wallCooldown", 16f, 8f, 40f,
            "seconds between walls");
        public static readonly EscortGene SerpentUseStance = Define("ai.serpent.useStance", 0f, 0f, 1f,
            "1: also stop (stance) at the wall point and hold");
        public static readonly EscortGene SerpentHoldSeconds = Define("ai.serpent.holdSeconds", 4f, 0f, 12f,
            "seconds to hold the stance at the wall point");

        // ── Scarab (rings) ───────────────────────────────────────────────────────────────────────────────────────

        public static readonly EscortGene ScarabPlateRange = Define("ai.scarab.plateRange", 160f, 60f, 260f,
            "dash (plate) at a creature's heart within this");
        public static readonly EscortGene ScarabRingStandoff = Define("ai.scarab.ringStandoff", 250f, 100f, 500f,
            "rings go on sites within this of the Ark, toward the threats");
        public static readonly EscortGene ScarabSiteMaxAge = Define("ai.scarab.siteMaxAge", 8f, 1f, 20f,
            "a crystal site (where a creature swam, a free crystal) is remembered this long");
        public static readonly EscortGene ScarabSiteReach = Define("ai.scarab.siteReach", 70f, 30f, 150f,
            "a ring press lands only if a site is within this of the ring's centre");
        public static readonly EscortGene ScarabRingCooldown = Define("ai.scarab.ringCooldown", 12f, 2f, 60f,
            "seconds between ring presses");

        // ── Command line ─────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Argument naming gene overrides for a sim run: <c>-escortGenes "ai.squirrel.cultivate=0;ai.sparrow.standoff=120"</c>.
        /// A test path for the sim launcher; the run's own <c>-arkEscortParams</c> JSON hook sets genes by key too.</summary>
        public const string CommandLineArg = "-escortGenes";

        // Declared LAST so every gene above exists when the command line is applied.
        static readonly int s_fromCommandLine = ApplyCommandLine();

        static int ApplyCommandLine()
        {
            string[] args;
            try { args = Environment.GetCommandLineArgs(); }
            catch (Exception) { return 0; }
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (!string.Equals(args[i], CommandLineArg, StringComparison.OrdinalIgnoreCase)) continue;
                int n = ApplyLine(args[i + 1]);
                Debug.Log($"[EscortGenes] {n} gene(s) set from the command line: {args[i + 1]}");
                return n;
            }
            return 0;
        }

        /// <summary>Apply <c>key=value;key=value</c> (';' or ',' separated). Returns how many keys were known.</summary>
        public static int ApplyLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return 0;
            int applied = 0;
            foreach (var pair in line.Trim('"').Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = pair.IndexOf('=');
                if (eq <= 0) continue;
                string key = pair.Substring(0, eq).Trim();
                if (!float.TryParse(pair.Substring(eq + 1).Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float value)) continue;
                if (TrySet(key, value)) applied++;
                else Debug.LogWarning($"[EscortGenes] Unknown gene '{key}'.");
            }
            return applied;
        }

        /// <summary>Touch the registry so every gene above is declared (static initialisation).</summary>
        public static void EnsureDefined() { _ = s_order.Count + s_fromCommandLine; }
    }
}
