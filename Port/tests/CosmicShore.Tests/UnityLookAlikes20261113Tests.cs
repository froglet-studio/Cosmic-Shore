using System;
using CosmicShore.Engine;
using CosmicShore.Engine.Collections;
using CosmicShore.Engine.Experimental.Rendering;
using CosmicShore.Engine.UI;

namespace CosmicShore.Tests;

// The Unity look-alikes added 2026-11-13 so the live compile builds again (SwitchbackSenseBatch, ProjectileSweepBatch,
// ColorGrade, WorldColorTrace, ButterflyDustField, MeerkatSentinels, FulmenView, UIThemeLive). Each test pins the
// behaviour the port gives them, not just that they compile.
public class UnityLookAlikes20261113Tests
{
    [Fact]
    public void SpherecastCommand_Batch_WritesHitsNearestFirst_AndLeavesEmptySlotsNull()
    {
        using var loop = new GameLoop();
        var (_, near, _) = ContactRig.MakeProbe("near", new Vector3(0f, 0f, 10f), radius: 1f, body: false);
        var (_, far, _) = ContactRig.MakeProbe("far", new Vector3(0f, 0f, 20f), radius: 1f, body: false);
        loop.Tick(0.02f);

        var query = new QueryParameters(~0, false, QueryTriggerInteraction.Collide, false);
        var cmds = new NativeArray<SpherecastCommand>(2, Allocator.Temp);
        cmds[0] = new SpherecastCommand(Vector3.zero, 0.5f, Vector3.forward, query, 30f);
        cmds[1] = new SpherecastCommand(Vector3.zero, 0.5f, Vector3.back, query, 30f); // nothing behind
        var hits = new NativeArray<RaycastHit>(2 * 3, Allocator.Temp);
        SpherecastCommand.ScheduleBatch(cmds, hits, 1, 3).Complete();

        Assert.Same(near, hits[0].collider);
        Assert.Same(far, hits[1].collider);
        Assert.Null(hits[2].collider);
        // The ray meets the near sphere's surface at z = 9; the cast sphere's centre stops a radius short.
        Assert.Equal(8.5f, hits[0].distance, 3);
        for (int k = 3; k < 6; k++) Assert.Null(hits[k].collider);
    }

    [Fact]
    public void SpherecastCommand_ZeroDistance_HitsNothing()
    {
        using var loop = new GameLoop();
        ContactRig.MakeProbe("p", new Vector3(0f, 0f, 2f), body: false);
        loop.Tick(0.02f);
        var cmds = new NativeArray<SpherecastCommand>(1, Allocator.Temp);
        cmds[0] = new SpherecastCommand(Vector3.zero, 0.5f, Vector3.forward, QueryParameters.Default, 0f);
        var hits = new NativeArray<RaycastHit>(4, Allocator.Temp);
        SpherecastCommand.ScheduleBatch(cmds, hits, 1, 4);
        for (int k = 0; k < 4; k++) Assert.Null(hits[k].collider);
    }

    [Fact]
    public void OverlapCapsuleCommand_Batch_FindsTheColliderOnTheSegment()
    {
        using var loop = new GameLoop();
        var (_, probe, _) = ContactRig.MakeProbe("p", new Vector3(5f, 0f, 0f), body: false);
        loop.Tick(0.02f);
        var query = new QueryParameters(~0, false, QueryTriggerInteraction.Collide, false);
        var cmds = new NativeArray<OverlapCapsuleCommand>(2, Allocator.Temp);
        cmds[0] = new OverlapCapsuleCommand(Vector3.zero, new Vector3(10f, 0f, 0f), 0.5f, query);
        cmds[1] = new OverlapCapsuleCommand(new Vector3(0f, 50f, 0f), new Vector3(10f, 50f, 0f), 0.5f, query);
        var hits = new NativeArray<ColliderHit>(2, Allocator.Temp);
        OverlapCapsuleCommand.ScheduleBatch(cmds, hits, 32, 1);
        Assert.Same(probe, hits[0].collider);
        Assert.Null(hits[1].collider);
    }

