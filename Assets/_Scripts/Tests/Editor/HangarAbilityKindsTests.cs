#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <see cref="HangarAbilityKinds"/> - the Hangar stage's move for each ability, read from its words (the owner,
    /// 2026-10-17: "whatever I press the vessel just shoots for every ability"). Tools/Build/
    /// check_hangar_ability_kinds.py runs the same tables over every shipped map offline.
    /// </summary>
    public class HangarAbilityKindsTests
    {
        [Test]
        public void TheNameDecidesFirst()
        {
            Assert.AreEqual(HangarAbilityKind.Boost, HangarAbilityKinds.Classify("Afterburner", "Hold to fire nothing."));
            Assert.AreEqual(HangarAbilityKind.Build, HangarAbilityKinds.Classify("Boost Ring", "Deploy a ring ahead."));
            Assert.AreEqual(HangarAbilityKind.Stance, HangarAbilityKinds.Classify("Turret Stance", "Stop and fire prisms."));
            Assert.AreEqual(HangarAbilityKind.Stance, HangarAbilityKinds.Classify("Mass / Dust Mode", "Lays a wide wake."));
        }

        [Test]
        public void TheDescriptionDecidesAnUnknownName()
        {
            Assert.AreEqual(HangarAbilityKind.Strike, HangarAbilityKinds.Classify("Charge Mode", "Five cords slicing prisms."));
            Assert.AreEqual(HangarAbilityKind.Shot, HangarAbilityKinds.Classify("Space Mode", "Five guns: RT fires every turret, its pull setting the rate."));
            Assert.AreEqual(HangarAbilityKind.Stance, HangarAbilityKinds.Classify("Mass Mode", "The A button toggles Mass Mode."));
        }

        [Test]
        public void WordsMatchWhole()
        {
            // "ram" is not "ramp", "fire" is not "firing".
            Assert.AreEqual(HangarAbilityKind.Boost, HangarAbilityKinds.Classify("Wind-up", "The ramp boost winds up."));
            Assert.AreEqual(HangarAbilityKind.Passive, HangarAbilityKinds.Classify("Hum", "Its firing rotation."));
            Assert.AreEqual(HangarAbilityKind.Passive, HangarAbilityKinds.Classify("", null));
        }

        [Test]
        public void EveryDesignedAbility_HasAKind_AndTheFleetVaries()
        {
            var kinds = new HashSet<HangarAbilityKind>();
            foreach (VesselClassType vessel in Enum.GetValues(typeof(VesselClassType)))
            {
                var map = ElementalAbilityMapSO.LoadFor(vessel);
                if (!map) continue;
                foreach (var e in map.Entries)
                {
                    if (string.IsNullOrEmpty(e.AbilityLabel) || e.AbilityLabel.StartsWith("(open")) continue;
                    var kind = HangarAbilityKinds.Classify(e.AbilityLabel, e.AbilityDescription);
                    Assert.AreNotEqual(HangarAbilityKind.Passive, kind, $"{vessel} {e.AbilityLabel} has no stage move");
                    kinds.Add(kind);
                }
            }
            Assert.GreaterOrEqual(kinds.Count, 7, "the fleet's abilities should show at least seven different moves");
        }
    }
}
#endif
