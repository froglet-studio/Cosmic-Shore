using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Gameplay;

namespace CosmicShore.Tests;

// V7: VesselAnimation — shape-key element mapping, engine/body flare material writes,
// and Update routing between Idle and the two puppetry input layouts.
public class VesselAnimationTests
{
    class TestAnimation : VesselAnimation
    {
        public readonly List<(float pitch, float yaw, float roll, float throttle)> Puppetry = new();
        public int IdleCalls;

        protected override void AssignTransforms() { }
        protected override void PerformShipPuppetry(float Pitch, float Yaw, float Roll, float Throttle)
            => Puppetry.Add((Pitch, Yaw, Roll, Throttle));
        protected override void Idle() => IdleCalls++;
    }

    const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;

    static (TestAnimation anim, StubVesselStatus status, ResourceSystem rs, InputStatus input) Make(bool useShapeKeys = false)
    {
        var vesselGo = new GameObject("vessel");
        vesselGo.SetActive(false);
        var rs = vesselGo.AddComponent<ResourceSystem>();
        rs.Resources = new List<Resource>();
        vesselGo.SetActive(true);

        var inputGo = new GameObject("input");
        var input = inputGo.AddComponent<InputStatus>();

        var status = new StubVesselStatus { ResourceSystem = rs, InputStatus = input };

        var animGo = new GameObject("animation");
        animGo.SetActive(false);
        var anim = animGo.AddComponent<TestAnimation>();
        anim.SkinnedMeshRenderer = animGo.AddComponent<SkinnedMeshRenderer>();
        typeof(VesselAnimation).GetField("UseShapeKeys", Priv).SetValue(anim, useShapeKeys);
        animGo.SetActive(true);

        return (anim, status, rs, input);
    }

    [Theory]
    [InlineData(Element.Mass, 0)]
    [InlineData(Element.Charge, 1)]
    [InlineData(Element.Space, 2)]
    [InlineData(Element.Time, 3)]
    public void UpdateShapeKey_MapsElementsToBlendShapeIndices(Element element, int index)
    {
        using var loop = new GameLoop();
        var (anim, status, rs, _) = Make(useShapeKeys: true);
        anim.Initialize(status);

        rs.SetElementLevel(element, 1.0f); // level 10 → weight 1.0

        Assert.Equal(1.0f, anim.SkinnedMeshRenderer.GetBlendShapeWeight(index), 3);
    }

    [Fact]
    public void UpdateShapeKey_WithoutShapeKeySupport_WritesNothing()
    {
        using var loop = new GameLoop();
        var (anim, status, rs, _) = Make(useShapeKeys: false);
        anim.Initialize(status);

        rs.SetElementLevel(Element.Mass, 1.0f);

        Assert.Equal(0f, anim.SkinnedMeshRenderer.GetBlendShapeWeight(0));
    }

    [Fact]
    public void FlareEngineAndBody_WriteColorMultiplierOnTheirMaterials()
    {
        using var loop = new GameLoop();
        var (anim, _, _, _) = Make();
        var shader = Shader.Find("Standard");
        anim.SkinnedMeshRenderer.materials = new[]
            { new Material(shader), new Material(shader), new Material(shader), new Material(shader) };

        anim.FlareEngine();
        Assert.Equal(5f, anim.SkinnedMeshRenderer.materials[3].GetFloat("_ColorMultiplier"));
        anim.StopFlareEngine();
        Assert.Equal(1f, anim.SkinnedMeshRenderer.materials[3].GetFloat("_ColorMultiplier"));

        anim.FlareBody(0.5f);
        Assert.Equal(3f, anim.SkinnedMeshRenderer.materials[0].GetFloat("_ColorMultiplier"));
        anim.StopFlareBody();
        Assert.Equal(1f, anim.SkinnedMeshRenderer.materials[0].GetFloat("_ColorMultiplier"));
    }

    [Fact]
    public void Update_BeforeInitialize_DoesNothing()
    {
        using var loop = new GameLoop();
        var (anim, _, _, input) = Make();
        input.Idle = true;

        loop.Tick(1f / 60f);

        Assert.Equal(0, anim.IdleCalls);
        Assert.Empty(anim.Puppetry);
    }

    [Fact]
    public void Update_RoutesIdle_ThenDualStickPuppetry()
    {
        using var loop = new GameLoop();
        var (anim, status, _, input) = Make();
        anim.Initialize(status);

        input.Idle = true;
        loop.Tick(1f / 60f);
        Assert.Equal(1, anim.IdleCalls);
        Assert.Empty(anim.Puppetry);

        input.Idle = false;
        input.YSum = 0.1f; input.XSum = 0.2f; input.YDiff = 0.3f; input.XDiff = 0.4f;
        loop.Tick(1f / 60f);
        Assert.Equal((0.1f, 0.2f, 0.3f, 0.4f), Assert.Single(anim.Puppetry));
    }

    [Fact]
    public void Update_SingleStickControls_UseEasedLeftJoystick()
    {
        using var loop = new GameLoop();
        var (anim, status, _, input) = Make();
        anim.Initialize(status);
        status.IsSingleStickControls = true;

        input.Idle = false;
        input.EasedLeftJoystickPosition = new Vector2(0.6f, -0.8f);
        loop.Tick(1f / 60f);

        Assert.Equal((-0.8f, 0.6f, 0f, 0f), Assert.Single(anim.Puppetry));
    }
}
