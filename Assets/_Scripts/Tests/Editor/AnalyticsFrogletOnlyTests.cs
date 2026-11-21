#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CosmicShore.Core;
using CosmicShore.Data;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The 2026-11-06 analytics additions (Shombith): the match's element on game_started /
    /// game_completed, the vessel on freestyle_entered, and the two Freestyle (Toybox) events
    /// freestyle_ended and freestyle_vessel_changed - all of them Froglet Analytics only.
    ///
    /// WHY THIS MATTERS:
    /// 1. UGS DISCARDS, SILENTLY, ANY EVENT THAT CARRIES A PARAMETER ITS EVENT MANAGER DOES NOT
    ///    DECLARE. One Froglet-only field leaking into UGS takes the whole UGS game_started with
    ///    it, and nothing anywhere reports it. So the UGS sink's filter must equal what the schema
    ///    marks "sinks": ["froglet"] - checked here against Docs/Analytics/EVENT_SCHEMA.json itself.
    /// 2. THE FILTER MUST NOT TOUCH THE SHARED PAYLOAD. The facade hands the same dictionary to
    ///    every sink; stripping it in place would strip Froglet Analytics too.
    /// 3. THE FROGLET SERVER REJECTS EVENTS MISSING FROM THE SCHEMA, so every event name the game
    ///    sends must be in it (the server's own test holds the same rule from the Python side).
    /// 4. A MODE WITH NO GENRE SENDS NO ELEMENT, rather than "None" or "", which a dashboard would
    ///    group as a fake element.
    /// 5. THE MATCH SETUP (2026-11-07) IS COUNTED FROM THE LOCAL PLAYER'S SIDE: humans and AI on its
    ///    domain (itself included) and on the others - what splits solo, co-op, competitive and teams vs AI.
    /// </summary>
    [TestFixture]
    public class AnalyticsFrogletOnlyTests
    {
        static JObject s_schema;

        static JObject Schema
        {
            get
            {
                if (s_schema != null) return s_schema;
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "Analytics", "EVENT_SCHEMA.json"));
                Assert.IsTrue(File.Exists(path), "the event contract: " + path);
                s_schema = JObject.Parse(File.ReadAllText(path));
                return s_schema;
            }
        }

        static JObject Events => (JObject)Schema["events"];

        /// <summary>A "sinks" list that leaves out ugs: what UGS must never be sent.</summary>
        static bool NotForUgs(JToken entry) =>
            entry is JObject o && o["sinks"] is JArray sinks && sinks.All(s => (string)s != "ugs");

        /// <summary>The schema's Froglet-only fields in <see cref="UgsAnalyticsSink.FrogletOnlyFields"/>' shape.</summary>
        static HashSet<string> SchemaFrogletOnlyFields()
        {
            var fields = new HashSet<string>();
            foreach (var evt in Events.Properties())
            {
                if (NotForUgs(evt.Value))
                {
                    // The whole event is dropped, so its parameters need no entries of their own.
                    fields.Add(evt.Name);
                    continue;
                }

                if (evt.Value["parameters"] is JObject parameters)
                    foreach (var p in parameters.Properties())
                        if (NotForUgs(p.Value))
                            fields.Add(evt.Name + "." + p.Name);
            }
            return fields;
        }

        // ── 1. The one list mirrors the schema ─────────────────────────────────

        [Test]
        public void UgsFilterMirrorsTheSchema()
        {
            var schema = SchemaFrogletOnlyFields();
            var sink = new HashSet<string>(UgsAnalyticsSink.FrogletOnlyFields);

            CollectionAssert.IsEmpty(schema.Except(sink).OrderBy(x => x),
                "marked \"sinks\": [\"froglet\"] in EVENT_SCHEMA.json but not stripped for UGS - add to UgsAnalyticsSink.FrogletOnlyFields");
            CollectionAssert.IsEmpty(sink.Except(schema).OrderBy(x => x),
                "stripped for UGS but not Froglet-only in EVENT_SCHEMA.json - mark the schema or drop the entry");
        }

        [Test]
        public void SchemaMarksTheExpectedFields()
        {
            // Pins the 2026-11-06 contract, so a schema edit that silently un-marks a field fails here.
            var schema = SchemaFrogletOnlyFields();
            Assert.That(schema, Does.Contain("freestyle_ended"));
            Assert.That(schema, Does.Contain("freestyle_vessel_changed"));
            Assert.That(schema, Does.Contain("game_started.element"));
            Assert.That(schema, Does.Contain("game_started.element_secondary"));
            Assert.That(schema, Does.Contain("game_completed.element"));
            Assert.That(schema, Does.Contain("game_completed.element_secondary"));
            Assert.That(schema, Does.Contain("freestyle_entered.vessel_class"));

            // 2026-11-07: the match setup on game_started, from the local player's side.
            Assert.That(schema, Does.Contain("game_started.domain"));
            Assert.That(schema, Does.Contain("game_started.humans_on_my_domain"));
            Assert.That(schema, Does.Contain("game_started.humans_on_other_domains"));
            Assert.That(schema, Does.Contain("game_started.ai_on_my_domain"));
            Assert.That(schema, Does.Contain("game_started.ai_on_other_domains"));
            // game_started only: game_completed joins back on match_id.
            Assert.That(Events[UGSKeys.EventGameCompleted]["parameters"][UGSKeys.ParamHumansOnMyDomain], Is.Null);
        }

        [Test]
        public void MatchSetupKeysMatchTheSchema()
        {
            var p = new Dictionary<string, object>();
            AnalyticsServiceFacade.DomainComposition.Count(Domains.Jade, null).AddTo(p, Domains.Jade);
            var declared = ((JObject)Events[UGSKeys.EventGameStarted]["parameters"]).Properties().Select(x => x.Name).ToList();
            foreach (var key in p.Keys)
            {
                Assert.That(declared, Does.Contain(key), "game_started." + key + " is in EVENT_SCHEMA.json");
                Assert.That(UgsAnalyticsSink.FrogletOnlyFields, Does.Contain(UGSKeys.EventGameStarted + "." + key));
            }
            Assert.AreEqual(5, p.Count);
        }

        // ── 2. The filter ──────────────────────────────────────────────────────

        [Test]
        public void FrogletOnlyEventsAreNotSentToUgs()
        {
            var p = AnalyticsServiceFacade.BuildFreestyleEndedParameters(12.5f, "Squirrel", 2, 90);
            Assert.IsFalse(UgsAnalyticsSink.TryFilterForUgs(UGSKeys.EventFreestyleEnded, p, out _));

            var c = AnalyticsServiceFacade.BuildFreestyleVesselChangedParameters("Manta", "Squirrel", 4f);
            Assert.IsFalse(UgsAnalyticsSink.TryFilterForUgs(UGSKeys.EventFreestyleVesselChanged, c, out _));
        }

        [Test]
        public void FrogletOnlyParametersAreStrippedWithoutTouchingThePayload()
        {
            var payload = new Dictionary<string, object>
            {
                { "game_mode", "BroodRush" },
                { "vessel_class", "Rhino" },
                { UGSKeys.ParamElement, "Mass" },
                { UGSKeys.ParamElementSecondary, "Space" }
            };

            Assert.IsTrue(UgsAnalyticsSink.TryFilterForUgs(UGSKeys.EventGameStarted, payload, out var ugs));
            Assert.IsFalse(ugs.ContainsKey(UGSKeys.ParamElement));
            Assert.IsFalse(ugs.ContainsKey(UGSKeys.ParamElementSecondary));
            Assert.AreEqual("BroodRush", ugs["game_mode"]);
            // vessel_class is Froglet-only on freestyle_entered, NOT on game_started.
            Assert.AreEqual("Rhino", ugs["vessel_class"]);

            Assert.AreEqual(4, payload.Count, "the shared payload is untouched - Froglet Analytics still gets the element");
            Assert.AreEqual("Mass", payload[UGSKeys.ParamElement]);
        }

        [Test]
        public void FreestyleEnteredLosesOnlyItsVesselForUgs()
        {
            var payload = new Dictionary<string, object>
            {
                { "is_first", true },
                { UGSKeys.ParamSecondsSinceLaunch, 30 },
                { UGSKeys.ParamVesselClass, "Sparrow" }
            };

            Assert.IsTrue(UgsAnalyticsSink.TryFilterForUgs(UGSKeys.EventFreestyleEntered, payload, out var ugs));
            CollectionAssert.AreEquivalent(new[] { "is_first", UGSKeys.ParamSecondsSinceLaunch }, ugs.Keys);
        }

        [Test]
        public void OtherEventsPassThroughUnchanged()
        {
            var payload = new Dictionary<string, object> { { "element", "kept" } };
            Assert.IsTrue(UgsAnalyticsSink.TryFilterForUgs(UGSKeys.EventUiAction, payload, out var ugs));
            Assert.AreSame(payload, ugs, "nothing stripped means no copy");

            Assert.IsTrue(UgsAnalyticsSink.TryFilterForUgs(UGSKeys.EventPartyJoined, null, out var none));
            Assert.IsNull(none);
        }

        // ── 3. Every event the game sends is in the schema ─────────────────────

        [Test]
        public void EveryUgsKeysEventIsInTheSchema()
        {
            var missing = typeof(UGSKeys)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.Name.StartsWith("Event"))
                .Select(f => (string)f.GetRawConstantValue())
                .Where(name => Events[name] == null)
                .ToList();

            CollectionAssert.IsEmpty(missing, "add these to Docs/Analytics/EVENT_SCHEMA.json");
        }

        [Test]
        public void NewEventNamesMatchTheSchema()
        {
            Assert.AreEqual("freestyle_ended", UGSKeys.EventFreestyleEnded);
            Assert.AreEqual("freestyle_vessel_changed", UGSKeys.EventFreestyleVesselChanged);
            Assert.IsNotNull(Events[UGSKeys.EventFreestyleEnded]);
            Assert.IsNotNull(Events[UGSKeys.EventFreestyleVesselChanged]);
        }

        [Test]
        public void FreestylePayloadsCarryExactlyTheSchemaParameters()
        {
            AssertKeysMatchSchema(UGSKeys.EventFreestyleEnded,
                AnalyticsServiceFacade.BuildFreestyleEndedParameters(3f, "Dolphin", 1, 10));
            AssertKeysMatchSchema(UGSKeys.EventFreestyleVesselChanged,
                AnalyticsServiceFacade.BuildFreestyleVesselChangedParameters("Dolphin", "Serpent", 3f));
        }

        static void AssertKeysMatchSchema(string eventName, Dictionary<string, object> payload)
        {
            var declared = ((JObject)Events[eventName]["parameters"]).Properties().Select(p => p.Name);
            CollectionAssert.AreEquivalent(declared, payload.Keys, eventName);
        }

        // ── 4. Payload contents ────────────────────────────────────────────────

        [Test]
        public void GenreElementsAreAbsentForAModeWithNoGenre()
        {
            var p = new Dictionary<string, object>();
            AnalyticsServiceFacade.AddGenreElements(p, Element.None, Element.None);
            CollectionAssert.IsEmpty(p);
        }

        [Test]
        public void SingleGenreSendsOnlyThePrimary()
        {
            var p = new Dictionary<string, object>();
            AnalyticsServiceFacade.AddGenreElements(p, Element.Time, Element.None);
            Assert.AreEqual("Time", p[UGSKeys.ParamElement]);
            Assert.IsFalse(p.ContainsKey(UGSKeys.ParamElementSecondary));
        }

        [Test]
        public void TwoElementGenreSendsBoth()
        {
            // Brood Rush, read through the same ModeGenre the card badge reads.
            Assert.IsTrue(ModeGenre.TryElementsFor(GameModes.BroodRush, null, out var a, out var b));
            var p = new Dictionary<string, object>();
            AnalyticsServiceFacade.AddGenreElements(p, a, b);
            Assert.AreEqual("Mass", p[UGSKeys.ParamElement]);
            Assert.AreEqual("Space", p[UGSKeys.ParamElementSecondary]);
        }

        // ── The match setup (game_started, from the local player's side) ───────

        [Test]
        public void MatchSetupCountsHumansAndAiPerDomain()
        {
            // The local human on Jade, another human on Jade, one on Ruby, 2 AI on Jade, 3 AI on Ruby.
            var seats = new List<(Domains Domain, bool IsAI)>
            {
                (Domains.Jade, false), (Domains.Jade, false), (Domains.Ruby, false),
                (Domains.Jade, true), (Domains.Jade, true),
                (Domains.Ruby, true), (Domains.Ruby, true), (Domains.Ruby, true)
            };

            var c = AnalyticsServiceFacade.DomainComposition.Count(Domains.Jade, seats);
            Assert.AreEqual(2, c.HumansOnMyDomain, "the local player is included");
            Assert.AreEqual(1, c.HumansOnOtherDomains);
            Assert.AreEqual(2, c.AiOnMyDomain);
            Assert.AreEqual(3, c.AiOnOtherDomains);

            var p = new Dictionary<string, object>();
            c.AddTo(p, Domains.Jade);
            Assert.AreEqual("Jade", p[UGSKeys.ParamDomain]);
            Assert.AreEqual(2, p[UGSKeys.ParamHumansOnMyDomain]);
            Assert.AreEqual(1, p[UGSKeys.ParamHumansOnOtherDomains]);
            Assert.AreEqual(2, p[UGSKeys.ParamAiOnMyDomain]);
            Assert.AreEqual(3, p[UGSKeys.ParamAiOnOtherDomains]);
        }

        [Test]
        public void MatchSetupFromTheOtherSideMirrors()
        {
            // The same match seen by the Ruby human: its side and the other side swap.
            var seats = new List<(Domains Domain, bool IsAI)>
            {
                (Domains.Jade, false), (Domains.Jade, false), (Domains.Ruby, false),
                (Domains.Jade, true), (Domains.Jade, true),
                (Domains.Ruby, true), (Domains.Ruby, true), (Domains.Ruby, true)
            };

            var c = AnalyticsServiceFacade.DomainComposition.Count(Domains.Ruby, seats);
            Assert.AreEqual(1, c.HumansOnMyDomain);
            Assert.AreEqual(2, c.HumansOnOtherDomains);
            Assert.AreEqual(3, c.AiOnMyDomain);
            Assert.AreEqual(2, c.AiOnOtherDomains);
        }

        [Test]
        public void SoloAgainstAiCountsOnlyTheLocalHuman()
        {
            // Solo on Gold against 3 AI on Ruby: 1 / 0 / 0 / 3.
            var seats = new List<(Domains Domain, bool IsAI)>
            {
                (Domains.Gold, false), (Domains.Ruby, true), (Domains.Ruby, true), (Domains.Ruby, true)
            };

            var c = AnalyticsServiceFacade.DomainComposition.Count(Domains.Gold, seats);
            Assert.AreEqual(1, c.HumansOnMyDomain);
            Assert.AreEqual(0, c.HumansOnOtherDomains);
            Assert.AreEqual(0, c.AiOnMyDomain);
            Assert.AreEqual(3, c.AiOnOtherDomains);
        }

        [Test]
        public void MatchSetupWithNoSeatsCountsNothing()
        {
            var c = AnalyticsServiceFacade.DomainComposition.Count(Domains.Jade, null);
            Assert.AreEqual(0, c.HumansOnMyDomain + c.HumansOnOtherDomains + c.AiOnMyDomain + c.AiOnOtherDomains);
        }

        [Test]
        public void MatchSetupIsStrippedForUgsAndKeptForFroglet()
        {
            var payload = new Dictionary<string, object> { { "game_mode", "Joust" } };
            AnalyticsServiceFacade.DomainComposition.Count(Domains.Jade,
                new List<(Domains Domain, bool IsAI)> { (Domains.Jade, false), (Domains.Ruby, true) }).AddTo(payload, Domains.Jade);

            Assert.IsTrue(UgsAnalyticsSink.TryFilterForUgs(UGSKeys.EventGameStarted, payload, out var ugs));
            CollectionAssert.AreEquivalent(new[] { "game_mode" }, ugs.Keys);
            Assert.AreEqual(6, payload.Count, "the shared payload keeps all five for Froglet Analytics");
        }

        [Test]
        public void FreestyleEndedNeverReportsZeroVessels()
        {
            var p = AnalyticsServiceFacade.BuildFreestyleEndedParameters(1f, null, 0, 5);
            Assert.AreEqual(1, p[UGSKeys.ParamVesselsFlown]);
            Assert.AreEqual("unknown", p[UGSKeys.ParamVesselClass]);
        }

        // ── The stretch's vessel log ───────────────────────────────────────────

        [Test]
        public void VesselLogCountsDistinctVesselsAndNamesTheOneLeft()
        {
            var log = new AnalyticsServiceFacade.FreestyleVesselLog();
            log.Begin("Squirrel");
            Assert.AreEqual(1, log.VesselsFlown);

            Assert.IsTrue(log.TryChange("Manta", out var from));
            Assert.AreEqual("Squirrel", from);
            Assert.IsTrue(log.TryChange("Squirrel", out from));
            Assert.AreEqual("Manta", from);
            Assert.AreEqual(2, log.VesselsFlown, "flying back to a hull does not count it twice");
            Assert.AreEqual("Squirrel", log.Current);
        }

        [Test]
        public void SameVesselOrFirstSightingIsNotAChange()
        {
            var log = new AnalyticsServiceFacade.FreestyleVesselLog();
            Assert.IsFalse(log.TryChange("Rhino", out _), "nothing flown yet: a first sighting");
            Assert.AreEqual("Rhino", log.Current);
            Assert.IsFalse(log.TryChange("Rhino", out _));
            Assert.IsFalse(log.TryChange(null, out _));
            Assert.AreEqual(1, log.VesselsFlown);
        }

        [Test]
        public void ANewStretchForgetsTheLastOne()
        {
            var log = new AnalyticsServiceFacade.FreestyleVesselLog();
            log.Begin("Squirrel");
            log.TryChange("Manta", out _);
            log.Begin("Manta");
            Assert.AreEqual(1, log.VesselsFlown);
            Assert.AreEqual("Manta", log.Current);
        }
    }
}
#endif
