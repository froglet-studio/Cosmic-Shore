#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The starter crew IS the painted pilots (<see cref="PaintedCrewSO"/>; Garrett, 2026-11-12: "none of the stories
    /// matched the art"; 2026-11-16: "lets get all 50 into the game. it is ok if we duplicate callsign everything else
    /// will be the different"): every painted pilot marked starter joins a crew once, under a fixed id, takes the
    /// pictured person's name, animals, faction, home and story, and keeps what a save hangs on (id, callsign, origin,
    /// chronicle). A recruit keeps their own record. The shipped asset is the one
    /// <c>Tools/Build/author_pilot_scene_assets.py</c> writes from <c>Docs/Pilots/art/scenes/people.json</c>.
    /// </summary>
    [TestFixture]
    public class PaintedCrewTests
    {
        const string CatalogPath = "Assets/_SO_Assets/Pilots/PilotSceneCatalog.asset";

        static Func<ulong> Seeds(ulong first)
        {
            ulong next = first;
            return () => next++;
        }

        static PaintedCrewSO Crew()
        {
            var crew = PaintedCrewSO.Load();
            Assert.IsNotNull(crew, "Resources/Pilots/PaintedCrew is missing; run Tools/Build/author_pilot_scene_assets.py");
            return crew;
        }

        static string NameOf(PaintedCrewSO.Person p) => string.IsNullOrEmpty(p.family) ? p.given : p.given + " " + p.family;

        static int Starters(PaintedCrewSO crew)
        {
            int n = 0;
            foreach (var p in crew.People) if (p != null && p.starter) n++;
            return n;
        }

        [Test]
        public void EveryPaintedScene_HasItsPerson_UnderTheSameName()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PilotSceneCatalogSO>(CatalogPath);
            Assert.IsNotNull(catalog);
            var crew = Crew();
            Assert.AreEqual(catalog.Scenes.Count, crew.People.Count, "one person a painted scene");
            var names = new HashSet<string>();
            foreach (var scene in catalog.Scenes)
            {
                Assert.IsTrue(crew.TryGet(scene.key, out var person), $"{scene.key} has no painted person");
                Assert.AreEqual(scene.callsign, person.callsign, $"{scene.key}'s callsign");
                Assert.AreEqual(scene.painted, NameOf(person), $"{scene.key}'s name");
                Assert.IsTrue(names.Add(NameOf(person)), $"two painted pilots are called {NameOf(person)}");
                Assert.AreNotEqual(person.speciesOne, person.speciesTwo);
                Assert.AreNotEqual(AnimalSpecies.None, person.speciesOne);
                Assert.AreNotEqual(FactionType.None, person.factionType);
                Assert.AreEqual(person.factionType, FactionCatalog.TypeOf(FactionCatalog.IdOf(person.factionType, person.factionName)));
                StringAssert.Contains("\n\n", person.story, $"{scene.key}'s story is short paragraphs");
            }
        }

        [Test]
        public void Grant_GivesAFreshCrewEveryStarter_UnderTheSameIdsInEveryCrew()
        {
            var crew = Crew();
            var a = new AccountInventory();
            var b = new AccountInventory();
            Assert.IsTrue(crew.Grant(a, 1));
            Assert.IsTrue(crew.Grant(b, 2));
            Assert.AreEqual(Starters(crew), a.Pilots.Count, "every starter joins");
            Assert.Greater(a.Pilots.Count, Enum.GetValues(typeof(PilotVision)).Length - 1,
                "the crew is larger than the callsigns: callsigns are shared");
            for (int i = 0; i < a.Pilots.Count; i++)
                Assert.AreEqual(a.Pilots[i].Id, b.Pilots[i].Id, "a squad or a friend's card names the same person");

            foreach (var pilot in a.Pilots)
            {
                Assert.IsTrue(crew.TryGet(pilot.PaintedKey, out var p), $"{pilot.Id} names no painted pilot");
                Assert.AreEqual(NameOf(p), pilot.DisplayName());
                Assert.AreEqual(p.callsign, pilot.Vision());
                Assert.AreEqual(PaintedCrewSO.StarterOrigin, pilot.Origin);
            }
            Assert.IsFalse(crew.Grant(a, 3), "a second grant changes nothing");
        }

        [Test]
        public void Grant_MakesAnOldStarterItsCallsignsOwnPilot_AndKeepsItsId()
        {
            var crew = Crew();
            var inv = new AccountInventory();
            var old = inv.MintPilot(PilotVision.Volt, FactionType.Pirate, PaintedCrewSO.StarterOrigin, 1, Seeds(5));
            old.AddChronicle("Joined the crew.");
            string id = old.Id;

            crew.Grant(inv, 2);
            Assert.AreEqual("VOLT", old.PaintedKey, "the starter saved before keys is VOLT's own painted pilot");
            Assert.AreEqual(id, old.Id, "a squad that seats them still finds them");
            Assert.AreEqual(1, old.Chronicle.Count, "the story they grew stays");
            Assert.AreSame(old, inv.PaintedPilot("VOLT"));
            int volts = 0;
            foreach (var p in inv.Pilots) if (p.PaintedKey == "VOLT") volts++;
            Assert.AreEqual(1, volts, "VOLT is not granted a second time");
            Assert.AreEqual(Starters(crew), inv.Pilots.Count);
        }

        [Test]
        public void Grant_NeverGivesBackAPilotTheCrewWasGrantedOnce()
        {
            var crew = Crew();
            var inv = new AccountInventory();
            crew.Grant(inv, 1);
            var gone = inv.PaintedPilot("Flux");
            Assert.IsNotNull(gone);
            inv.Pilots.Remove(gone);
            Assert.IsFalse(crew.Grant(inv, 2), "a pilot who left is not granted again");
            Assert.IsNull(inv.PaintedPilot("Flux"));
        }

        [Test]
        public void Grant_LeavesOutAPersonNotMarkedStarter()
        {
            var crew = ScriptableObject.CreateInstance<PaintedCrewSO>();
            try
            {
                JsonUtility.FromJsonOverwrite(
                    "{\"people\":[" +
                    "{\"key\":\"Kept\",\"callsign\":" + (int)PilotVision.Volt + ",\"starter\":true,\"given\":\"Kept\"}," +
                    "{\"key\":\"Later\",\"callsign\":" + (int)PilotVision.Volt + ",\"starter\":false,\"given\":\"Later\"}]}",
                    crew);
                var inv = new AccountInventory();
                Assert.IsTrue(crew.Grant(inv, 1));
                Assert.AreEqual(1, inv.Pilots.Count);
                Assert.IsNotNull(inv.PaintedPilot("Kept"));
                Assert.IsNull(inv.PaintedPilot("Later"), "a crew grows to them one pilot at a time");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(crew);
            }
        }

        [Test]
        public void Paint_ReadsThePaintedPilotByKey_AndARecruitKeepsTheirOwnRecord()
        {
            var crew = Crew();
            var inv = new AccountInventory();
            var p = inv.MintPilot(PilotVision.Zodiac, FactionType.Pirate, PaintedCrewSO.StarterOrigin, 1, Seeds(5));
            p.PaintedKey = "Astrofoil";
            Assert.IsTrue(crew.TryGet("Astrofoil", out var painted));
            Assert.IsTrue(crew.Paint(p), "a rolled record changes");
            Assert.AreEqual(NameOf(painted), p.DisplayName());
            Assert.AreEqual(painted.speciesOne, p.SpeciesOne);
            Assert.AreEqual(painted.speciesTwo, p.SpeciesTwo);
            Assert.AreEqual(painted.factionType, p.FactionType);
            Assert.AreEqual(painted.factionName, FactionCatalog.NameOf(p.FactionId));
            Assert.AreEqual(painted.homeworld, p.Homeworld);
            Assert.AreEqual(painted.story, p.Backstory);
            Assert.IsFalse(crew.Paint(p), "a second read changes nothing");

            var recruit = inv.MintPilot(PilotVision.Volt, FactionType.Pirate, "recruit", 1, Seeds(9));
            string name = recruit.DisplayName();
            Assert.IsFalse(crew.Paint(recruit), "a recruit names no painted pilot");
            Assert.AreEqual(name, recruit.DisplayName());
            Assert.IsFalse(crew.Paint(null));
        }

        [Test]
        public void Normalize_KeepsAStorysParagraphs()
        {
            var inv = new AccountInventory();
            var p = inv.MintPilot(PilotVision.Volt, FactionType.Pirate, PaintedCrewSO.StarterOrigin, 1, Seeds(5));
            p.PaintedKey = "VOLT";
            Crew().Paint(p);
            string story = p.Backstory;
            p.Backstory = story + "\u0007";
            inv.Normalize();
            Assert.AreEqual(story, p.Backstory, "line breaks stay; what the font cannot draw goes");
        }
        // ── A crew of one's own (Garrett, 2026-11-18/19) ─────────────────────

        static readonly string[] PlayerA =
        {
            "ENSIGN", "Veveola", "ANVIL", "Flux", "Astrofoil", "Quilox", "Ovex", "Zesondo", "CITADEL", "Krush", "Bushy",
            "Briola", "Belsoth", "WINGMAN", "Delsoko", "Mirisa", "Uthael", "Zebeel",
        };

        [Test]
        public void RandomCrew_IsRosterPysRandomCrew_BitForBit()
        {
            // Tools/Build/pilot_art/roster.py random_crew("player-a"), in callsign order.
            CollectionAssert.AreEqual(PlayerA, Crew().RandomCrew("player-a"));
            CollectionAssert.AreEqual(Crew().RandomCrew("player-b"), Crew().RandomCrew("player-b"), "the same every time");
            CollectionAssert.IsEmpty(Crew().RandomCrew(""));
        }

        [Test]
        public void DealtCrews_AreSeven_OnePilotACallsign_NoPilotInTwo()
        {
            var crew = Crew();
            Assert.AreEqual(7, crew.Crews.Count);
            var seen = new HashSet<string>();
            foreach (var dealt in crew.Crews)
            {
                Assert.AreEqual(Callsigns.All.Count, dealt.members.Count);
                for (int i = 0; i < dealt.members.Count; i++)
                {
                    Assert.IsTrue(crew.TryGet(dealt.members[i], out var p), $"{dealt.members[i]} is no painted pilot");
                    Assert.AreEqual(Callsigns.All[i], p.callsign, $"{dealt.members[i]} sits on its callsign");
                    Assert.IsTrue(seen.Add(dealt.members[i]), $"{dealt.members[i]} is in two dealt crews");
                }
            }
        }

        [Test]
        public void Grant_ForAPlayer_DealsEighteen_AndOldStartersLeave_TheirHeirTakingTheChair()
        {
            var crew = Crew();
            var inv = new AccountInventory();
            crew.Grant(inv, 1);                                   // the build before crews: every painted pilot
            var outsider = inv.PaintedPilot("VOLT");               // player-a's VOLT is Flux
            Assert.IsNotNull(outsider);
            inv.CaptainId = outsider.Id;
            inv.SetHangarPilot(VesselClassType.Sparrow, outsider.Id);

            Assert.IsTrue(crew.Grant(inv, 2, "player-a"));
            CollectionAssert.AreEqual(PlayerA, inv.Crew);
            Assert.AreEqual(PlayerA.Length, inv.Pilots.Count, "only the crew stays");
            foreach (var key in PlayerA) Assert.IsNotNull(inv.PaintedPilot(key), $"{key} joined");
            var heir = inv.PaintedPilot("Flux");
            Assert.AreEqual(heir.Id, inv.CaptainId, "the crew's VOLT takes the captain's chair");
            Assert.AreEqual(heir.Id, inv.HangarFor(VesselClassType.Sparrow).PilotId, "and the Hangar seat");
            Assert.IsFalse(crew.Grant(inv, 3, "someone-else"), "a crew is dealt once");
        }

        [Test]
        public void Claim_DealsThatCrew_AndARecruitStays()
        {
            var crew = Crew();
            var inv = new AccountInventory();
            crew.Grant(inv, 1, "player-a");
            var recruit = inv.MintPilot(PilotVision.Volt, FactionType.Pirate, "recruit", 1, Seeds(9));
            Assert.IsNull(recruit, "the crew already carries VOLT");
            Assert.IsTrue(crew.Claim(inv, 1, 2));
            Assert.AreEqual(2, inv.ClaimedCrew);
            CollectionAssert.AreEqual(crew.Crews[1].members, inv.Crew);
            Assert.AreEqual(crew.Crews[1].members.Count, inv.Pilots.Count);
            Assert.IsFalse(crew.Claim(inv, crew.Crews.Count, 3), "no such crew");
            Assert.IsTrue(crew.Claim(inv, 0, 4), "a crew can be claimed again");
            foreach (var key in crew.Crews[0].members) Assert.IsNotNull(inv.PaintedPilot(key), $"{key} came back");
        }

        [Test]
        public void ClaimCode_HashesAsCrewCodesJson_AndOnlyACodeAsks()
        {
            // Docs/Pilots/art/roster/crew_codes.json: SHA-256 of "cosmic-shore/crew-code/" + the code, upper case,
            // letters and digits only (Python: hashlib.sha256(b"cosmic-shore/crew-code/AB12CD34EF56")).
            Assert.AreEqual("AB12CD34EF56", PaintedCrewSO.CodeKey(" ab12-cd34 ef56 "));
            Assert.AreEqual("e1cd60f94176915222b5571e66cf0d5474a598cdc011293bf1f2b68c8d4ddd32", PaintedCrewSO.CodeHash("ab12-cd34-ef56"));
            var crew = Crew();
            foreach (var dealt in crew.Crews)
                StringAssert.IsMatch("^[0-9a-f]{64}$", dealt.codeHash ?? "", "every dealt crew can be claimed");
            Assert.AreEqual(-1, crew.CrewForCode("AB12-CD34-EF56"), "a code no crew holds claims nothing");
            Assert.AreEqual(-1, crew.CrewForCode("Garrett"), "a display name is never a code");
            Assert.AreEqual(-1, crew.CrewForCode(""));
        }

        [Test]
        public void CrewAvatars_OfferTheCrew_AndKeepTheCallsign()
        {
            var crew = Crew();
            var icons = new List<ProfileIcon>();
            int id = 1;
            foreach (var p in crew.People) icons.Add(new ProfileIcon(p.key, id++, null));
            var mine = new List<string>(PlayerA);

            var offered = CrewAvatars.Offered(icons, mine);
            Assert.AreEqual(mine.Count, offered.Count);
            for (int i = 0; i < mine.Count; i++) Assert.AreEqual(mine[i], offered[i].Name);
            Assert.AreEqual(icons.Count, CrewAvatars.Offered(icons, new List<string>()).Count, "no crew yet: every avatar");

            int voltAvatar = icons.Find(i => i.Name == "VOLT").Id;           // not player-a's
            int fluxAvatar = icons.Find(i => i.Name == "Flux").Id;           // player-a's VOLT
            Assert.AreEqual(fluxAvatar, CrewAvatars.Consistent(voltAvatar, icons, mine, crew));
            Assert.AreEqual(fluxAvatar, CrewAvatars.Consistent(fluxAvatar, icons, mine, crew), "a crew avatar stays");
            Assert.AreEqual(offered[0].Id, CrewAvatars.Consistent(-5, icons, mine, crew), "an unknown avatar: the first");
        }
    }
}
#endif