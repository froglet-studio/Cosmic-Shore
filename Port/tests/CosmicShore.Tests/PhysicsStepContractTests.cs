using System;
using CosmicShore.Content.Serialization;
using CosmicShore.Engine;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// The physics step, as the original engine runs it with autoSyncTransforms off
// (this project's DynamicsManager): trigger messages are a SAMPLE taken in the
// fixed step, and queries answer from the collider poses of the last step.
// Found by replaying Unity-trained AI genomes in the port — per-frame triggers
// and live-pose queries let a racer collect crystals Unity would have missed.
// ─────────────────────────────────────────────────────────────────────────────
public class PhysicsStepContractTests
{
    [Fact]
    public void Triggers_AreSampledInTheFixedStep_NotEveryFrame()
    {
        using var loop = new GameLoop();
        Time.fixedDeltaTime = 0.04f;
        var (_, _, recorderA) = ContactRig.MakeProbe("A", Vector3.zero, isTrigger: true);
        var (goB, _, _) = ContactRig.MakeProbe("B", new Vector3(50f, 0f, 0f));

        // 60 Hz frames, 25 Hz steps: the first frame carries no step.
        loop.Tick(1f / 60f);
        goB.transform.position = new Vector3(1f, 0f, 0f);
        loop.Tick(1f / 60f);            // 0.033 s: still no step — B passes through unseen
        goB.transform.position = new Vector3(50f, 0f, 0f);
        loop.Tick(1f / 60f);            // 0.05 s: a step, B already gone
        Assert.Empty(recorderA.Events);

        goB.transform.position = new Vector3(1f, 0f, 0f);
        loop.Tick(1f / 60f);            // 0.067 s: no step
        Assert.Empty(recorderA.Events);
        loop.Tick(1f / 60f);            // 0.083 s: a step — now it is sampled
        Assert.Equal("enter", Assert.Single(recorderA.Events).evt);
    }

    [Fact]
    public void Query_SeesTheLastStepsPose_UntilTheNextStepOrSyncTransforms()
    {
        using var loop = new GameLoop();
        Time.fixedDeltaTime = 0.02f;
        var (go, collider, _) = ContactRig.MakeProbe("P", Vector3.zero);
        loop.Tick(0.02f); // the step puts P in the scene at the origin

        go.transform.position = new Vector3(100f, 0f, 0f);
        var hits = new Collider[4];
        Assert.Equal(1, Physics.OverlapSphereNonAlloc(Vector3.zero, 2f, hits, ~0, QueryTriggerInteraction.Collide));
        Assert.Same(collider, hits[0]);
        Assert.Equal(0, Physics.OverlapSphereNonAlloc(new Vector3(100f, 0f, 0f), 2f, hits, ~0, QueryTriggerInteraction.Collide));

        Physics.SyncTransforms();
        Assert.Equal(0, Physics.OverlapSphereNonAlloc(Vector3.zero, 2f, hits, ~0, QueryTriggerInteraction.Collide));
        Assert.Equal(1, Physics.OverlapSphereNonAlloc(new Vector3(100f, 0f, 0f), 2f, hits, ~0, QueryTriggerInteraction.Collide));

        go.transform.position = new Vector3(-100f, 0f, 0f);
        loop.Tick(0.02f);
        Assert.Equal(1, Physics.OverlapSphereNonAlloc(new Vector3(-100f, 0f, 0f), 2f, hits, ~0, QueryTriggerInteraction.Collide));
        Assert.True(Physics.Raycast(new Vector3(-110f, 0f, 0f), Vector3.right, out var ray, 50f, ~0, QueryTriggerInteraction.Collide));
        Assert.Same(collider, ray.collider);
        Assert.Equal(9f, ray.distance, 3);
    }

    [Fact]
    public void Query_ArrivalsEnterAtOnce_DeparturesLeaveAtOnce_InRegistrationOrder()
    {
        using var loop = new GameLoop();
        var (_, first, _) = ContactRig.MakeProbe("first", new Vector3(3f, 0f, 0f));
        loop.Tick(0.02f);

        // Created between steps: in the scene immediately, at its current pose.
        var (_, second, _) = ContactRig.MakeProbe("second", new Vector3(-3f, 0f, 0f));
        var hits = new Collider[4];
        Assert.Equal(2, Physics.OverlapSphereNonAlloc(Vector3.zero, 3f, hits, ~0, QueryTriggerInteraction.Collide));
        Assert.Same(first, hits[0]);
        Assert.Same(second, hits[1]);

        // Deactivated: gone at once; reactivated: back at once.
        first.gameObject.SetActive(false);
        Assert.Equal(1, Physics.OverlapSphereNonAlloc(Vector3.zero, 3f, hits, ~0, QueryTriggerInteraction.Collide));
        Assert.Same(second, hits[0]);
        loop.Tick(0.02f);
        first.gameObject.SetActive(true);
        Assert.Equal(2, Physics.OverlapSphereNonAlloc(Vector3.zero, 3f, hits, ~0, QueryTriggerInteraction.Collide));
        Assert.Same(first, hits[0]);

        // A full buffer keeps the registration-order prefix.
        var one = new Collider[1];
        Assert.Equal(1, Physics.OverlapSphereNonAlloc(Vector3.zero, 3f, one, ~0, QueryTriggerInteraction.Collide));
        Assert.Same(first, one[0]);
    }

