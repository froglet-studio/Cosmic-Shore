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
    /// The Honeyguide and Ratel in Unity (HONEYGUIDE.md). The lab (<c>Docs/Studios/HoneyguideLab.html</c>, round 6) is the
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
        public void Config_CarriesTheLabsRound6Rules()
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
            Assert.AreEqual(0, c.Patterns.Count(p => p.parked), "round 6 parks no card: the trigger cards are the upgrades");
            Assert.AreEqual(4, c.Engines.Count, "the four designed engines");
            Assert.AreEqual(48, s.PortSets.Count, "the three-fold colour-symmetric port sets the lab draws from");
            Assert.IsTrue(c.Patterns.All(p => p.effect != HoneyguideEffect.None && !string.IsNullOrEmpty(p.effectText)),
                "every card carries its own effect and its words");
            Assert.IsNotNull(c.GameData);
        }

        [Test]
        public void Triggers_ArePeckAndFlit_UpgradedByThirteenCards()
        {
            // Garrett 2026-10-21: "the right trigger should always be a weak attack power that gets upgraded sometimes. the
            // left should be a movement power that works similar. the 39 patterns should still be unique."
            var c = Load();
            Assert.AreEqual(13, c.Upgrades.Count, "the lab's ACTIVES");
            Assert.AreEqual(6, c.Upgrades.Count(u => u.trigger == 0), "six attack upgrades take RT");
            Assert.AreEqual(7, c.Upgrades.Count(u => u.trigger == 1), "seven move upgrades take LT");
            Assert.AreEqual(13, c.Upgrades.Select(u => u.effect).Distinct().Count(), "one upgrade per card");
            foreach (var u in c.Upgrades)
                Assert.AreEqual(1, c.Patterns.Count(p => p.effect == u.effect), $"{u.effect} is exactly one card's effect");
            Assert.AreEqual(39, c.Patterns.Select(p => p.effect).Distinct().Count(), "the 39 patterns are still unique");
            Assert.AreEqual(20f, c.ActiveSeconds, "an upgrade holds its trigger 20 s");
            Assert.AreEqual(HoneyguideTriggerUse.Hold, c.UpgradeFor(HoneyguideEffect.Harmonizer).use);
            Assert.AreEqual(HoneyguideTriggerUse.Charge, c.UpgradeFor(HoneyguideEffect.Fugacity).use);
            Assert.IsNull(c.UpgradeFor(HoneyguideEffect.Plow), "a passive card is not an upgrade");

            var peck = c.PeckAction as FullAutoActionSO;
            Assert.IsNotNull(peck, "RT's Peck is a FullAutoActionSO through the hull's own gun");
            Assert.AreEqual(2.5f, peck.FiringRate, 1e-4f, "lab peckRate");
            float range = peck.ResolveSpeed(null) * 2f * peck.ProjectileTime / Mathf.PI;
            Assert.AreEqual(c.PeckRange, range, 0.5f, "lab peckRange: range = speed x 2T / pi");
            Assert.IsFalse(peck.Spread.Enabled, "a Peck does not spray");
        }

        [Test]
        public void HoneyguideHull_MapsRTAndLTToItsOwnTriggers_AndTheAIPilotFiresNoSparrowGun()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab);
            var found = new System.Collections.Generic.Dictionary<int, ShipActionSO>();
            foreach (var mb in prefab.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!mb) continue;
                var list = new SerializedObject(mb).FindProperty("_inputEventShipActions");
                if (list == null) continue;
                for (int i = 0; i < list.arraySize; i++)
                {
                    var e = list.GetArrayElementAtIndex(i);
                    var actions = e.FindPropertyRelative("ShipActions");
                    if (actions.arraySize > 0) found[e.FindPropertyRelative("InputEvent").intValue] = actions.GetArrayElementAtIndex(0).objectReferenceValue as ShipActionSO;
                }
            }
            Assert.IsTrue(found.TryGetValue((int)InputEvents.RightStickAction, out var rt) && rt is HoneyguideTriggerActionSO { Trigger: 0 },
                "RT is not the Honeyguide's trigger 0 (Peck and the attack upgrades)");
            Assert.IsTrue(found.TryGetValue((int)InputEvents.LeftStickAction, out var lt) && lt is HoneyguideTriggerActionSO { Trigger: 1 },
                "LT is not the Honeyguide's trigger 1 (Flit and the move upgrades)");
            Assert.IsFalse(found.ContainsKey((int)InputEvents.Button1Action), "the Sparrow's turret-stance toggle is gone");
            Assert.IsTrue(found.ContainsKey((int)InputEvents.Button2Action), "the boost stays");
        }

        [Test]
        public void Config_PlaysTheLabsGame_StepForStep()
        {
            // Golden from the lab's own core (seed 777, the shipped rules - Printer retired from the draw - the hard Engineer AI
            // with dice seeded 1000 + k, a half-second clock step between its moves): the asset, read through ToSettings, plays
            // the same game. Re-measured 2026-10-22 from the page and from DynamoCore.cs on this asset (the two agree); the
            // golden before it still had a start slot of 27, from before round 6 unparked every card.
            var s = Load().ToSettings();
            var core = new DynamoCore(s, 777);
            var memo = new DynamoCore.AIMemo();
            Assert.AreEqual("0000032200100200100010010030023300000|5:3,6:2,7:2,10:1,13:2,16:1,20:1,23:1,26:3,29:2,30:3,31:3|3+0||26,1,8/0,0,0|-||capacitor|0|0|0|567895250",
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
            Assert.AreEqual(7, made, "patterns the AI Ratel made");
            Assert.AreEqual("0020000200000021000000003000020000300|36:1,30:1,26:1,24:3,23:2,19:2,15:2,13:3,11:1,10:2,6:3,3:3|0+0|132|30,1,8/2,1,1|-||wildCore|6|7|0|1703684196",
                core.Digest());
        }

        [Test]
        public void RatelBoard_CopiedToAnotherMachine_IsTheSameBoard_AndPlaysOnAlike()
        {
            // A Ratel played on another machine (CrewStations, HoneyguideCrew's ICrewRelay): the hull's machine runs the
            // Dynamo and sends DynamoCore.WriteState; the rider's copy reads it. The copy must be the same board (the
            // digest) and carry the random stream too, or the next spin's draws differ between the two screens.
            var s = Load().ToSettings();
            var real = new DynamoCore(s, 777);
            var memo = new DynamoCore.AIMemo();
            var state = new System.Collections.Generic.List<int>();
            for (int k = 0; k < 200; k++)
            {
                if (k % 3 == 0) real.Advance(s, 0.7);
                else
                {
                    var inp = real.AIPlan(s, new DynamoAISkill { Top = 1, Blunder = 0 }, DynamoCore.Mulberry32(k), memo);
                    if (inp != null && inp.Value.Kind == DynamoInputKind.Activate) memo.Key = "";
                    if (inp != null) real.Apply(s, inp.Value);
                }
                real.Events.Clear();
                if (k % 20 != 0) continue;

                real.WriteState(state);
                var copy = new DynamoCore(s, 4242);
                Assert.IsTrue(copy.ReadState(state.ToArray()), $"step {k}: the copy refused the board");
                Assert.AreEqual(real.Digest(), copy.Digest(), $"step {k}: the copy is not the same board");

                var twin = new DynamoCore(s, 1);
                twin.ReadState(state.ToArray());
                for (int q = 0; q < 30; q++)
                {
                    copy.Advance(s, 0.9);
                    twin.Advance(s, 0.9);
                }
                Assert.AreEqual(twin.Digest(), copy.Digest(), $"step {k}: two copies of one board spun apart");

                var cut = state.GetRange(0, state.Count - 3).ToArray();
                Assert.IsFalse(new DynamoCore(s, 3).ReadState(cut), $"step {k}: a cut-short board was read");
            }
        }

        [Test]
        public void Config_CarriesRound7_ApBankSix_AndAPickOfThree()
        {
            // Garrett 2026-10-29: "lets have the ap cap go to 6. lets do the 3 ability pick as well"
            var s = Load().ToSettings();
            Assert.AreEqual(6, s.ApBank, "what a spin does not use is kept, up to 6");
            Assert.AreEqual(3, s.ApPerTick, "a spin still gives 3");
            Assert.AreEqual(3, s.AbilityChoices, "three abilities are offered and the Ratel picks one");
            Assert.IsFalse(string.IsNullOrEmpty(Load().PickAbilityTitle), "the pick's heading is authored");
        }

        // Plays the AI until the draw offers abilities; the AI's own pick is not applied.
        static DynamoCore UntilOffered(DynamoSettings s, int seed)
        {
            var core = new DynamoCore(s, seed);
            var memo = new DynamoCore.AIMemo();
            for (int k = 0; k < 4000 && core.RevealState?.Offers == null; k++)
            {
                if (k % 2 == 0) { core.Advance(s, 1.0); continue; }
                var inp = core.AIPlan(s, new DynamoAISkill { Top = 1, Blunder = 0 }, DynamoCore.Mulberry32(seed * 7 + k), memo);
                if (inp != null && inp.Value.Kind == DynamoInputKind.Activate) memo.Key = "";
                if (inp != null && inp.Value.Kind != DynamoInputKind.Pick) core.Apply(s, inp.Value);
            }
            return core;
        }

        [Test]
        public void Ratel_IsOfferedThreeAbilities_TheClockWaitsForItsPick_AndItGetsTheOneItPicked()
        {
            var s = Load().ToSettings();
            s.PatternsPerShuffle = 1;
            s.PickSeconds = 0;   // no time limit: only the pick moves on
            for (int seed = 1; seed <= 20; seed++)
            {
                var core = UntilOffered(s, seed);
                var offers = core.RevealState?.Offers;
                Assert.IsNotNull(offers, $"seed {seed}: the draw never offered abilities");
                Assert.AreEqual(3, offers.Length, $"seed {seed}: three offered");
                Assert.AreEqual(3, offers.Distinct().Count(), $"seed {seed}: the offers repeat");
                CollectionAssert.DoesNotContain(offers, core.Ability, $"seed {seed}: the held ability was offered again");
                CollectionAssert.DoesNotContain(offers, DynamoAbility.Printer, $"seed {seed}: the retired Printer was offered");
                Assert.IsTrue(core.Events.Any(e => e.Kind == DynamoEventKind.Offer), $"seed {seed}: no Offer event");
                core.Events.Clear();

                int ticks = core.Ticks;
                double clock = core.Clock;
                core.Advance(s, 120.0);
                Assert.AreEqual(ticks, core.Ticks, $"seed {seed}: the ports spun while the Ratel was picking");
                Assert.AreEqual(clock, core.Clock, $"seed {seed}: the spin clock ran while the Ratel was picking");
                Assert.IsFalse(core.Apply(s, DynamoInput.Activate(0)), $"seed {seed}: a card was made mid-pick");
                Assert.IsFalse(core.Apply(s, DynamoInput.Pick(3)), $"seed {seed}: a fourth offer was taken");

                var want = offers[seed % 3];
                Assert.IsTrue(core.Apply(s, DynamoInput.Pick(seed % 3)), $"seed {seed}: the pick was refused");
                Assert.AreEqual(want, core.Ability, $"seed {seed}: the Ratel did not get the ability it picked");
                Assert.IsTrue(core.Events.Any(e => e.Kind == DynamoEventKind.Shuffle && e.Ability == want), $"seed {seed}: the pick was not announced");
                Assert.IsTrue(core.RevealState == null || (core.RevealState.Stage == 2 && core.RevealState.Offers == null),
                              $"seed {seed}: after the pick the new ports are shown");
                Assert.IsFalse(core.Apply(s, DynamoInput.Pick(0)), $"seed {seed}: a second pick was taken");
            }
        }

        [Test]
        public void Ratel_WhoDoesNotPickInTime_GetsTheFirstOffer()
        {
            var s = Load().ToSettings();
            s.PatternsPerShuffle = 1;
            s.PickSeconds = 2;
            var core = UntilOffered(s, 5);
            var offers = core.RevealState.Offers;
            core.Advance(s, 1.9);
            Assert.IsNotNull(core.RevealState?.Offers, "the pick was taken before its time ran out");
            core.Advance(s, 0.2);
            Assert.AreEqual(offers[0], core.Ability, "a pick that runs out of time takes the first offer");
        }

        [Test]
        public void TheAIRatel_PicksByItsOrder()
        {
            Assert.AreEqual(1, DynamoCore.AIPick(new[] { DynamoAbility.Mirror, DynamoAbility.Overclock, DynamoAbility.Swap }));
            Assert.AreEqual(2, DynamoCore.AIPick(new[] { DynamoAbility.Mirror, DynamoAbility.Swap, DynamoAbility.LongReach }));
            var s = Load().ToSettings();
            s.PatternsPerShuffle = 1;
            var core = UntilOffered(s, 9);
            var inp = core.AIPlan(s, new DynamoAISkill { Top = 1, Blunder = 0 }, DynamoCore.Mulberry32(1), new DynamoCore.AIMemo());
            Assert.IsNotNull(inp, "the AI does not answer an offer");
            Assert.AreEqual(DynamoInputKind.Pick, inp.Value.Kind);
            Assert.AreEqual(DynamoCore.AIPick(core.RevealState.Offers), inp.Value.Slot);
        }

        [Test]
        public void TheBank_KeepsUnspentAP_UpToSix()
        {
            var s = Load().ToSettings();
            var core = new DynamoCore(s, 11);
            Assert.AreEqual(3, core.Ap, "a fresh board starts with one spin's AP");
            core.Advance(s, s.TickSeconds + 0.01);
            Assert.AreEqual(6, core.Ap, "an unspent spin is kept");
            Assert.AreEqual(0, core.Wasted);
            core.Advance(s, s.TickSeconds);
            Assert.AreEqual(6, core.Ap, "the bank holds 6");
            Assert.AreEqual(3, core.Wasted, "past 6 a spin's AP are wasted");
        }

        [Test]
        public void Ratel_NeverDrawsPrinter_WhileItIsRetired()
        {
            // Garrett 2026-10-22: "remove the engineer ability that gives players a free prism at the start of the spin, the
            // experience is too confusing to players on mobile" (for now: the config's retiredAbilities, the lab's RETIRED_ABILITIES)
            var s = Load().ToSettings();
            Assert.IsNotNull(s.Abilities, "the config retires no ability");
            CollectionAssert.DoesNotContain(s.Abilities, (int)DynamoAbility.Printer);
            Assert.AreEqual(DynamoCore.AbilityCount - 1, s.Abilities.Length, "only Printer is retired");
            s.PatternsPerShuffle = 1;   // a new ability after every pattern, to see many draws
            for (int seed = 1; seed <= 60; seed++)
            {
                var core = new DynamoCore(s, seed);
                var memo = new DynamoCore.AIMemo();
                for (int k = 0; k < 300; k++)
                {
                    Assert.AreNotEqual(DynamoAbility.Printer, core.Ability, $"seed {seed}, step {k}: the Ratel drew Printer");
                    Assert.AreEqual(0, core.FreePlace, $"seed {seed}, step {k}: a free prism (the core grants them only for Printer; cards grant theirs through the crew)");
                    if (k % 2 == 0) core.Advance(s, 1.0);
                    else
                    {
                        var inp = core.AIPlan(s, new DynamoAISkill { Top = 1, Blunder = 0 }, DynamoCore.Mulberry32(seed * 1000 + k), memo);
                        if (inp != null && inp.Value.Kind == DynamoInputKind.Activate) memo.Key = "";
                        if (inp != null) core.Apply(s, inp.Value);
                    }
                    core.Events.Clear();
                }
            }
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
        public void HoneyguideHull_DrawsItsOwnModel_WithFourElementMorphs_AndTheBonesItsBodyPoses()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab);
            Assert.IsTrue(prefab.TryGetComponent(out HoneyguideBody _), "the Honeyguide's root carries no HoneyguideBody");
            var model = prefab.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Honeyguide" && t != prefab.transform);
            Assert.IsNotNull(model, "Honeyguide.fbx is not nested under the hull as 'Honeyguide'");
            var smr = model.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.IsTrue(smr && smr.enabled && smr.sharedMesh, "the Honeyguide model has no drawn skinned mesh");
            foreach (var shape in new[] { "Charge", "Mass", "Space", "Time" })
                Assert.GreaterOrEqual(smr.sharedMesh.GetBlendShapeIndex(shape), 0, $"the model has no {shape} morph");
            var bones = model.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToList();
            foreach (var bone in new[] { "hull", "beak", "ratel", "wing.L", "wing.R", "tail" })
                CollectionAssert.Contains(bones, bone, $"the model has no {bone} bone (HoneyguideBody finds bones by name)");
            var drawn = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.enabled).ToList();
            Assert.AreEqual(1, drawn.Count, "a second skinned mesh still draws (the Sparrow inside the Honeyguide)");
            Assert.IsTrue(prefab.TryGetComponent(out VesselCustomization custom));
            var geometries = new SerializedObject(custom).FindProperty("_shipGeometries");
            Assert.AreEqual(1, geometries.arraySize);
            Assert.AreEqual(model.gameObject, geometries.GetArrayElementAtIndex(0).objectReferenceValue,
                "the domain paint does not land on the Honeyguide model");
        }

        [Test]
        public void HoneyguideCard_NamesBothSeats()
        {
            var cls = AssetDatabase.LoadAssetAtPath<SO_Vessel>("Assets/_SO_Assets/Classes/SO_Class_Honeyguide.asset");
            Assert.IsNotNull(cls);
            Assert.AreEqual("Honeyguide", cls.Name, "Name stays the vessel's key");
            Assert.AreEqual("Honeyguide and Ratel", cls.CardName);
        }

        [Test]
        public void MadePattern_HasJuiceAndALineAboveTheCards()
        {
            // Garrett 2026-10-23: juice when a pattern is made, the taken prism morphing into its charge, and the effect's
            // words always above the cards - lime when they are about the Dynamo.
            var c = Load();
            Assert.Greater(c.MadeJuiceSeconds, 0f);
            Assert.Greater(c.ChargeFlightSeconds, 0f);
            StringAssert.Contains("{text}", c.MadeLineTemplate, "the line names what the pattern does");
            var lime = c.DynamoLineColour;
            Assert.IsTrue(lime.g > lime.r && lime.r > lime.b && lime.a > 0.99f, $"the Dynamo line is not lime: {lime}");
        }

        [Test]
        public void TouchesDynamo_IsTheCardsThatWorkOnTheBoard()
        {
            foreach (var e in new[] { HoneyguideEffect.Translator, HoneyguideEffect.Printer5D, HoneyguideEffect.Hammer, HoneyguideEffect.Triplesynth,
                                      HoneyguideEffect.Cilia, HoneyguideEffect.Chrono, HoneyguideEffect.Vorticity, HoneyguideEffect.Anode,
                                      HoneyguideEffect.Saddle, HoneyguideEffect.Amanita, HoneyguideEffect.BigRed })
                Assert.IsTrue(HoneyguideConfigSO.TouchesDynamo(e), $"{e} works on the Dynamo");
            foreach (var e in new[] { HoneyguideEffect.Plow, HoneyguideEffect.Blink, HoneyguideEffect.Gravity, HoneyguideEffect.Lasso,
                                      HoneyguideEffect.Detangler, HoneyguideEffect.Frame })
                Assert.IsFalse(HoneyguideConfigSO.TouchesDynamo(e), $"{e} works on the hull, the slip or a trigger");
        }

        [Test]
        public void HexToDisc_IsTheBoardsPrismAtZeroAndTheBatterysChargeAtOne()
        {
            static float[] Radii(float m)
            {
                var vh = new UnityEngine.UI.VertexHelper();
                HoneyguideMesh.HexToDisc(vh, Vector2.zero, 10f, 4f, m, 0f, Color.white);
                var r = new float[vh.currentVertCount - 1];
                var v = new UIVertex();
                for (int k = 1; k < vh.currentVertCount; k++) { vh.PopulateUIVertex(ref v, k); r[k - 1] = ((Vector2)v.position).magnitude; }
                vh.Dispose();
                return r;
            }
            var hex = Radii(0f);
            Assert.AreEqual(24, hex.Length);
            for (int k = 0; k < hex.Length; k++)
            {
                float a = k * 15f;   // a pointy-top hexagon: corners at 30 + 60n degrees, edge midpoints at 60n
                if (Mathf.Approximately(Mathf.Repeat(a - 30f, 60f), 0f)) Assert.AreEqual(10f, hex[k], 1e-4f, $"corner at {a}");
                if (Mathf.Approximately(Mathf.Repeat(a, 60f), 0f)) Assert.AreEqual(10f * Mathf.Cos(Mathf.PI / 6f), hex[k], 1e-4f, $"edge at {a}");
            }
            foreach (float r in Radii(1f)) Assert.AreEqual(4f, r, 1e-4f, "fully morphed, it is the battery's round charge");
        }
    }
}
#endif
