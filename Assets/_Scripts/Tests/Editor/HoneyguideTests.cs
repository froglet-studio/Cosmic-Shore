#if UNITY_EDITOR
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Honeyguide and Ratel in Unity (HONEYGUIDE.md). The lab (<c>Docs/Studios/HoneyguideLab.html</c>, round 4) is the
    /// record: <c>Tools/Build/dynamo_lab/parity.cjs</c> holds <see cref="DynamoCore"/> to the page step by step, and
    /// <c>Tools/Build/author_honeyguide_assets.py --check</c> holds the shipped tables to it. These tests pin what only Unity
    /// can see: the asset loads into the core the lab plays, and the hull the Vessel Changer offers carries the seat.
    /// </summary>
    public class HoneyguideTests
    {
        const string ConfigPath = "Assets/_SO_Assets/VesselActions/Honeyguide/HoneyguideConfig.asset";
        const string PrefabPath = "Assets/_Prefabs/Spacevessels/Honeyguide.prefab";

        static HoneyguideConfigSO Load()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<HoneyguideConfigSO>(ConfigPath);
            Assert.IsNotNull(cfg, $"no HoneyguideConfigSO at {ConfigPath}: run Tools/Build/author_honeyguide_assets.py");
            return cfg;
        }

        [Test]
        public void Config_CarriesTheLabsRound4Rules()
        {
            var c = Load();
            var s = c.ToSettings();
            Assert.AreEqual(15.0, s.TickSeconds, "Garrett 2026-10-21: 15 seconds per spin");
            Assert.AreEqual(3, s.UsesPerPattern, "a pattern made for the third time is replaced");
            Assert.AreEqual(4, s.PatternsPerShuffle, "every 4 patterns, a new ability and a new port pattern");
            Assert.AreEqual(3.5, s.RevealAbility, 1e-9);
            Assert.AreEqual(2.0, s.RevealPorts, 1e-9);
            Assert.AreEqual(45f, c.AimCone, "Garrett 2026-10-21: the nose within 45 degrees of the prism's axis");
            Assert.AreEqual(39, c.Patterns.Count, "the 39 tabletop cards");
            Assert.AreEqual(8, c.Patterns.Count(p => p.parked), "the trigger-upgrade cards stay parked until the triggers take upgrades");
            Assert.AreEqual(4, c.Engines.Count, "the four designed engines");
            Assert.AreEqual(48, s.PortSets.Count, "the three-fold colour-symmetric port sets the lab draws from");
            Assert.IsTrue(c.Patterns.All(p => p.effect != HoneyguideEffect.None && !string.IsNullOrEmpty(p.effectText)),
                "every card carries its own effect and its words");
            Assert.IsNotNull(c.GameData);
        }

        [Test]
        public void Config_PlaysTheLabsGame_StepForStep()
        {
            // Golden from the lab's own core (seed 777, the shipped rules, the hard Engineer AI with dice seeded 1000 + k,
            // a half-second clock step between its moves): the asset, read through ToSettings, plays the same game.
            var s = Load().ToSettings();
            var core = new DynamoCore(s, 777);
            var memo = new DynamoCore.AIMemo();
            Assert.AreEqual("0000032200100200100010010030023300000|5:3,6:2,7:2,10:1,13:2,16:1,20:1,23:1,26:3,29:2,30:3,31:3|3+0||27,1,8/0,0,0|-||wildCore|0|0|0|567895250",
                core.Digest());
            int made = 0;
            for (int k = 0; k < 400; k++)
            {
                if (k % 2 == 0) core.Advance(s, 0.5);
                else
                {
                    var inp = core.AIPlan(s, new DynamoAISkill { Top = 1, Blunder = 0 }, DynamoCore.Mulberry32(1000 + k), memo);
                    if (inp != null && inp.Value.Kind == DynamoInputKind.Activate) memo.Key = "";
                    if (inp != null) core.Apply(s, inp.Value);
                }
                made += core.Events.Count(e => e.Kind == DynamoEventKind.Pick);
                core.Events.Clear();
            }
            Assert.AreEqual(8, made, "patterns the AI Ratel made");
            Assert.AreEqual("0000000010001200020000000100000300000|9:3,16:3,23:3,11:1,6:2,24:2,7:2,19:3,8:2,35:1,31:1,26:1|0+0|123|27,3,36/1,1,0|1:44||overclock|6|8|0|440012856",
                core.Digest());
        }

        [Test]
        public void HoneyguideHull_CarriesTheSeat_WiredToThisConfig_SoTheVesselChangerOffersIt()
        {
            // "Bring it to Unity" = the Toybox's Vessel Changer offers it (Garrett, 2026-10-17)
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, "no Honeyguide.prefab: run Tools/Build/author_honeyguide_assets.py");
            Assert.IsTrue(prefab.TryGetComponent(out HoneyguideCrew seat), "the Honeyguide's root carries no HoneyguideCrew");
            var wired = new SerializedObject(seat).FindProperty("config").objectReferenceValue;
            Assert.AreEqual(Load(), wired, "the seat is not wired to HoneyguideConfig.asset (a null config never seats the Ratel)");
            Assert.IsTrue(prefab.TryGetComponent(out IVesselStatus status));
            Assert.AreEqual(VesselClassType.Honeyguide, status.VesselType);
            CollectionAssert.Contains(ToyVesselRoster.Default, VesselClassType.Honeyguide, "the Vessel Changer offers only the roster");
        }

        [Test]
        public void HoneyguideCard_NamesBothSeats()
        {
            var cls = AssetDatabase.LoadAssetAtPath<SO_Vessel>("Assets/_SO_Assets/Classes/SO_Class_Honeyguide.asset");
            Assert.IsNotNull(cls);
            Assert.AreEqual("Honeyguide", cls.Name, "Name stays the vessel's key");
            Assert.AreEqual("Honeyguide and Ratel", cls.CardName);
        }
    }
}
#endif
