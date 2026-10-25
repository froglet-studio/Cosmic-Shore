#if UNITY_EDITOR
using System.Reflection;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <b>In Regatta an AI flies the course's rings and never a crystal</b> (Yash, 2026-10-17: "in Regatta no AI should
    /// target an omni crystal; only the waypoints of the track"). Every gate race lets an AI detour up to
    /// <c>aiCrystalDetourSlack</c> for a crystal on its way, and Regatta's scene serializes 220, so the rule lives in
    /// code (<c>RegattaController.AICrystalDetourSlack</c> = 0). These tests hold it there and prove the other gate races
    /// still keep their own detour (Garrett's modes are untouched).
    /// </summary>
    public class RegattaNoCrystalTargetTests
    {
        static float DetourSlack(GateRaceController controller)
        {
            var prop = typeof(GateRaceController).GetProperty("AICrystalDetourSlack",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(prop, "GateRaceController.AICrystalDetourSlack is gone: the steering no longer asks the mode");
            return (float)prop.GetValue(controller);
        }

        static float SerializedSlack(GateRaceController controller)
        {
            var field = typeof(GateRaceController).GetField("aiCrystalDetourSlack",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            return (float)field.GetValue(controller);
        }

        [Test]
        public void RegattaNeverDetoursForACrystal_WhateverTheSceneSays()
        {
            var go = new GameObject("RegattaNoCrystalTargetTests");
            try
            {
                var regatta = go.AddComponent<RegattaController>();
                typeof(GateRaceController).GetField("aiCrystalDetourSlack", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(regatta, 220f);   // what MinigameRegatta.unity serializes
                Assert.AreEqual(0f, DetourSlack(regatta), "a Regatta AI would steer at a crystal (the omni crystal) again");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void OtherGateRacesKeepTheirSceneDetour()
        {
            // Negative control: the same reading on a mode that did not opt out returns its serialized value, so the
            // Regatta test above can fail.
            var go = new GameObject("RegattaNoCrystalTargetTests_Headlong");
            try
            {
                var headlong = go.AddComponent<HeadlongController>();
                Assert.AreEqual(SerializedSlack(headlong), DetourSlack(headlong));
                Assert.Greater(DetourSlack(headlong), 0f);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
#endif
