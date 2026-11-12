#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using Newtonsoft.Json;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Unique pilots, unique artifacts and the account inventory that holds them (Docs/Pilots/INVENTORY.md; Garrett,
    /// 2026-11-07). Pure data, no assets: the kinds are built in the test the way <c>ArtifactSO.ToArchetype</c> builds
    /// them. The shipped catalog's kinds are covered in <see cref="PilotsAndArtifactsTests"/>.
    /// </summary>
    [TestFixture]
    public class AccountInventoryTests
    {
        static ArtifactArchetype Javelin() => new("artifact.javelin", "Javelin",
            (Element.Mass, "line width"), (Element.Space, "range"), (Element.Time, "linger"), (Element.Charge, "cracks"));

        static ArtifactArchetype Hydra() => new("artifact.hydra", "Hydra",
            (Element.Mass, "radius"), (Element.Space, "range"), (Element.Time, "linger"), (Element.Charge, "heads"));

        static Func<ulong> Seeds(ulong first = 1)
        {
            ulong next = first;
            return () => next++;
        }

        static bool Printable(string s)
        {
            foreach (char c in s ?? string.Empty)
                if (c < ' ' || c > '~') return false;
            return true;
        }

        // ── Ids and randomness ──────────────────────────────────────────

        [Test]
        public void SeededRandom_IsDeterministic_AndPurposesAreIndependent()
        {
            var a = SeededRandom.For(42, "name");
            var b = SeededRandom.For(42, "name");
            for (int i = 0; i < 16; i++) Assert.AreEqual(a.NextULong(), b.NextULong());
            Assert.AreNotEqual(SeededRandom.For(42, "name").NextULong(), SeededRandom.For(42, "story").NextULong());
            Assert.AreNotEqual(0UL, SeededRandom.NewSeed());
        }

        [Test]
        public void UniqueId_CarriesItsTemplate_AndTellsLegacyIdsApart()
        {
            string id = UniqueId.Compose("artifact.javelin", 0x03aa91c0f2e6d418UL);
            Assert.AreEqual("artifact.javelin#03aa91c0f2e6d418", id);
            Assert.IsTrue(UniqueId.TrySplit(id, out string template, out ulong seed));
            Assert.AreEqual("artifact.javelin", template);
            Assert.AreEqual(0x03aa91c0f2e6d418UL, seed);
            Assert.AreEqual("artifact.javelin", UniqueId.TemplateOf(id));
            Assert.AreEqual("03AA-91C0", UniqueId.SerialOf(id));
            Assert.IsFalse(UniqueId.IsUnique("artifact.javelin"), "a bare kind id is legacy");
            Assert.AreEqual("artifact.javelin", UniqueId.TemplateOf("artifact.javelin"));
            Assert.IsFalse(UniqueId.IsUnique("a#b#0000000000000001"));
            Assert.IsFalse(UniqueId.IsUnique("artifact.javelin#xyz"));
            Assert.AreEqual(string.Empty, UniqueId.TemplateOf(null));
        }

        [Test]
        public void Callsigns_AreTheFixedSet_EachItsOwnVision()
        {
            Assert.AreEqual(18, Callsigns.All.Count, "the eighteen callsigns of CALLSIGNS.md §2");
            var ids = new HashSet<string>();
            foreach (var v in Callsigns.All)
            {
                string id = Callsigns.IdOf(v);
                Assert.IsTrue(ids.Add(id));
                Assert.IsTrue(Callsigns.TryParse(id, out var back));
                Assert.AreEqual(v, back);
            }
            Assert.AreEqual("pilot.callsign.volt", Callsigns.IdOf(PilotVision.Volt));
            Assert.IsFalse(Callsigns.TryParse("pilot.callsign.nobody", out _));
            Assert.IsFalse(Callsigns.TryParse("artifact.javelin", out _));
        }

        // ── The curve ───────────────────────────────────────────────────

        [Test]
        public void Curve_StartsAtBase_HalfwayAtHalf_AndNeverReachesTheCeiling()
        {
            Assert.AreEqual(1f, ArtifactCurve.Level(0, 1f, 9f, 40f), 1e-5f);
            Assert.AreEqual(5f, ArtifactCurve.Level(40, 1f, 9f, 40f), 1e-4f, "half the way at half crystals");
            Assert.AreEqual(7f, ArtifactCurve.Level(120, 1f, 9f, 40f), 1e-4f, "three quarters at three times");
            float huge = ArtifactCurve.Level(long.MaxValue, 1f, 9f, 40f);
            Assert.LessOrEqual(huge, 9f, "infinite input stays inside the band");
            Assert.Greater(huge, 8.99f);
        }

        [Test]
        public void Curve_EveryCrystalAddsSomething_EachLessThanTheOneBefore()
        {
            float last = ArtifactCurve.Level(0, 0f, 10f, 30f), lastGain = float.MaxValue;
            for (long c = 1; c <= 400; c++)
            {
                float level = ArtifactCurve.Level(c, 0f, 10f, 30f);
                float gain = level - last;
                Assert.Greater(gain, 0f, $"crystal {c} added nothing");
                Assert.LessOrEqual(gain, lastGain + 1e-6f, $"crystal {c} added more than the one before");
                last = level;
                lastGain = gain;
            }
        }

        [Test]
        public void Curve_CrystalsFor_IsTheInverse()
        {
            foreach (float target in new[] { 2f, 5f, 7.5f, 9.9f })
            {
                long c = ArtifactCurve.CrystalsFor(target, 1f, 10f, 40f);
                Assert.GreaterOrEqual(ArtifactCurve.Level(c, 1f, 10f, 40f), target - 1e-4f);
                Assert.Less(ArtifactCurve.Level(c - 1, 1f, 10f, 40f), target);
            }
            Assert.AreEqual(0, ArtifactCurve.CrystalsFor(0.5f, 1f, 10f, 40f));
            Assert.AreEqual(long.MaxValue, ArtifactCurve.CrystalsFor(10f, 1f, 10f, 40f), "the ceiling is never reached");
        }

        // ── Artifacts ───────────────────────────────────────────────────

        [Test]
        public void Artifact_IsAPureFunctionOfKindAndSeed()
        {
            var a = ArtifactGenerator.Roll(Javelin(), 12345);
            var b = ArtifactGenerator.Roll(Javelin(), 12345);
            Assert.AreEqual(JsonConvert.SerializeObject(a), JsonConvert.SerializeObject(b));
            var again = ArtifactGenerator.FromId(a.Id, Javelin());
            Assert.AreEqual(a.Name, again.Name, "an id seen anywhere re-rolls the same artifact");
            Assert.AreEqual(a.Element, again.Element);
            Assert.IsNull(ArtifactGenerator.FromId(a.Id, Hydra()), "the id names a Javelin, not a Hydra");
            Assert.IsNull(ArtifactGenerator.FromId("artifact.javelin", Javelin()), "a bare kind id has no seed");
        }

        [Test]
        public void Artifacts_AreUnlimitedAndUnique_AttunedToOneElement_InsideTheirRanges()
        {
            var ids = new HashSet<string>();
            var names = new HashSet<string>();
            var elements = new HashSet<Element>();
            var builds = new HashSet<string>();
            for (ulong seed = 1; seed <= 5000; seed++)
            {
                var a = ArtifactGenerator.Roll(Javelin(), seed * 0x9E3779B97F4A7C15UL);
                Assert.IsTrue(ids.Add(a.Id), "no two artifacts share an id");
                names.Add(a.Name);
                elements.Add(a.Element);
                builds.Add($"{a.Element}|{a.BaseLevel}|{a.Ceiling}|{a.HalfCrystals}|{a.FixedMass}|{a.FixedSpace}|{a.FixedTime}|{a.FixedCharge}");
                Assert.IsTrue(ArtifactLevels.IsTrackElement(a.Element));
                Assert.AreEqual(Javelin().AspectOf(a.Element), a.Aspect, "it improves exactly the kind's track for its element");
                Assert.That(a.Ceiling, Is.InRange(ArtifactGenerator.CeilingMin, ArtifactGenerator.CeilingMax));
                Assert.That(a.BaseLevel, Is.InRange(ArtifactGenerator.BaseMin, ArtifactGenerator.BaseMax));
                Assert.That(a.HalfCrystals, Is.InRange(ArtifactGenerator.HalfMin, ArtifactGenerator.HalfMax));
                Assert.That(a.Facets, Is.InRange(3, 9));
                Assert.IsTrue(Printable(a.Name) && Printable(a.Lore), a.Name);
                Assert.Less(a.Levels().Of(a.Element), ArtifactLevels.TwistLevel, "nothing is twisted before it is fed");
            }
            Assert.AreEqual(4, elements.Count, "every element comes up");
            Assert.Greater(names.Count, 1500, "names vary widely before the serial");
            Assert.AreEqual(5000, builds.Count, "no two of 5000 even play alike");
        }

        [Test]
        public void Feeding_RaisesOnlyItsOneTrack_AcceptsOnlyItsElement_WithoutLimit()
        {
            var inv = new AccountInventory();
            var a = inv.MintArtifact(Javelin(), "test", 0, Seeds());
            var before = a.Levels();
            var other = a.Element == Element.Mass ? Element.Charge : Element.Mass;

            var refused = inv.Feed(a.Id, other, 10);
            Assert.AreEqual(AccountInventory.FeedStatus.WrongElement, refused.Status, "one element only");
            Assert.AreEqual(0, a.Invested);

            var fed = inv.Feed(a.Id, a.Element, 50);
            Assert.AreEqual(AccountInventory.FeedStatus.Fed, fed.Status);
            Assert.Greater(fed.After, fed.Before);
            var after = a.Levels();
            foreach (var e in ArtifactLevels.TrackElements)
                if (e != a.Element) Assert.AreEqual(before.Of(e), after.Of(e), $"{e} is fixed");

            Assert.AreEqual(AccountInventory.FeedStatus.Fed, inv.Feed(a.Id, Element.Omni, 5).Status, "omni feeds the attuned track");
            Assert.AreEqual(55, a.Invested);
            inv.Feed(a.Id, a.Element, long.MaxValue);
            inv.Feed(a.Id, a.Element, long.MaxValue);
            Assert.AreEqual(long.MaxValue, a.Invested, "saturates instead of overflowing");
            Assert.LessOrEqual(a.AttunedLevel(), a.Ceiling);
            Assert.AreEqual(AccountInventory.FeedStatus.NothingToFeed, inv.Feed(a.Id, a.Element, 0).Status);
            Assert.AreEqual(AccountInventory.FeedStatus.NoSuchArtifact, inv.Feed("artifact.javelin#0000000000000999", a.Element, 1).Status);
        }

        [Test]
        public void Wire_CarriesTheArtifactWhole()
        {
            var inv = new AccountInventory();
            var a = inv.MintArtifact(Javelin(), "test", 0, Seeds(77));
            inv.Feed(a.Id, a.Element, 123);
            ulong code = ArtifactWire.Pack(3, a);
            Assert.IsTrue(ArtifactWire.TryUnpack(code, out int kind, out var element, out var levels, out _));
            Assert.AreEqual(3, kind);
            Assert.AreEqual(a.Element, element);
            foreach (var e in ArtifactLevels.TrackElements)
                Assert.AreEqual(a.Levels().Of(e), levels.Of(e), e == a.Element ? 0.006f : 0.11f, e.ToString());
            Assert.AreEqual(0UL, ArtifactWire.Pack(0, null));
            Assert.IsFalse(ArtifactWire.TryUnpack(0, out _, out _, out _, out _), "0 is no artifact");
        }

        // ── Pilots ──────────────────────────────────────────────────────

        [Test]
        public void Pilot_IsAUniquePerson_OnAFixedCallsign()
        {
            var p = PilotGenerator.Roll(PilotVision.Volt, 99);
            Assert.AreEqual("pilot.callsign.volt", p.CallsignId);
            Assert.AreEqual(PilotVision.Volt, p.Vision());
            Assert.AreEqual("VOLT", p.CallsignWord());
            Assert.IsTrue(p.Id.StartsWith("pilot.callsign.volt#"));
            Assert.AreNotEqual(p.SpeciesOne, p.SpeciesTwo, "two different animals");
            Assert.AreNotEqual(AnimalSpecies.None, p.SpeciesOne);
            StringAssert.StartsWith("Human / ", p.SpeciesCombo());
            Assert.AreNotEqual(FactionType.None, p.FactionType);
            Assert.Contains(p.FactionType, FactionCatalog.Recruitable);
            Assert.AreEqual(p.FactionType, FactionCatalog.TypeOf(p.FactionId));
            Assert.AreEqual(p.FactionName, FactionCatalog.NameOf(p.FactionId));
            StringAssert.Contains(p.GivenName, p.Backstory);
            StringAssert.Contains("VOLT", p.Backstory, "the story says what their callsign sees");
            Assert.IsTrue(Printable(p.Backstory) && Printable(p.DisplayName()));
            Assert.AreEqual(JsonConvert.SerializeObject(p), JsonConvert.SerializeObject(PilotGenerator.Roll(PilotVision.Volt, 99)), "deterministic");
            Assert.IsNull(PilotGenerator.Roll(PilotVision.None, 1));
        }

        [Test]
        public void Pilots_VaryInNameHeritageFactionAndStory()
        {
            var ids = new HashSet<string>();
            var names = new HashSet<string>();
            var combos = new HashSet<string>();
            var factions = new HashSet<string>();
            var stories = new HashSet<string>();
            for (ulong seed = 1; seed <= 2000; seed++)
            {
                var p = PilotGenerator.Roll(PilotVision.Bandit, seed * 0x9E3779B97F4A7C15UL);
                Assert.IsTrue(ids.Add(p.Id));
                names.Add(p.DisplayName());
                combos.Add(p.SpeciesCombo());
                factions.Add(p.FactionId);
                stories.Add(p.Backstory);
                Assert.AreNotEqual(p.SpeciesOne, p.SpeciesTwo);
            }
            Assert.Greater(names.Count, 1950, "names almost never repeat");
            Assert.Greater(combos.Count, 900, "heritage spans the animal pairs");
            Assert.GreaterOrEqual(factions.Count, 20, "every founding faction of the recruitable types appears");
            Assert.AreEqual(2000, stories.Count, "no two stories are the same");
        }

        [Test]
        public void FactionCatalog_TypesBelongToDomains_AndIdsRoundTrip()
        {
            Assert.AreEqual(Domains.Ruby, FactionCatalog.DomainOf(FactionType.Pirate));
            Assert.AreEqual(Domains.Gold, FactionCatalog.DomainOf(FactionType.Merchant));
            Assert.AreEqual(Domains.Jade, FactionCatalog.DomainOf(FactionType.Aristocrat));
            Assert.AreEqual("faction.pirate.black-tide", FactionCatalog.IdOf(FactionType.Pirate, "Black Tide"));
            Assert.AreEqual(FactionType.BountyHunter, FactionCatalog.TypeOf("faction.bountyhunter.writ-and-hook"));
            Assert.AreEqual("Writ and Hook", FactionCatalog.NameOf("faction.bountyhunter.writ-and-hook"));
            foreach (FactionType t in Enum.GetValues(typeof(FactionType)))
                if (t != FactionType.None) Assert.IsNotEmpty(FactionCatalog.FoundingNames(t), $"{t} has founding factions");
        }

        // ── The inventory ───────────────────────────────────────────────

        [Test]
        public void Crew_HoldsOnePilotPerCallsign()
        {
            var inv = new AccountInventory();
            var volt = inv.MintPilot(PilotVision.Volt, FactionType.None, "test", 0, Seeds());
            Assert.IsNotNull(volt);
            Assert.IsNull(inv.MintPilot(PilotVision.Volt, FactionType.None, "test", 0, Seeds(50)), "a second VOLT is refused");
            Assert.AreEqual(AccountInventory.AddResult.CallsignTaken, inv.TryAddPilot(PilotGenerator.Roll(PilotVision.Volt, 777)));
            Assert.AreEqual(AccountInventory.AddResult.DuplicateId, inv.TryAddPilot(volt));
            Assert.AreEqual(AccountInventory.AddResult.Invalid, inv.TryAddPilot(new PilotRecord { Id = "pilot.callsign.volt" }), "no seed");
            Assert.AreEqual(AccountInventory.AddResult.Invalid, inv.TryAddPilot(new PilotRecord { Id = "artifact.javelin#0000000000000001" }));
            Assert.IsNotNull(inv.MintPilot(PilotVision.Bandit, FactionType.Pirate, "test", 0, Seeds(9)));
            Assert.AreEqual(FactionType.Pirate, inv.PilotWithCallsign("pilot.callsign.bandit").FactionType, "a recruit from a faction keeps it");
            Assert.AreSame(volt, inv.ResolvePilot("pilot.callsign.volt"), "a legacy callsign id is the crew's VOLT");
            Assert.AreSame(volt, inv.ResolvePilot("pilot.callsign.volt#00000000000000ff"), "another crew's VOLT reads as ours");
            Assert.IsNull(inv.ResolvePilot("pilot.callsign.citadel"), "no CITADEL in this crew");
        }

        [Test]
        public void Captain_DecidesTheFaction_TheRestAreAllies()
        {
            var inv = new AccountInventory();
            var a = inv.MintPilot(PilotVision.Volt, FactionType.Merchant, "t", 0, Seeds(1));
            var b = inv.MintPilot(PilotVision.Talon, FactionType.Hunter, "t", 0, Seeds(100));
            Assert.AreEqual(string.Empty, inv.PrimaryFactionId(), "no captain, no faction");
            Assert.IsTrue(inv.PromoteToCaptain(b.Id));
            Assert.AreEqual(b.FactionId, inv.PrimaryFactionId());
            var allies = new List<string>();
            inv.AllyFactionIds(allies);
            CollectionAssert.AreEqual(new[] { a.FactionId }, allies);
            StringAssert.Contains("captain", b.Chronicle[b.Chronicle.Count - 1]);
            Assert.IsFalse(inv.PromoteToCaptain("pilot.callsign.ivy#0000000000000001"));
            inv.RemovePilot(b.Id);
            Assert.IsNull(inv.Captain(), "a discontinued captain leaves the chair empty");
        }

        [Test]
        public void Hangar_SeatsAndSlotsOnlyWhatIsHeld_OneVesselPerArtifact()
        {
            var inv = new AccountInventory();
            var p = inv.MintPilot(PilotVision.Volt, FactionType.None, "t", 0, Seeds());
            var a = inv.MintArtifact(Javelin(), "t", 0, Seeds(10));
            Assert.IsFalse(inv.SetHangarPilot(VesselClassType.Sparrow, "pilot.callsign.ivy#0000000000000001"));
            Assert.IsTrue(inv.SetHangarPilot(VesselClassType.Sparrow, p.Id));
            Assert.IsTrue(inv.SetHangarArtifact(VesselClassType.Sparrow, a.Id));
            Assert.IsTrue(inv.SetHangarArtifact(VesselClassType.Manta, a.Id));
            Assert.IsNull(inv.HangarFor(VesselClassType.Sparrow).ArtifactId, "it moved to the Manta");
            Assert.AreEqual(a.Id, inv.HangarFor(VesselClassType.Manta).ArtifactId);
            inv.RemoveArtifact(a.Id);
            Assert.IsNull(inv.HangarFor(VesselClassType.Manta).ArtifactId, "a lost artifact leaves its slot");
        }

        [Test]
        public void AdoptSquads_PointsLegacyIdsAtThePeopleAndArtifactsHeld()
        {
            var inv = new AccountInventory();
            var volt = inv.MintPilot(PilotVision.Volt, FactionType.None, "t", 0, Seeds(1));
            var bandit = inv.MintPilot(PilotVision.Bandit, FactionType.None, "t", 0, Seeds(30));
            var jav1 = inv.MintArtifact(Javelin(), "t", 0, Seeds(60));
            var jav2 = inv.MintArtifact(Javelin(), "t", 0, Seeds(90));

            var mine = new SavedSquad("Mine",
                new SquadSlot(VesselClassType.Sparrow, Element.Mass, "pilot.callsign.volt", "artifact.javelin"),
                new SquadSlot(VesselClassType.Manta, Element.Space, "pilot.callsign.bandit", "artifact.javelin"),
                new SquadSlot(VesselClassType.Rhino, Element.Time, "pilot.callsign.ivy", "artifact.javelin#00000000deadbeef"));
            var joined = new SavedSquad("Theirs",
                new SquadSlot(VesselClassType.Sparrow, Element.Mass, "pilot.callsign.volt", "artifact.javelin"),
                default, default) { OwnerId = "friend", MySeat = 1 };
            var book = new SquadBook();
            book.Add(mine);
            book.Add(joined);

            Assert.IsTrue(inv.AdoptSquads(book));
            Assert.AreEqual(volt.Id, mine.Leader.PilotId);
            Assert.AreEqual(bandit.Id, mine.RogueOne.PilotId);
            Assert.AreEqual(jav1.Id, mine.Leader.ArtifactId);
            Assert.AreEqual(jav2.Id, mine.RogueOne.ArtifactId, "each vessel its own artifact");
            Assert.AreEqual("pilot.callsign.ivy", mine.RogueTwo.PilotId, "no IVY in the crew: kept for FillPilots to repair");
            Assert.IsNull(mine.RogueTwo.ArtifactId, "an artifact the hoard no longer holds is dropped");
            Assert.AreEqual("pilot.callsign.volt", joined.Leader.PilotId, "a joined squad keeps its owner's ids");
            Assert.IsFalse(inv.AdoptSquads(book), "idempotent");
        }

        [Test]
        public void Normalize_RepairsABrokenRecord()
        {
            var inv = new AccountInventory();
            var volt = inv.MintPilot(PilotVision.Volt, FactionType.None, "t", 0, Seeds(1));
            var a = inv.MintArtifact(Javelin(), "t", 0, Seeds(10));
            inv.Pilots.Add(null);
            inv.Pilots.Add(volt.Clone());                                   // a repeated id
            inv.Pilots.Add(PilotGenerator.Roll(PilotVision.Volt, 555));      // a second VOLT
            inv.Pilots.Add(new PilotRecord { Id = "garbage" });
            inv.Artifacts.Add(a.Clone());
            inv.Artifacts.Add(new ArtifactRecord { Id = "artifact.javelin#0000000000000005", Element = Element.Omni });
            var broken = ArtifactGenerator.Roll(Hydra(), 3);
            broken.Invested = -9;
            broken.Ceiling = float.NaN;
            broken.HalfCrystals = 0;
            inv.Artifacts.Add(broken);
            inv.CaptainId = "pilot.callsign.ivy#0000000000000001";
            inv.Hangar.Add(new HangarLoadout { Vessel = VesselClassType.Manta, PilotId = "nobody", ArtifactId = a.Id });
            inv.Hangar.Add(new HangarLoadout { Vessel = VesselClassType.Manta });
            inv.Hangar.Add(new HangarLoadout { Vessel = VesselClassType.Rhino, ArtifactId = a.Id });
            inv.Artifacts.Add(null);
            inv.Hangar.Add(null);

            inv.Normalize();
            Assert.AreEqual(1, inv.Pilots.Count);
            Assert.AreEqual(2, inv.Artifacts.Count);
            Assert.AreEqual(0, broken.Invested);
            Assert.AreEqual(ArtifactLevels.MaxLevel, broken.Ceiling);
            Assert.Greater(broken.HalfCrystals, 0f);
            Assert.IsNull(inv.CaptainId);
            Assert.AreEqual(2, inv.Hangar.Count);
            Assert.IsNull(inv.HangarFor(VesselClassType.Manta).PilotId);
            Assert.IsNull(inv.HangarFor(VesselClassType.Rhino).ArtifactId, "an artifact rides one vessel");
            string once = JsonConvert.SerializeObject(inv);
            inv.Normalize();
            Assert.AreEqual(once, JsonConvert.SerializeObject(inv), "idempotent");
        }

        [Test]
        public void CloudRecord_RoundTripsThroughJson()
        {
            var data = new InventoryCloudData();
            var inv = data.Inventory;
            var p = inv.MintPilot(PilotVision.Galleon, FactionType.Pirate, "t", 1730937600000, Seeds(3));
            var a = inv.MintArtifact(Hydra(), "voyage", 1730937600000, Seeds(4));
            inv.Feed(a.Id, a.Element, 77);
            inv.PromoteToCaptain(p.Id);
            inv.SetHangarArtifact(VesselClassType.Serpent, a.Id);
            inv.StarterGranted = true;

            string json = JsonConvert.SerializeObject(data);
            StringAssert.DoesNotContain("\"Levels\"", json, "computed answers are methods, not saved");
            var back = JsonConvert.DeserializeObject<InventoryCloudData>(json);
            back.Inventory.Normalize();
            Assert.AreEqual(json, JsonConvert.SerializeObject(back), "nothing lost or changed");
            Assert.AreEqual(a.AttunedLevel(), back.Inventory.Artifact(a.Id).AttunedLevel());
            Assert.AreEqual(p.Backstory, back.Inventory.Captain().Backstory);

            var empty = JsonConvert.DeserializeObject<InventoryCloudData>("{\"SchemaVersion\":1}");
            Assert.IsNotNull(empty.Inventory);
            empty.Inventory.Normalize();
            Assert.IsEmpty(empty.Inventory.Pilots);
        }
    }
}
#endif
