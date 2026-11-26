#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The round Ark (<c>ArkRound.fbx</c>, <c>Tools/Build/vessel_model/specs/ark_round.py</c>), the voyages' Ark since
    /// 2026-11-25: its rig table is a hull the game can lay (the tiers where the design put them), its turning parts
    /// alternate, every engine has a stream, the definition wires all of it, every Arkway voyage draws it, and its solid
    /// (<see cref="ArkRoundSolid"/>) is open where the squad flies and closed where the hull is.
    /// </summary>
    public class ArkRoundModelTests
    {
        const string RigTablePath = "Assets/_Models/Vessel Models/ArkRound.rig.json";
        const string ModelPath = "Assets/_Models/Vessel Models/ArkRound.fbx";
        const string DefinitionPath = "Assets/_SO_Assets/Toys/ArkRoundModel.asset";
        const string ArkwaySettingsPath = "Assets/_SO_Assets/Toys/Wander_WithArk.asset";
        const string DomainPlaceholderPath = "Assets/_Graphics/Materials/GreenAccentVesselMaterial.mat";
        const string TrailMaterialPath = "Assets/_Graphics/Materials/ArkEngineTrail.mat";

        /// <summary>The table's build-only numbers (JsonUtility skips them on <see cref="ArkRigTable"/>).</summary>
        [Serializable]
        sealed class ArtifactFrame
        {
            public float artifactScale;
            public float[] artifactOrigin;
        }

        static string LoadText()
        {
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(RigTablePath);
            Assert.IsNotNull(text, $"{RigTablePath} is missing - the model's build writes it beside the FBX");
            return text.text;
        }

        static ArkRigTable LoadTable()
        {
            var table = ArkRigTable.Parse(LoadText(), out string error);
            Assert.IsNotNull(table, error);
            return table;
        }

        /// <summary>Artifact x (the nose axis) of a model-space point.</summary>
        static float NoseX(Vector3 model) => model.z / ArkRoundSolid.Scale + ArkRoundSolid.CentreX;

        [Test]
        public void RigTable_EverySocketIsALayablePrism_OnTheHullOrATurningPart()
        {
            var table = LoadTable();
            var turning = new HashSet<string>(table.spins.Select(s => s.bone));
            var names = new HashSet<string>();
            foreach (var s in table.sockets)
            {
                Assert.IsTrue(names.Add(s.name), $"socket {s.name} is listed twice");
                CollectionAssert.Contains(new[] { "Plain", "Danger", "Shielded", "SuperShielded" }, s.kind, $"{s.name}: unknown kind '{s.kind}'");
                Assert.AreEqual(1f, ArkRigTable.V3(s.forward).magnitude, 1e-3f, $"{s.name}: forward is not a unit vector");
                Assert.AreEqual(1f, ArkRigTable.V3(s.up).magnitude, 1e-3f, $"{s.name}: up is not a unit vector");
                Assert.AreEqual(0f, Vector3.Dot(ArkRigTable.V3(s.forward), ArkRigTable.V3(s.up)), 1e-3f, $"{s.name}: up is not square to forward");
                var size = s.Size;
                Assert.IsTrue(size.x > 0f && size.y > 0f && size.z > 0f, $"{s.name}: size {size}");
                Assert.IsTrue(s.RidesHull || turning.Contains(s.bone), $"{s.name} rides '{s.bone}', which neither the hull nor a turning part is");
            }
            // The prism cell (PRISM_CELL in the spec) sets the count: 581 at 2.2. Far outside this band is a budget change.
            Assert.That(table.sockets.Length, Is.InRange(400, 800), "the round Ark's hull prism count left its budget");
        }

        [Test]
        public void RigTable_TiersAreWhereTheDesignPutsThem()
        {
            var table = LoadTable();
            int Count(PrismKind kind) => table.sockets.Count(s => s.Kind == kind);
            foreach (var kind in new[] { PrismKind.Plain, PrismKind.Danger, PrismKind.Shielded, PrismKind.SuperShielded })
                Assert.Greater(Count(kind), 0, $"no {kind} socket");
            Assert.Greater(Count(PrismKind.Plain), Count(PrismKind.Shielded), "plain prisms are the largest share");
            Assert.Greater(Count(PrismKind.Plain), Count(PrismKind.SuperShielded), "plain prisms are the largest share");
            Assert.Less(Count(PrismKind.Danger), Count(PrismKind.Shielded), "danger prisms are the fewest");

            // Danger only at the lip (the mouth's front gate) and the stern.
            const float FrontLip = 60f, Stern = -82.8f;
            foreach (var s in table.sockets.Where(s => s.Kind == PrismKind.Danger))
            {
                float x = NoseX(s.Position);
                Assert.IsTrue(x > FrontLip || x < Stern, $"{s.name} is a danger prism at nose x {x:0.0}, away from the lip and the stern");
            }
        }

        [Test]
        public void RigTable_TurningPartsAlternate_AboutTheNoseAxis()
        {
            var table = LoadTable();
            Assert.AreEqual(8, table.spins.Length, "five decks, two gates and the pods turn");
            ArkRigTable.Spin Find(string bone) => table.spins.FirstOrDefault(s => s.bone == bone);
            foreach (var s in table.spins)
            {
                Assert.AreEqual(1f, Mathf.Abs(s.Axis.z), 1e-3f, $"{s.bone} turns about {s.Axis}, not the nose axis");
                Assert.Greater(s.degreesPerSecond, 0f, $"{s.bone} does not turn");
                Assert.Less(s.degreesPerSecond, 30f, $"{s.bone} spins faster than a hull part reads");
            }
            for (int i = 0; i < 4; i++)
            {
                var a = Find($"deck.{i}");
                var b = Find($"deck.{i + 1}");
                Assert.IsNotNull(a, $"deck.{i}");
                Assert.IsNotNull(b, $"deck.{i + 1}");
                Assert.Less(Vector3.Dot(a.Axis, b.Axis), 0f, $"deck.{i} and deck.{i + 1} turn the same way: neighbours alternate");
            }
            var g0 = Find("gate.0");
            var g1 = Find("gate.1");
            Assert.IsNotNull(g0, "gate.0");
            Assert.IsNotNull(g1, "gate.1");
            Assert.Less(Vector3.Dot(g0.Axis, g1.Axis), 0f, "the two ring gates turn against each other");
            Assert.IsNotNull(Find("pods"), "the pods turn on their cables");
            Assert.AreEqual(1f, Mathf.Abs(table.ReactorAxis.normalized.z), 1e-3f, "the omni crystal's collar turns about the nose axis");
            Assert.Greater(table.reactorRadius, 0f);
        }

        [Test]
        public void RigTable_EveryEngineLeavesAStream()
        {
            var table = LoadTable();
            Assert.AreEqual(8, table.trails.Length, "Trail 3: every one of the eight engines leaves its own stream");
            var puppeted = new HashSet<string>(table.bones.Select(b => b.bone));
            var streamed = new HashSet<string>();
            foreach (var t in table.trails)
            {
                Assert.IsTrue(streamed.Add(t.bone), $"two streams ride {t.bone}");
                Assert.IsTrue(puppeted.Contains(t.bone), $"stream on '{t.bone}', which is not a bell the table vectors");
                Assert.Greater(t.width, 0f, $"{t.bone}: no width");
                Assert.Less(NoseX(t.Position), -82.8f, $"{t.bone}: the stream starts fore of the stern");
            }
            for (int k = 0; k < 8; k++)
                Assert.IsTrue(streamed.Contains($"engine.{k}"), $"engine.{k} leaves no stream");
        }

        [Test]
        public void RoundDefinition_WiresTheModelItsSolidTheCrystalAndTheStreams()
        {
            var definition = AssetDatabase.LoadAssetAtPath<ArkModelDefinitionSO>(DefinitionPath);
            Assert.IsNotNull(definition, DefinitionPath);
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), definition.Model, "the definition draws ArkRound.fbx");
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<TextAsset>(RigTablePath), definition.RigTable, "the definition binds the model's own rig table");
            Assert.Contains(AssetDatabase.LoadAssetAtPath<Material>(DomainPlaceholderPath), definition.DomainReplacesMaterials.ToList(),
                "the domain must replace the Domain slot's placeholder, or the Ark's trim is green in every domain");
            Assert.AreEqual(ArkHullShape.Round, definition.HullShape, "the round Ark's solid is ArkRoundSolid");
            Assert.IsNotNull(definition.ReactorCrystalMesh, "the omni crystal is the game's own crystal mesh");
            Assert.IsNotNull(definition.ReactorCrystalMaterial, "the omni crystal wears the game's crystal material (neutral, CTA-tinted)");
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<Material>(TrailMaterialPath), definition.EngineTrailMaterial, "the streams' material");
            Assert.Greater(definition.EngineTrailSeconds, 0f);
            Assert.That(definition.EngineTrailAlpha, Is.InRange(0.05f, 1f));
        }

        [Test]
        public void EveryArkwayVoyage_IsGivenTheRoundArk()
        {
            var round = AssetDatabase.LoadAssetAtPath<ArkModelDefinitionSO>(DefinitionPath);
            Assert.IsNotNull(round, DefinitionPath);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<ArkwaySettingsSO>(ArkwaySettingsPath), ArkwaySettingsPath);
            var guids = AssetDatabase.FindAssets("t:ArkwaySettingsSO");
            Assert.IsNotEmpty(guids, "no Arkway voyage settings");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var settings = AssetDatabase.LoadAssetAtPath<ArkwaySettingsSO>(path);
                Assert.AreEqual(round, settings.BuildConfig("test").ArkModel,
                    $"{path} does not draw the round Ark, the voyages' Ark since Shombith's pick (2026-11-25)");
            }
        }

        [Test]
        public void SolidFrame_IsTheOneTheBuildWrote()
        {
            var frame = JsonUtility.FromJson<ArtifactFrame>(LoadText());
            Assert.AreEqual(frame.artifactScale, ArkRoundSolid.Scale, 1e-4f, "ArkRoundSolid.Scale drifted from the build's artifactScale");
            Assert.IsNotNull(frame.artifactOrigin);
            Assert.AreEqual(3, frame.artifactOrigin.Length);
            // The model's origin sits at artifact x = CentreX, so the artifact's origin lands at z = -CentreX * Scale.
            Assert.AreEqual(frame.artifactOrigin[2], -ArkRoundSolid.CentreX * ArkRoundSolid.Scale, 1e-2f,
                "ArkRoundSolid.CentreX drifted from the build's artifactOrigin");
            Assert.AreEqual(Vector3.zero, ArkRoundSolid.FromArtifact(new Vector3(ArkRoundSolid.CentreX, 0f, 0f)));
        }

        [Test]
        public void Solid_IsOpenWhereTheSquadFlies_AndClosedWhereTheHullIs()
        {
            Vector3 A(float x, float y, float z) => ArkRoundSolid.FromArtifact(new Vector3(x, y, z));
            // Open: the tube, the hall, every exit along its length, the gap's water, the sea around.
            foreach (var (name, p) in new[]
                     {
                         ("the tube at its lip", A(5f, 0f, 0f)), ("the tube mid-hull", A(-30f, 0f, 0f)),
                         ("the junction hall", A(-77.5f, 0f, 0f)), ("the gap", A(15f, 12f, 0f)), ("the sea above", A(-30f, 60f, 0f)),
                     })
                Assert.Greater(ArkRoundSolid.Distance(p), 0f, $"{name} is solid");
            var hall = ArkRoundSolid.HallCentre;
            for (int k = 0; k < 3; k++)
            {
                var end = ArkRoundSolid.ExitEnd(k);
                for (float s = 0f; s <= 1f; s += 0.1f)
                {
                    var p = ArkRoundSolid.FromArtifact(Vector3.Lerp(hall, end, s));
                    Assert.Greater(ArkRoundSolid.Distance(p), 0f, $"exit {k} is blocked at {s:0.0} of its length");
                    Assert.IsTrue(ArkRoundSolid.InPassage(p), $"exit {k} at {s:0.0} is outside the passage");
                }
            }
            for (float x = -70f; x <= 10f; x += 5f)
                Assert.IsTrue(ArkRoundSolid.InPassage(A(x, 0f, 0f)), $"the tube is not passage at nose x {x}");

            // Closed: the omni crystal, a deck, the booster, the nose dome.
            foreach (var (name, p) in new[]
                     {
                         ("the omni crystal", A(21f, 0f, 0f)), ("a deck", A(-25f, 12f, 0f)),
                         ("the booster", A(-90f, 0f, 15f)), ("the nose dome", A(62.6f, 0f, 0f)),
                     })
                Assert.Less(ArkRoundSolid.Distance(p), 0f, $"{name} is open water");
            Assert.IsFalse(ArkRoundSolid.InPassage(A(21f, 0f, 0f)), "the crystal is in the passage");
        }

        [Test]
        public void Solid_PodsMoveWithTheirTurn()
        {
            float t = Mathf.PI / 3f;
            var pod = ArkRoundSolid.FromArtifact(new Vector3(-40f, 33.5f * Mathf.Cos(t), 33.5f * Mathf.Sin(t)));
            var turn = Quaternion.AngleAxis(60f, Vector3.forward);
            Assert.Less(ArkRoundSolid.Distance(pod), 0f, "the pod is not solid where it was built");
            Assert.Greater(ArkRoundSolid.Distance(pod, turn), 0f, "the pod stayed where it was built after it turned away");
            Assert.Less(ArkRoundSolid.Distance(turn * pod, turn), 0f, "the pod's solid did not follow it");
        }

        [Test]
        public void Solid_HugsEverySocket()
        {
            // A socket is a prism laid on the hull's skin: the solid must be within a prism's reach of it, or ships
            // pass through prisms (too far out) or bounce off water (too far in).
            foreach (var s in LoadTable().sockets)
                Assert.That(ArkRoundSolid.Distance(s.Position), Is.InRange(-4f, 4f), $"{s.name} ({s.bone}) is off the solid");
        }

        [Test]
        public void Model_HasEveryBoneTheTableNames_AndTheLaunchPadsSitInTheExits()
        {
            var table = LoadTable();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            Assert.IsNotNull(model, ModelPath);
            // The crystal's bone (omni) and the launch tubes' bones carry no geometry: an importer that drops unweighted
            // bones leaves the Ark with no omni crystal and its squad launching from the old door.
            StringAssert.Contains("optimizeBones: 0", System.IO.File.ReadAllText(ModelPath + ".meta"),
                "ArkRound.fbx must keep its unweighted bones (omni, hangar.slot/exit)");
            var byName = new Dictionary<string, Transform>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                byName.TryAdd(t.name, t);
            foreach (var name in table.bones.Select(b => b.bone)
                         .Concat(table.spins.Select(s => s.bone))
                         .Concat(table.trails.Select(t => t.bone))
                         .Concat(table.sockets.Where(s => !s.RidesHull).Select(s => s.bone))
                         .Concat(new[] { "omni", "omni.ring" }).Distinct())
                Assert.IsTrue(byName.ContainsKey(name), $"the model has no bone '{name}' (optimizeBones strips a bone that skins nothing)");
            var root = model.transform;
            for (int s = 0; s < 3; s++)
            {
                Assert.IsTrue(byName.TryGetValue($"hangar.slot.{s}", out var pad), $"no hangar.slot.{s}");
                Assert.IsTrue(byName.TryGetValue($"hangar.exit.{s}", out var exit), $"no hangar.exit.{s}");
                var padLocal = root.InverseTransformPoint(pad.position);
                var exitLocal = root.InverseTransformPoint(exit.position);
                Assert.IsTrue(ArkRoundSolid.InPassage(padLocal), $"launch pad {s} at {padLocal} is not inside its exit");
                Assert.Greater(ArkRoundSolid.Distance(padLocal), 0f, $"launch pad {s} is inside the hull");
                Assert.Less(exitLocal.z, padLocal.z, $"exit {s} is not aft of its pad: the squad would launch forward into the hall");
            }
        }

        [Test]
        public void Definition_DrawsTheArkAtTheArtifactsSize_AgainstASquirrel()
        {
            // The artifact draws a Squirrel for scale, 2.42 long (escort() in ark_round_geometry.js: 2 * 2.2 * 0.55), beside
            // its Ark; the game's Squirrel is 6.451 long (Docs/VesselModels/Squirrel.md 1.3). Shombith's playtest,
            // 2026-11-26: "the ark need to bigger and longer it does not match the artifact size".
            const float ArtifactSquirrel = 2.42f, GameSquirrel = 6.451f;
            var table = LoadTable();
            var definition = AssetDatabase.LoadAssetAtPath<ArkModelDefinitionSO>(DefinitionPath);
            Assert.IsNotNull(definition, DefinitionPath);
            float artifactRatio = table.noseToJets / ArkRoundSolid.Scale / ArtifactSquirrel;
            float gameRatio = table.noseToJets * definition.Scale / GameSquirrel;
            Assert.That(gameRatio / artifactRatio, Is.InRange(0.93f, 1.07f),
                $"the Ark is {gameRatio:0} Squirrels long in the game and {artifactRatio:0} in the artifact");
        }

        [Test]
        public void Solid_ClosesTheSlotsBetweenTheTurningDecks()
        {
            // A slot between two decks is a crevice, not a passage: open, it let a vessel sit inside the model.
            foreach (var x in new[] { -6.3f, -18.5f, -32f, -45.5f, -59f })
                for (int k = 0; k < 6; k++)
                {
                    float t = k * Mathf.PI / 3f + 0.2f;
                    var p = ArkRoundSolid.FromArtifact(new Vector3(x, 13f * Mathf.Cos(t), 13f * Mathf.Sin(t)));
                    Assert.Less(ArkRoundSolid.Distance(p), 0f, $"the slot at nose x {x} is open at {t:0.0} rad");
                }
        }

        [Test]
        public void HoldOut_SweepsAFastMove_SoABoostCannotStepOverAWall()
        {
            // Both ends of one frame's boost are clear (the gap's water outside the tube's lip, the tube's inside), but the
            // straight line between them crosses the tube's wall: the sweep stops the vessel before the wall.
            var from = ArkRoundSolid.FromArtifact(new Vector3(5f, 14f, 0f));
            var to = ArkRoundSolid.FromArtifact(new Vector3(5f, 3f, 0f));
            const float margin = 1f;
            Assert.Greater(ArkRoundSolid.Distance(from), margin, "the start is not clear");
            Assert.Greater(ArkRoundSolid.Distance(to), margin, "the end is not clear");
            float reach = ArkHullSolid.FirstContact(ArkHullShape.Round, Quaternion.identity, from, to, margin);
            Assert.Less(reach, 1f, "the move passed through the tube's wall");
            var stop = Vector3.Lerp(from, to, reach);
            Assert.GreaterOrEqual(ArkRoundSolid.Distance(stop), margin, "the sweep stopped the vessel inside the margin");
            Assert.Greater(stop.y, (9.2f + 1.2f) * ArkRoundSolid.Scale, "the sweep let the vessel past the wall");

            // A clear move is left alone.
            var along = ArkRoundSolid.FromArtifact(new Vector3(-20f, 0f, 0f));
            Assert.AreEqual(1f, ArkHullSolid.FirstContact(ArkHullShape.Round, Quaternion.identity, to, along, margin),
                "a move down the open tube was stopped");
        }

        [Test]
        public void SocketLays_LayTheWholeRoundHull()
        {
            var definition = AssetDatabase.LoadAssetAtPath<ArkModelDefinitionSO>(DefinitionPath);
            var lays = ArkModel.SocketLays(definition, Domains.Jade);
            Assert.IsNotNull(lays);
            Assert.AreEqual(LoadTable().sockets.Length, lays.Count);
        }
    }
}
#endif
