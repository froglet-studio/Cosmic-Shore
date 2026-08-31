using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Locks the SWITCH shader vocabulary (Docs/ToySystem/ARCHITECTURE.md, "The switch"):
    /// every switch is drawn in the prism shader, and WHICH prism it is painted as says what it
    /// will do.
    ///
    /// <para>The rule that needs holding is the RESERVATION - <b>a switch wearing a playable
    /// domain's colour is one that hands you that domain</b>. Half of it is structural and needs
    /// no test: <see cref="ToyFactory.SwitchDomain"/> forces a
    /// <see cref="ToySwitchSignal.Neutral"/> switch to Blue whatever a caller passes, and
    /// <c>AddSwitchRing</c> takes no raw colour or material, so the signal is the only door. The
    /// other half is a CALL-SITE fact - who may ask for <see cref="ToySwitchSignal.Domain"/> at
    /// all - which lives in source where no compiler sees it, so this reads the source, the same
    /// shape as the other law tests in this suite.</para>
    /// </summary>
    public class ToySwitchVocabularyTests
    {
        // Every file permitted to ask for a DOMAIN-coloured switch, and why. Adding a row is the
        // deliberate act the reservation exists to make deliberate.
        static readonly Dictionary<string, string> SanctionedDomainSwitches = new()
        {
            ["DomainChangerToySet.cs"] =
                "the Domain Changer's slots - threading one calls RequestSetDomain_ServerRpc",
            ["PaintingRunner.cs"] =
                "the painting's stroke-start gates - crossing one sets the stroke's domain",
            ["ScarabSwitch.cs"] =
                "the Scarab's placed switch - the one wearer OUTSIDE the toybox, where the colour " +
                "names the domain the switch belongs to (SCARAB.md section 5). Nothing in that " +
                "mode changes a pilot's domain, so the two readings never share a screen.",
        };

        // Files that NAME the member without requesting a switch.
        static readonly HashSet<string> Exempt = new()
        {
            "ToySwitchSignal.cs",           // the enum's own declaration
            "ToyFactory.cs",                // the resolver that implements the vocabulary
            "Toy.cs",                       // the base that carries a signal to the builder
            "ToySwitchVocabularyTests.cs",  // this file
        };

        static string ScriptRoot => Path.Combine(Application.dataPath, "_Scripts");

        static IEnumerable<string> SourceFiles() =>
            Directory.EnumerateFiles(ScriptRoot, "*.cs", SearchOption.AllDirectories);

        // ── The structural half ──────────────────────────────────────────────

        [Test]
        public void NeutralSwitchIsAlwaysBlue_WhateverDomainTheCallerPasses()
        {
            foreach (Domains domain in System.Enum.GetValues(typeof(Domains)))
                Assert.AreEqual(Domains.Blue, ToyFactory.SwitchDomain(ToySwitchSignal.Neutral, domain),
                    $"A Neutral switch asked for {domain} must still be painted Blue - the signal " +
                    "picks the colour, never the caller.");
        }

        [Test]
        public void DomainSwitchWearsTheDomainItWasGiven()
        {
            foreach (var domain in GameDataSO.ActiveDomains)
                Assert.AreEqual(domain, ToyFactory.SwitchDomain(ToySwitchSignal.Domain, domain),
                    $"A Domain switch for {domain} must wear {domain}.");
        }

        [Test]
        public void DomainSwitchOnTheNeutralSentinelStaysNeutral()
        {
            // Domains.Blue IS "no team", so a Domain-signalled switch on it is not a claim.
            Assert.AreEqual(Domains.Blue, ToyFactory.SwitchDomain(ToySwitchSignal.Domain, Domains.Blue));
        }

        [Test]
        public void NeutralAndPlayableSwitchColoursAreDistinguishable()
        {
            // No theme wired: the fixed fallback palette. Neutral must not be mistakable for a
            // playable domain, or the reservation says nothing on screen.
            Color neutral = ToyFactory.SwitchColor(null, ToySwitchSignal.Neutral, Domains.Blue);
            foreach (var domain in GameDataSO.ActiveDomains)
            {
                Color playable = ToyFactory.SwitchColor(null, ToySwitchSignal.Domain, domain);
                float delta = Mathf.Abs(neutral.r - playable.r)
                            + Mathf.Abs(neutral.g - playable.g)
                            + Mathf.Abs(neutral.b - playable.b);
                Assert.Greater(delta, 0.5f,
                    $"The neutral switch colour is too close to {domain}'s ({delta:F2} summed " +
                    "channel distance) - a player cannot read the reservation off it.");
            }
        }

        [Test]
        public void AddSwitchRingTakesNoRawColourOrMaterial()
        {
            // The signal is the ONLY door. A raw Color or Material parameter would let a caller
            // paint a switch anything at all, which is exactly what the reservation forbids.
            var overloads = typeof(ToyFactory)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == nameof(ToyFactory.AddSwitchRing))
                .ToList();

            CollectionAssert.IsNotEmpty(overloads, "ToyFactory.AddSwitchRing has gone missing.");
            foreach (var m in overloads)
                foreach (var p in m.GetParameters())
                    Assert.That(p.ParameterType, Is.Not.EqualTo(typeof(Color))
                                             .And.Not.EqualTo(typeof(Material)),
                        $"AddSwitchRing takes a raw {p.ParameterType.Name} ('{p.Name}') - a switch's " +
                        "look must come from its ToySwitchSignal, not from the caller.");
        }

        // ── The call-site half ───────────────────────────────────────────────

        /// <summary>Filenames under _Scripts that mention <c>ToySwitchSignal.Domain</c>.</summary>
        static HashSet<string> FilesAskingForADomainSwitch()
        {
            var asking = new HashSet<string>();
            foreach (string path in SourceFiles())
            {
                string file = Path.GetFileName(path);
                if (Exempt.Contains(file)) continue;
                if (File.ReadAllText(path).Contains("ToySwitchSignal.Domain"))
                    asking.Add(file);
            }
            return asking;
        }

        [Test]
        public void OnlySanctionedCallSitesAskForADomainColouredSwitch()
        {
            var offenders = FilesAskingForADomainSwitch()
                .Where(f => !SanctionedDomainSwitches.ContainsKey(f))
                .OrderBy(f => f)
                .ToList();

            CollectionAssert.IsEmpty(offenders,
                "A domain-coloured switch is RESERVED to the things that hand you a domain " +
                $"({string.Join(", ", SanctionedDomainSwitches.Keys)}). Offender(s): " +
                $"{string.Join(", ", offenders)}. If this really is a new domain changer, add it " +
                "to SanctionedDomainSwitches with the reason; otherwise it wants " +
                "ToySwitchSignal.Neutral.");
        }

        [Test]
        public void EverySanctionedCallSiteStillAsksForOne()
        {
            var asking = FilesAskingForADomainSwitch();
            foreach (var entry in SanctionedDomainSwitches)
                Assert.IsTrue(asking.Contains(entry.Key),
                    $"{entry.Key} is listed as a sanctioned domain switch ({entry.Value}) but no " +
                    "longer asks for one - it was renamed, deleted, or moved to Neutral. Drop the " +
                    "row: an allow-list nobody uses stops describing the reservation and starts " +
                    "hiding it.");
        }

    }
}
