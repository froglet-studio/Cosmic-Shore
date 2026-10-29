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
    /// The Codex's chapters (<see cref="CodexCatalog"/>, Docs/MENU_REDESIGN_B.md sections 88 and 97), built from hand-made
    /// assets so the rules are pinned without a scene: which entries a stop lists, in what order, and what each page says.
    /// </summary>
    [TestFixture]
    public class CodexCatalogTests
    {
        readonly List<Object> _made = new();
        CodexSO _codex;
        SO_VesselList _vessels;
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

            var sparrow = Vessel(VesselClassType.Sparrow, "Sparrow");
            var squirrel = Vessel(VesselClassType.Squirrel, "Squirrel");
            _vessels = Make<SO_VesselList>();
            _vessels.VesselList = new List<SO_Vessel> { squirrel, sparrow };

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

        SpawnMatrixToyDefinitionSO Roster(string[] fauna, string[] flora, params (string species, string line)[] descriptions)
        {
            var roster = Make<SpawnMatrixToyDefinitionSO>();
            var t = typeof(SpawnMatrixToyDefinitionSO);
            const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;
            t.GetField("faunaSpecies", F).SetValue(roster, System.Array.ConvertAll(fauna, n => new SpawnMatrixToyDefinitionSO.FaunaSpecies { Name = n }));
            t.GetField("floraSpecies", F).SetValue(roster, System.Array.ConvertAll(flora, n => new SpawnMatrixToyDefinitionSO.FloraSpecies { Name = n }));
            t.GetField("speciesDescriptions", F).SetValue(roster, System.Array.ConvertAll(descriptions,
                d => new SpawnMatrixToyDefinitionSO.SpeciesDescription { Species = d.species, Description = d.line }));
            return roster;
        }

        CodexWords Words()
        {
            var w = new CodexWords();
            w.elementCards.Add(new CodexCard { title = "Omni", gameplay = "Fly into it." });
            w.elementCards.Add(new CodexCard { title = "Prisms" });
            w.lexicon.Add(new CodexCard { title = "Vessels", gameplay = "Eight hulls." });
            w.lexicon.Add(new CodexCard { title = "" });     // a blank card is not listed
            w.lexicon.Add(new CodexCard { title = "Toys", lore = "Old stations." });
            return w;
        }

        List<CodexItem> Build(CodexChapter chapter, SpawnMatrixToyDefinitionSO roster = null) =>
            CodexCatalog.Build(chapter, _codex, _vessels, new Sprite[4], Words(),
                               c => _maps.TryGetValue(c, out var m) ? m : null, roster);

        [Test]
        public void Elements_AreTheFourElements_ThenTheAuthoredCards()
        {
            var items = Build(CodexChapter.Elements);
            Assert.AreEqual(new[] { "Charge", "Mass", "Space", "Time", "Omni", "Prisms" }, items.ConvertAll(i => i.Name).ToArray());
            Assert.AreEqual(new[] { Element.Charge, Element.Mass, Element.Space, Element.Time, Element.None, Element.None },
                            items.ConvertAll(i => i.Element).ToArray());
            Assert.AreEqual("Fly into it.", items[4].Page.Gameplay);
        }

        [Test]
        public void Element_ShowsItsEtherion_ButNoneOfTheCrystalsBuildNotes()
        {
            var mass = Build(CodexChapter.Elements)[1].Page;
            Assert.IsEmpty(mass.Facts);                                          // "Kind", triangle counts: gone (section 97)
            StringAssert.Contains("Etherions", mass.Gameplay);
            StringAssert.Contains("Mass", mass.Gameplay);
        }

        [Test]
        public void Element_CarriesItsCrystalFlipbook()
        {
            var tex = new Texture2D(4, 4);
            _made.Add(tex);
            var items = CodexCatalog.Build(CodexChapter.Elements, _codex, _vessels, new Sprite[4], Words(), c => null, null,
                                           new[] { null, tex, null, null });
            Assert.AreSame(tex, items[1].Flipbook);
            Assert.AreSame(tex, items[1].Page.Flipbook);
            Assert.IsNull(items[0].Flipbook);
        }

        [Test]
        public void Elements_ListEveryVessel_WithAnOpenSlotWhereNoAbilityIsDesigned()
        {
            var items = Build(CodexChapter.Elements);
            var mass = items[1].Page;
            Assert.AreEqual(2, mass.Rows.Count);
            Assert.AreEqual("Open slot", mass.Rows[0].Line);                     // the Squirrel has no map here
            Assert.AreEqual("Turret Stance - level 5: Shielded Prisms", mass.Rows[1].Line);
            Assert.AreEqual("Pulsefire Cannons", items[2].Page.Rows[1].Line);   // an ability with no upgrade
        }

        [Test]
        public void Fauna_FollowsTheToyBox_AndFillsInFromTheCodex()
        {
            var roster = Roster(new[] { "Swarm", "Shark", "Leviathan" }, new string[0], ("Swarm", "A colony that hunts as one."));
            var items = Build(CodexChapter.Fauna, roster);
            Assert.AreEqual(new[] { "Swarm", "Shark", "Leviathan" }, items.ConvertAll(i => i.Name).ToArray());
            Assert.AreEqual("A colony that hunts as one.", items[0].Page.Gameplay);
            Assert.AreEqual("Speed", items[1].Page.Facts[0].Label);             // the Codex's entry fills in the Shark
            Assert.IsEmpty(items[2].Page.Facts);                                // a Toy Box species the Codex has no entry for
        }

        [Test]
        public void Lifeforms_TheCodexKnows_AndTheToyBoxDoesNot_FollowTheToyBoxList()
        {
            var roster = Roster(new[] { "Swarm" }, new[] { "Gyroid" });
            Assert.AreEqual(new[] { "Swarm", "Shark" }, Build(CodexChapter.Fauna, roster).ConvertAll(i => i.Name).ToArray());
            Assert.AreEqual(new[] { "Gyroid", "Arbor" }, Build(CodexChapter.Flora, roster).ConvertAll(i => i.Name).ToArray());
        }

        [Test]
        public void Lifeforms_MatchTheirCodexEntry_WhateverTheSpacing()
        {
            _codex.Ecology.Add(Entry("fauna.worm-colony", CodexKingdom.Fauna, "Worm Colony", ("Diet", "Trail")));
            var roster = Roster(new[] { "WormColony" }, new string[0]);
            var items = Build(CodexChapter.Fauna, roster);
            Assert.AreEqual(2, items.Count);                                    // WormColony is the Worm Colony, then the Shark
            Assert.AreEqual("Diet", items[0].Page.Facts[0].Label);
        }

        [Test]
        public void Flora_And_Fauna_SplitTheSharedEcologyList_WithoutAToyBox()
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
        public void Lexicon_IsTheAuthoredCards_SkippingBlankOnes()
        {
            var items = Build(CodexChapter.Lexicon);
            Assert.AreEqual(new[] { "Vessels", "Toys" }, items.ConvertAll(i => i.Name).ToArray());
            Assert.AreEqual("Eight hulls.", items[0].Page.Gameplay);
            Assert.AreEqual("Old stations.", items[1].Page.Body);
        }

        [Test]
        public void OnlyLifeforms_DriftThroughThePalette()
        {
            var palette = new[] { Color.green, Color.red, Color.yellow };
            List<CodexItem> With(CodexChapter c) => CodexCatalog.Build(c, _codex, _vessels, new Sprite[4], Words(),
                                                                      cls => null, null, null, palette);
            Assert.AreSame(palette, With(CodexChapter.Flora)[0].Palette);
            Assert.AreSame(palette, With(CodexChapter.Fauna)[0].Page.Palette);
            Assert.IsNull(With(CodexChapter.Elements)[0].Palette);
            Assert.IsNull(With(CodexChapter.Lexicon)[0].Palette);
        }
    }
}
#endif
