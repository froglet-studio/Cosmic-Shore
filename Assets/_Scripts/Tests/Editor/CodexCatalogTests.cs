#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Codex's chapters (<see cref="CodexCatalog"/>, Docs/MENU_REDESIGN_B.md section 88), built from hand-made
    /// assets so the rules are pinned without a scene: which entries a stop lists, in what order, and what each page says.
    /// </summary>
    [TestFixture]
    public class CodexCatalogTests
    {
        readonly List<Object> _made = new();
        CodexSO _codex;
        SO_VesselList _vessels;
        SO_CaptainList _captains;
        Dictionary<VesselClassType, ElementalAbilityMapSO> _maps;

        T Make<T>() where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            _made.Add(o);
            return o;
        }

        SO_Vessel Vessel(VesselClassType cls, string name)
        {
            var v = Make<SO_Vessel>();
            v.Class = cls;
            v.Name = name;
            return v;
        }

        SO_Captain Pilot(string name, SO_Vessel vessel, Element element, string flavor = "")
        {
            var c = Make<SO_Captain>();
            c.Name = name;
            c.Vessel = vessel;
            c.PrimaryElement = element;
            c.Flavor = flavor;
            c.Description = name + " signature";
            return c;
        }

        ElementalAbilityMapSO Map(params (Element el, string ability, string upgrade)[] rows)
        {
            var map = Make<ElementalAbilityMapSO>();
            var list = new List<ElementalAbilityEntry>();
            foreach (var r in rows) list.Add(new ElementalAbilityEntry { Element = r.el, AbilityLabel = r.ability, UpgradeLabel = r.upgrade });
            typeof(ElementalAbilityMapSO).GetField("entries", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(map, list);
            return map;
        }

        static CodexEntry Entry(string id, CodexKingdom kingdom, string name, params (string label, string value)[] stats)
        {
            var e = new CodexEntry { Id = id, Kingdom = kingdom, DisplayName = name };
            foreach (var s in stats) e.Stats.Add(new CodexStat(s.label, s.value));
            return e;
        }

        [SetUp]
        public void SetUp()
        {
            _codex = Make<CodexSO>();
            foreach (var n in new[] { "Charge", "Mass", "Space", "Time", "Omni" })
                _codex.Ethirions.Add(Entry("ethirion." + n.ToLowerInvariant(), CodexKingdom.Ethirion, n, ("Kind", "Elemental")));

            var arbor = Entry("flora.arbor", CodexKingdom.Flora, "Arbor", ("Prisms per plant", "260"));
            var charge = new CodexVariant { Label = "Charge", Element = Element.Charge };
            charge.Stats.Add(new CodexStat("Heart size", "2.64 world scale"));
            charge.Stats.Add(new CodexStat("Elemental law", "Charge armours its leaves"));
            arbor.Variants.Add(charge);
            _codex.Ecology.Add(arbor);
            _codex.Ecology.Add(Entry("fauna.shark", CodexKingdom.Fauna, "Shark", ("Speed", "25-35 units/s")));

            var tool = Entry("tool.painting", CodexKingdom.Tool, "Connect the Dots");
            tool.Group = "3 · Creation";
            var saturn = new CodexVariant { Label = "Saturn" };
            saturn.Stats.Add(new CodexStat("What it does", "A planet and its rings"));
            tool.Variants.Add(saturn);
            _codex.Tools.Add(tool);

            var sparrow = Vessel(VesselClassType.Sparrow, "Sparrow");
            var squirrel = Vessel(VesselClassType.Squirrel, "Squirrel");
            _vessels = Make<SO_VesselList>();
            _vessels.VesselList = new List<SO_Vessel> { squirrel, sparrow };

            var grizzly = Vessel(VesselClassType.Grizzly, "Grizzly");    // not on the vessel list
            _captains = Make<SO_CaptainList>();
            _captains.CaptainList = new List<SO_Captain>
            {
                Pilot("Longshift", sparrow, Element.Space), Pilot("Shrapnel", sparrow, Element.Mass, "“Bigger bangs!”"),
                Pilot("Trace", squirrel, Element.Time), Pilot("Ghost", grizzly, Element.Charge),
            };

            _maps = new Dictionary<VesselClassType, ElementalAbilityMapSO>
            {
                [VesselClassType.Sparrow] = Map((Element.Mass, "Turret Stance", "Shielded Prisms"), (Element.Space, "Pulsefire Cannons", "")),
            };
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _made) Object.DestroyImmediate(o);
            _made.Clear();
        }

        List<CodexItem> Build(CodexChapter chapter) =>
            CodexCatalog.Build(chapter, _codex, _captains, _vessels, new Sprite[4], new CodexWords(),
                               c => _maps.TryGetValue(c, out var m) ? m : null);

        [Test]
        public void Ethirions_ListEveryCrystal_InAssetOrder()
        {
            var items = Build(CodexChapter.Ethirions);
            Assert.AreEqual(new[] { "Charge", "Mass", "Space", "Time", "Omni" }, items.ConvertAll(i => i.Name).ToArray());
            Assert.AreEqual("Kind", items[0].Page.Facts[0].Label);
        }

        [Test]
        public void Flora_And_Fauna_SplitTheSharedEcologyList()
        {
            Assert.AreEqual("Arbor", Build(CodexChapter.Flora)[0].Name);
            Assert.AreEqual(1, Build(CodexChapter.Flora).Count);
            Assert.AreEqual("Shark", Build(CodexChapter.Fauna)[0].Name);
        }

        [Test]
        public void Lifeform_ShowsItsElementCell_AndItsLawAsARow_NotInTheCell()
        {
            var page = Build(CodexChapter.Flora)[0].Page;
            Assert.AreEqual(1, page.Cells.Count);
            StringAssert.Contains("Heart size", page.Cells[0].Lines);
            StringAssert.DoesNotContain("armours", page.Cells[0].Lines);
            Assert.AreEqual(1, page.Rows.Count);
            Assert.AreEqual("Charge armours its leaves", page.Rows[0].Line);
        }

        [Test]
        public void Tools_DropTheGroupsOrderingPrefix_AndListTheirChoices()
        {
            var item = Build(CodexChapter.Tools)[0];
            Assert.AreEqual("Creation", item.Sub);
            Assert.AreEqual("Saturn", item.Page.Rows[0].Title);
            Assert.AreEqual("A planet and its rings", item.Page.Rows[0].Line);
        }

        [Test]
        public void Pilots_FollowTheVesselList_ThenCharge_ToTime_AndSkipHullsOffTheList()
        {
            var names = Build(CodexChapter.Pilots).ConvertAll(i => i.Name);
            Assert.AreEqual(new[] { "Trace", "Shrapnel", "Longshift" }, names.ToArray());
        }

        [Test]
        public void Pilot_Page_QuotesTheFlavour_AndNamesTheElementsAbility()
        {
            var page = Build(CodexChapter.Pilots).Find(i => i.Name == "Shrapnel").Page;
            Assert.AreEqual("“Bigger bangs!”", page.Body);
            Assert.IsTrue(page.Facts.Exists(f => f.Value == "Mass - Turret Stance"));
            Assert.IsTrue(page.Facts.Exists(f => f.Value == "Shielded Prisms"));
        }

        [Test]
        public void Elements_ListEveryVessel_WithAnOpenSlotWhereNoAbilityIsDesigned()
        {
            var items = Build(CodexChapter.Elements);
            Assert.AreEqual(4, items.Count);
            var mass = items[1].Page;
            Assert.AreEqual(2, mass.Rows.Count);
            Assert.AreEqual("Open slot", mass.Rows[0].Line);                     // the Squirrel has no map here
            Assert.AreEqual("Turret Stance - level 5: Shielded Prisms", mass.Rows[1].Line);
            Assert.AreEqual("Pulsefire Cannons", items[2].Page.Rows[1].Line);   // an ability with no upgrade
        }

        [Test]
        public void Vessel_Page_HasFourAbilities_AndItsPilots()
        {
            var sparrow = Build(CodexChapter.Vessels).Find(i => i.Name == "Sparrow").Page;
            Assert.AreEqual(4, sparrow.Rows.Count);
            Assert.AreEqual("Turret Stance", sparrow.Rows[1].Title);
            Assert.AreEqual("Level 5: Shielded Prisms", sparrow.Rows[1].Line);
            Assert.AreEqual(2, sparrow.Cells.Count);
        }

        [Test]
        public void Artifacts_AreEmpty_UntilTheGameHasArtifactData()
        {
            Assert.IsEmpty(Build(CodexChapter.Artifacts));
        }
    }
}
#endif
