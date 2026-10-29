#if UNITY_EDITOR
using System.Reflection;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <b>A racing AI flies the waypoints, never a crystal</b> (Yash, 2026-10-26: "while playing any racing game mode, AI
    /// should not go for crystals. It should only focus on waypoints"; Regatta first, 2026-10-17). Every gate race's scene
    /// serializes <c>aiCrystalDetourSlack</c> (200-220 u), which let an AI swing off its line for any crystal near its
    /// route; in Switchback the AI Dolphins took crystals instead of the gates. The rule lives in code
    /// (<c>GateRaceController.AIDetoursForCrystals</c> = false), so no scene value can turn it back on. The one
    /// exception is Switchback, which since 2026-10-29 is Blast Run only: its race needs the key crystals.
    /// </summary>
    public class RaceAIWaypointsOnlyTests
    {
        static float DetourSlack(GateRaceController controller)
        {
            var prop = typeof(GateRaceController).GetProperty("AICrystalDetourSlack",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(prop, "GateRaceController.AICrystalDetourSlack is gone: the steering no longer asks the mode");
            return (float)prop.GetValue(controller);
        }

        static void SetSceneSlack(GateRaceController controller, float slack) =>
            typeof(GateRaceController).GetField("aiCrystalDetourSlack", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(controller, slack);

        static float SlackOf<T>(float sceneSlack) where T : GateRaceController
        {
            var go = new GameObject("RaceAIWaypointsOnlyTests_" + typeof(T).Name);
            try
            {
                var race = go.AddComponent<T>();
                SetSceneSlack(race, sceneSlack);   // what the mode's scene serializes
                return DetourSlack(race);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void EveryGateRace_NeverDetoursForACrystal_WhateverTheSceneSays()
        {
            Assert.AreEqual(0f, SlackOf<RegattaController>(220f), "Regatta: never an omni crystal (2026-10-17)");
            Assert.AreEqual(0f, SlackOf<BreakwaterController>(200f));
            Assert.AreEqual(0f, SlackOf<HeadlongController>(220f));
            Assert.AreEqual(0f, SlackOf<RedlineController>(220f));
            Assert.AreEqual(0f, SlackOf<WarplineController>(220f));
        }

        [Test]
        public void Switchback_IsBlastRun_SoItsAIsTakeTheKeyCrystals()
        {
            // Switchback is Blast Run only since 2026-10-29: a blocked gate opens only to a key crystal's blast, so
            // its AI Dolphins detour for the keys (they sit on the leg line) within the scene's slack.
            Assert.AreEqual(220f, SlackOf<SwitchbackController>(220f));
        }

        [Test]
        public void AModeWhoseRaceNeedsCrystals_DetoursWithinItsSceneSlack()
        {
            // Negative control: the opt-in returns the serialized value, so the test above can fail.
            Assert.AreEqual(220f, SlackOf<CrystalRaceForTests>(220f));
        }

        sealed class CrystalRaceForTests : HeadlongController
        {
            protected override bool AIDetoursForCrystals => true;
        }
    }
}
#endif
