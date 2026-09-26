using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Element Charger toy: its grant is a crystal's grant (whole petals onto the base level), its row reads charge → time for the pilot flying out through it, and
    /// it ships in the toybox on a ring angle no other toy occupies.
    /// </summary>
    public class ElementChargerToyTests
    {
        [Test]
        public void Definition_DefaultsToOnePassToTheLevelFiveUpgrade()
        {
            // Five whole petals: one pass from rest reaches the level-5 ability upgrade, two reach
            // the sustained ceiling (10). The grant is always at least one petal.
            var def = ScriptableObject.CreateInstance<ElementChargerToyDefinitionSO>();
            try
            {
                Assert.AreEqual(5, def.LevelsPerPass);
                Assert.GreaterOrEqual(def.LevelsPerPass, 1);
            }
            finally { Object.DestroyImmediate(def); }
        }

        [Test]
        public void ProjectedLevel_ClampsToTheResourceSystemRange()
        {
            Assert.AreEqual(5, ElementChargerToy.ProjectedLevel(0, 5));
            Assert.AreEqual(10, ElementChargerToy.ProjectedLevel(5, 5));
            Assert.AreEqual(ElementChargerToy.MaxLevel, ElementChargerToy.ProjectedLevel(12, 5));
            Assert.AreEqual(ElementChargerToy.MaxLevel, ElementChargerToy.ProjectedLevel(15, 5));
            Assert.AreEqual(0, ElementChargerToy.ProjectedLevel(-5, 5), "a pass fills a deficit too");
        }

        [Test]
        public void Row_ReadsInHudOrder_ForAPilotFlyingOutward()
        {
            // Stations are laid along the toy's +right, which is the outward-flying pilot's LEFT,
            // so index 0 is the pilot's RIGHTMOST station and must carry the HUD's LAST element.
            var hud = VesselHUDView.AbilityDisplayOrder;
            Assert.AreEqual(hud.Length, ElementChargerToy.MatrixElements.Count);
            for (int i = 0; i < hud.Length; i++)
            {
                Assert.AreEqual(hud[i], ElementChargerToy.MatrixElements[i], "offered order must be the HUD's");
                Assert.AreEqual(hud[hud.Length - 1 - i], ElementChargerToy.ElementAtStation(i),
                    $"station {i} carries the wrong element");
            }
        }

        [Test]
        public void Definition_IsAPilotToy()
        {
            var def = ScriptableObject.CreateInstance<ElementChargerToyDefinitionSO>();
            try
            {
                Assert.AreEqual(ToyCategory.Pilot, def.Category);
                Assert.GreaterOrEqual(def.LevelsPerPass, 1);
            }
            finally { Object.DestroyImmediate(def); }
        }

        [Test]
        public void ShippedToybox_CarriesIt_OnAnUnoccupiedAngle()
        {
            var box = Resources.Load<ToyboxSO>("Toybox");
            Assert.IsNotNull(box, "Resources/Toybox.asset is missing");

            bool found = false;
            var angles = new Dictionary<float, string>();
            foreach (var toy in box.Toys)
            {
                if (!toy) continue;
                if (toy is ElementChargerToyDefinitionSO) found = true;

                float angle = toy.PlacementAngleDegrees;
                if (angle < 0f) continue; // auto-distributed
                Assert.IsFalse(angles.ContainsKey(angle),
                    $"'{toy.DisplayName}' and '{(angles.TryGetValue(angle, out var other) ? other : "")}' " +
                    $"both sit at {angle} degrees - they would stack on the membrane ring");
                angles[angle] = toy.DisplayName;
            }

            Assert.IsTrue(found, "the shipped toybox does not register the Element Charger");
        }
    }
}