    [Fact]
    public void Texture3D_SetPixelData_StoresVoxelsXFastest()
    {
        var t = new Texture3D(2, 2, 2, GraphicsFormat.R8G8B8A8_UNorm, TextureCreationFlags.None);
        var rgba = new byte[2 * 2 * 2 * 4];
        int voxel = (1 * 2 + 0) * 2 + 1; // x 1, y 0, z 1
        rgba[voxel * 4] = 255; rgba[voxel * 4 + 3] = 255;
        t.SetPixelData(rgba, 0);
        t.Apply(false, true);
        Assert.Equal(TextureDimension.Tex3D, t.dimension);
        Assert.Equal(2, t.depth);
        Assert.Equal(1f, t.GetPixel(1, 0, 1).r, 3);
        Assert.Equal(0f, t.GetPixel(0, 0, 1).r, 3);
        Assert.False(t.isReadable);
    }

    [Fact]
    public void Gradient_FixedMode_HoldsEachKeyUpToItsTime()
    {
        var g = new Gradient { mode = GradientMode.Fixed };
        g.SetKeys(new[] { new GradientColorKey(Color.red, 0.5f), new GradientColorKey(Color.blue, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        Assert.Equal(Color.red, g.Evaluate(0.25f));
        Assert.Equal(Color.blue, g.Evaluate(0.75f));
        g.mode = GradientMode.Blend;
        Assert.NotEqual(Color.blue, g.Evaluate(0.75f));
    }

    [Fact]
    public void Mesh_CombineMeshes_KeepsOneSubmeshPerInstance_AndAppliesTheMatrix()
    {
        var quad = new Mesh
        {
            vertices = new[] { Vector3.zero, Vector3.right, Vector3.up },
            triangles = new[] { 0, 1, 2 },
        };
        var parts = new[]
        {
            new CombineInstance { mesh = quad, subMeshIndex = 0, transform = Matrix4x4.identity },
            new CombineInstance { mesh = quad, subMeshIndex = 0, transform = Matrix4x4.TRS(new Vector3(10f, 0f, 0f), Quaternion.identity, Vector3.one) },
        };
        var combined = new Mesh();
        combined.CombineMeshes(parts, mergeSubMeshes: false, useMatrices: true);
        Assert.Equal(6, combined.vertexCount);
        Assert.Equal(2, combined.subMeshCount);
        Assert.Equal(new[] { 3, 4, 5 }, combined.GetTriangles(1));
        Assert.Equal(new Vector3(11f, 0f, 0f), combined.vertices[4]);
        var sub = combined.GetSubMesh(1);
        Assert.Equal(3, sub.indexStart);
        Assert.Equal(3, sub.indexCount);
    }

    [Fact]
    public void ColorBlock_ComparesByValue()
    {
        var a = ColorBlock.defaultColorBlock;
        var b = ColorBlock.defaultColorBlock;
        Assert.True(a == b);
        b.pressedColor = Color.red;
        Assert.True(a != b);
    }

    [Fact]
    public void ParticleSystem_CustomData_And_ColorBySpeed_RoundTrip()
    {
        using var loop = new GameLoop();
        var ps = new GameObject("ps").AddComponent<ParticleSystem>();
        var custom = ps.customData;
        custom.enabled = true;
        custom.SetMode(ParticleSystemCustomData.Custom2, ParticleSystemCustomDataMode.Color);
        custom.SetColor(ParticleSystemCustomData.Custom2, new ParticleSystem.MinMaxGradient(Color.green));
        Assert.Equal(ParticleSystemCustomDataMode.Disabled, ps.customData.GetMode(ParticleSystemCustomData.Custom1));
        Assert.Equal(ParticleSystemCustomDataMode.Color, ps.customData.GetMode(ParticleSystemCustomData.Custom2));
        Assert.Equal(Color.green, ps.customData.GetColor(ParticleSystemCustomData.Custom2).colorMax);

        var speed = ps.colorBySpeed;
        speed.enabled = true;
        speed.color = Color.red;
        Assert.True(ps.colorBySpeed.enabled);
        Assert.Equal(Color.red, ps.colorBySpeed.color.colorMax);
    }

    [Fact]
    public void Application_ExitCancellationToken_IsLiveUntilQuit()
    {
        using var loop = new GameLoop();
        Assert.False(Application.exitCancellationToken.IsCancellationRequested);
    }
}