    [Fact]
    public void Query_FindsShapesAcrossTheWholeSortedWindow_IncludingHugeOnes()
    {
        using var loop = new GameLoop();
        var rng = new System.Random(3);
        for (int i = 0; i < 400; i++)
            ContactRig.MakeProbe("p" + i, new Vector3(rng.Next(-500, 500), rng.Next(-50, 50), rng.Next(-50, 50)), radius: 1f + (float)rng.NextDouble() * 3f);
        var (_, huge, _) = ContactRig.MakeProbe("huge", new Vector3(-2000f, 0f, 0f), radius: 1990f);
        loop.Tick(0.02f);

        var buffer = new Collider[1024];
        for (int q = 0; q < 50; q++)
        {
            var at = new Vector3(rng.Next(-520, 520), rng.Next(-60, 60), rng.Next(-60, 60));
            float r = 5f + (float)rng.NextDouble() * 60f;
            int n = Physics.OverlapSphereNonAlloc(at, r, buffer, ~0, QueryTriggerInteraction.Collide);
            // Brute force over the same world (nothing moved since the step).
            int expected = 0;
            foreach (var c in UnityEngine_FindColliders())
            {
                var s = (SphereCollider)c;
                float rr = r + s.radius;
                if ((s.transform.position - at).sqrMagnitude <= rr * rr) expected++;
            }
            Assert.Equal(expected, n);
            bool sawHuge = Array.IndexOf(buffer, huge, 0, n) >= 0;
            Assert.Equal((huge.transform.position - at).magnitude <= r + 1990f, sawHuge);
        }

        static SphereCollider[] UnityEngine_FindColliders()
            => CosmicShore.Engine.Object.FindObjectsByType<SphereCollider>(FindObjectsSortMode.None);
    }

    sealed class StayCounter : MonoBehaviour
    {
        public readonly System.Collections.Generic.List<string> Events = new();
        void OnTriggerEnter(Collider other) => Events.Add("enter");
        void OnTriggerStay(Collider other) => Events.Add("stay");
        void OnTriggerExit(Collider other) => Events.Add("exit");
    }

    [Fact]
    public void Stay_FiresEveryStepWhileTouching_FromTheEnterStep_AndNeverAfterExit()
    {
        using var loop = new GameLoop();
        Time.fixedDeltaTime = 0.04f;
        var (a, _, _) = ContactRig.MakeProbe("A", Vector3.zero, isTrigger: true);
        var counter = a.AddComponent<StayCounter>();
        var (b, _, _) = ContactRig.MakeProbe("B", new Vector3(1f, 0f, 0f));

        for (int f = 0; f < 6; f++) loop.Tick(1f / 60f); // 0.1 s: steps at 0.04 and 0.08
        Assert.Equal(new[] { "enter", "stay", "stay" }, counter.Events);

        b.transform.position = new Vector3(50f, 0f, 0f);
        for (int f = 0; f < 6; f++) loop.Tick(1f / 60f); // steps at 0.12 (exit) and 0.16
        Assert.Equal(new[] { "enter", "stay", "stay", "exit" }, counter.Events);
    }

    [Fact]
    public void HexBlob_PrimitiveArrays_DecodeLittleEndian()
    {
        Assert.True(SerializedReader.TryDecodeHexBlob("0100000002000000ffffffff", typeof(int), out var ints));
        Assert.Equal(new[] { 1, 2, -1 }, (int[])ints);
        Assert.True(SerializedReader.TryDecodeHexBlob("0000803f", typeof(float), out var floats));
        Assert.Equal(1f, ((float[])floats)[0]);
        Assert.True(SerializedReader.TryDecodeHexBlob("", typeof(int), out var empty));
        Assert.Empty((int[])empty);
        Assert.False(SerializedReader.TryDecodeHexBlob("012", typeof(int), out _));
        Assert.False(SerializedReader.TryDecodeHexBlob("00000000", typeof(string), out _));
    }
}
