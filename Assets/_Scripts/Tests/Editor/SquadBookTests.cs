#if UNITY_EDITOR
using CosmicShore.Core;
using CosmicShore.Data;
using Newtonsoft.Json;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The saved-squads record (Docs/PILOTS_AND_ARTIFACTS.md §1): a book of named squads with one
    /// active. The active squad is what the pre-game flow plays, so every add and remove must keep it
    /// pointing at the same squad, and a load must repair whatever a partial cloud record carries.
    /// Squad names reach a TMP label whose font has printable ASCII only, so the book sanitizes them.
    /// </summary>
    [TestFixture]
    public class SquadBookTests
    {
        static SavedSquad Squad(string name) => new SavedSquad(name,
            new SquadSlot(VesselClassType.Manta, Element.Mass),
            new SquadSlot(VesselClassType.Dolphin, Element.Charge, "pilot.dolphin.seed-caster"),
            new SquadSlot(VesselClassType.Sparrow, Element.Time, null, "artifact.sparrow.afterburner"));

        [Test]
        public void KeepCardsOwn_SwapsTheSamePilotAndClearsTheSameArtifact()
        {
            // The leader just took ENSIGN (it had CITADEL) and Javelin.
            var squad = new SavedSquad("s",
                new SquadSlot(VesselClassType.Manta, Element.Mass, "pilot.ensign", "artifact.javelin"),
                new SquadSlot(VesselClassType.Dolphin, Element.Charge, "pilot.ensign", "artifact.aegis"),
                new SquadSlot(VesselClassType.Sparrow, Element.Time, "pilot.volt", "artifact.javelin"));

            Assert.IsTrue(squad.KeepCardsOwn(0, "pilot.citadel"));
            Assert.AreEqual("pilot.ensign", squad.Leader.PilotId);
            Assert.AreEqual("pilot.citadel", squad.RogueOne.PilotId, "the other vessel takes the leader's old pilot");
            Assert.AreEqual("artifact.aegis", squad.RogueOne.ArtifactId);
            Assert.AreEqual("pilot.volt", squad.RogueTwo.PilotId);
            Assert.IsNull(squad.RogueTwo.ArtifactId);
            Assert.IsFalse(squad.KeepCardsOwn(0, "pilot.citadel"));
        }

        [Test]
        public void FillPilots_GivesEveryVesselItsOwnPilot()
        {
            var squad = new SavedSquad("s",
                new SquadSlot(VesselClassType.Manta, Element.Mass, "pilot.volt"),
                new SquadSlot(VesselClassType.Dolphin, Element.Charge),
                new SquadSlot(VesselClassType.Sparrow, Element.Time, "pilot.volt"));
            var ids = new[] { "pilot.ensign", "pilot.volt", "pilot.citadel" };

            Assert.IsTrue(squad.FillPilots(ids));
            Assert.AreEqual("pilot.volt", squad.Leader.PilotId);
            Assert.AreEqual("pilot.ensign", squad.RogueOne.PilotId);
            Assert.AreEqual("pilot.citadel", squad.RogueTwo.PilotId, "a repeated pilot is replaced too");
            Assert.IsFalse(squad.FillPilots(ids));
        }

        [Test]
        public void KeepCardsOwn_LeavesEmptyCardsAlone()
        {
            var squad = new SavedSquad("s",
                new SquadSlot(VesselClassType.Manta, Element.Mass),
                new SquadSlot(VesselClassType.Dolphin, Element.Charge),
                new SquadSlot(VesselClassType.Sparrow, Element.Time));
            Assert.IsFalse(squad.KeepCardsOwn(0, null));
        }

        [Test]
        public void EmptyBook_HasNoActiveSquad()
        {
            var book = new SquadBook();
            Assert.IsNull(book.GetActive());
            Assert.IsFalse(book.TrySetActive(0));
        }

        [Test]
        public void FirstSquadAdded_BecomesActive_LaterOnesDoNot()
        {
            var book = new SquadBook();
            var first = book.Add(Squad("Skim wing"));
            book.Add(Squad("Breakers"));

            Assert.AreSame(first, book.GetActive());
            Assert.AreEqual(0, book.ActiveIndex);
        }

        [Test]
        public void TrySetActive_SelectsThatSquad_AndRejectsOutOfRange()
        {
            var book = new SquadBook();
            book.Add(Squad("A"));
            var b = book.Add(Squad("B"));

            Assert.IsTrue(book.TrySetActive(1));
            Assert.AreSame(b, book.GetActive());
            Assert.IsFalse(book.TrySetActive(2));
            Assert.IsFalse(book.TrySetActive(-1));
            Assert.AreSame(b, book.GetActive(), "a rejected index must not move the active squad");
        }

        [Test]
        public void RemovingASquadBeforeTheActiveOne_KeepsTheSameSquadActive()
        {
            var book = new SquadBook();
            book.Add(Squad("A"));
            book.Add(Squad("B"));
            var c = book.Add(Squad("C"));
            book.TrySetActive(2);

            Assert.IsTrue(book.RemoveAt(0));
            Assert.AreSame(c, book.GetActive());
            Assert.AreEqual(1, book.ActiveIndex);
        }

        [Test]
        public void RemovingTheActiveSquad_ActivatesItsSuccessor_OrTheNewLast()
        {
            var book = new SquadBook();
            book.Add(Squad("A"));
            book.Add(Squad("B"));
            var c = book.Add(Squad("C"));
            book.TrySetActive(1);

            book.RemoveAt(1);
            Assert.AreSame(c, book.GetActive(), "the successor slides into the removed index");

            book.RemoveAt(1);
            Assert.AreEqual("A", book.GetActive().Name, "removing the last squad activates the new last one");

            book.RemoveAt(0);
            Assert.IsNull(book.GetActive());
            Assert.AreEqual(0, book.ActiveIndex);
            Assert.IsFalse(book.RemoveAt(0));
        }

        [Test]
        public void Rename_SanitizesAndRejectsOutOfRange()
        {
            var book = new SquadBook();
            book.Add(Squad("A"));

            Assert.IsTrue(book.Rename(0, "  Night   shift  "));
            Assert.AreEqual("Night shift", book.Squads[0].Name);
            Assert.IsFalse(book.Rename(1, "nope"));
        }

        [Test]
        public void SanitizeName_KeepsOnlyWhatTheUiFontCanDraw()
        {
            Assert.AreEqual("Squad 1", SquadBook.SanitizeName(null, 0));
            Assert.AreEqual("Squad 3", SquadBook.SanitizeName("   ", 2));
            Assert.AreEqual("Squad 2", SquadBook.SanitizeName("✓★", 1), "a name of only undrawable glyphs falls back");
            Assert.AreEqual("Skim wing", SquadBook.SanitizeName("Skim ✓ wing", 0));
            Assert.AreEqual("a b", SquadBook.SanitizeName("a\t\tb", 0));

            string longName = SquadBook.SanitizeName(new string('x', 60), 0);
            Assert.AreEqual(SquadBook.MaxNameLength, longName.Length);

            string cutAtSpace = SquadBook.SanitizeName(new string('x', SquadBook.MaxNameLength - 1) + " tail", 0);
            Assert.IsFalse(cutAtSpace.EndsWith(" "), "a cut must not leave a trailing space");
        }

        [Test]
        public void Normalize_RepairsAPartialRecord()
        {
            var book = new SquadBook { Squads = null, ActiveIndex = 7 };
            book.Normalize();
            Assert.IsNotNull(book.Squads);
            Assert.AreEqual(0, book.ActiveIndex);

            book.Squads.Add(null);
            book.Squads.Add(new SavedSquad { Name = "" });
            book.Squads.Add(null);
            book.Squads.Add(new SavedSquad { Name = "Kept" });
            book.ActiveIndex = 3;
            book.Normalize();

            Assert.AreEqual(2, book.Squads.Count, "null entries are dropped");
            Assert.AreEqual("Squad 1", book.Squads[0].Name);
            Assert.AreEqual("Kept", book.Squads[1].Name);
            Assert.AreEqual(0, book.ActiveIndex, "an index that pointed past the end resets");

            book.ActiveIndex = 1;
            book.Normalize();
            Assert.AreEqual(1, book.ActiveIndex, "a valid index survives, so Normalize is idempotent");
        }

        [Test]
        public void SavedSquad_SlotIndexing_MatchesLeaderRogueOneRogueTwo()
        {
            var squad = Squad("A");
            Assert.AreEqual(VesselClassType.Manta, squad.GetSlot(0).Vessel);
            Assert.AreEqual(VesselClassType.Dolphin, squad.GetSlot(1).Vessel);
            Assert.AreEqual(VesselClassType.Sparrow, squad.GetSlot(2).Vessel);

            squad.SetSlot(2, new SquadSlot(VesselClassType.Rhino, Element.Space));
            Assert.AreEqual(VesselClassType.Rhino, squad.RogueTwo.Vessel);

            Assert.Throws<System.ArgumentOutOfRangeException>(() => squad.GetSlot(3));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => squad.SetSlot(-1, default));
            Assert.AreEqual(SavedSquad.SlotCount, 3);
        }

        [Test]
        public void SquadSlot_ChangingVessel_ClearsThePerVesselPicks()
        {
            var slot = new SquadSlot(VesselClassType.Dolphin, Element.Charge, "pilot.dolphin.seed-caster", "artifact.x");
            var moved = slot.WithVessel(VesselClassType.Rhino);

            Assert.AreEqual(VesselClassType.Rhino, moved.Vessel);
            Assert.AreEqual(Element.Charge, moved.Element, "the element is the slot's own choice and stays");
            Assert.IsTrue(moved.UsesDefaultPilot());
            Assert.IsFalse(moved.CarriesArtifact());
        }

        [Test]
        public void CloudRecord_RoundTripsThroughJson_WithoutComputedFields()
        {
            var data = new SquadCloudData();
            data.Book.Add(Squad("Skim wing"));
            data.Book.Add(Squad("Breakers"));
            data.Book.TrySetActive(1);

            string json = JsonConvert.SerializeObject(data);
            StringAssert.DoesNotContain("UsesDefaultPilot", json, "computed answers must not be written to the record");
            StringAssert.DoesNotContain("CarriesArtifact", json);

            var back = JsonConvert.DeserializeObject<SquadCloudData>(json);
            Assert.AreEqual(SquadCloudData.CurrentSchemaVersion, back.SchemaVersion);
            Assert.AreEqual(2, back.Book.Squads.Count);
            Assert.AreEqual(1, back.Book.ActiveIndex);
            Assert.AreEqual("Breakers", back.Book.GetActive().Name);
            Assert.AreEqual("pilot.dolphin.seed-caster", back.Book.Squads[0].RogueOne.PilotId);
            Assert.AreEqual("artifact.sparrow.afterburner", back.Book.Squads[0].RogueTwo.ArtifactId);
            Assert.AreEqual(VesselClassType.Sparrow, back.Book.Squads[0].RogueTwo.Vessel);
        }

        [Test]
        public void CloudRecord_InTheRetiredOneSquadShape_LoadsAsAnEmptyBook()
        {
            // Schema 0 was a mirror nothing ever wrote; its fields must deserialize into nothing.
            const string legacy = "{\"SquadLeaderClass\":1,\"SquadLeaderElement\":2,\"RogueOneClass\":2," +
                                  "\"RogueOneElement\":1,\"RogueTwoClass\":3,\"RogueTwoElement\":3,\"Initialized\":false}";

            var data = JsonConvert.DeserializeObject<SquadCloudData>(legacy);
            Assert.IsNotNull(data.Book);
            data.Book.Normalize();
            Assert.AreEqual(0, data.Book.Squads.Count);
            Assert.IsNull(data.Book.GetActive());
        }
    }
}
#endif
