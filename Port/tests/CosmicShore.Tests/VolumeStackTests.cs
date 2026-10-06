using CosmicShore.Engine;
using CosmicShore.Engine.Rendering;

namespace CosmicShore.Tests;

// The post stack a camera renders is the blend of its scene's volumes (SRP volume
// framework), not a constant — that is what lets the speed tunnel's Panini write reach
// the screen and a camera switch its post-processing off.
public class VolumeStackTests
{
    static Volume MakeVolume(float priority, float weight, float bloomIntensity, bool overrideIntensity = true)
    {
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var bloom = profile.Add<Bloom>();
        bloom.intensity.value = bloomIntensity;
        bloom.intensity.overrideState = overrideIntensity;
        var go = new GameObject("Volume");
        var v = go.AddComponent<Volume>();
        v.sharedProfileRef = profile;
        v.priority = priority;
        v.weight = weight;
        return v;
    }

    [Fact]
    public void HigherPriorityVolumeBlendsOverLowerByItsWeight()
    {
        using var loop = new GameLoop();
        MakeVolume(priority: 0, weight: 1, bloomIntensity: 2f);
        MakeVolume(priority: 10, weight: 0.5f, bloomIntensity: 4f);
        var stack = new VolumeStack();
        stack.Update(Vector3.zero, ~0);
        Assert.Equal(3f, stack.GetComponent<Bloom>().intensity.value, 3);
    }

    [Fact]
    public void NonOverriddenParametersKeepTheirDefaults()
    {
        using var loop = new GameLoop();
        MakeVolume(priority: 0, weight: 1, bloomIntensity: 5f, overrideIntensity: false);
        var stack = new VolumeStack();
        stack.Update(Vector3.zero, ~0);
        Assert.Equal(0f, stack.GetComponent<Bloom>().intensity.value);
    }

    [Fact]
    public void VolumesOffTheCameraMaskAreIgnored()
    {
        using var loop = new GameLoop();
        var v = MakeVolume(priority: 0, weight: 1, bloomIntensity: 2f);
        v.gameObject.layer = 5;
        var stack = new VolumeStack();
        stack.Update(Vector3.zero, 1 << 0);
        Assert.Equal(0f, stack.GetComponent<Bloom>().intensity.value);
    }

    [Fact]
    public void ProfileInstance_CopiesComponents_SoRuntimeEditsNeverTouchTheAsset()
    {
        using var loop = new GameLoop();
        var v = MakeVolume(priority: 0, weight: 1, bloomIntensity: 2f);
        var shared = v.sharedProfileRef;
        Assert.True(v.profile.TryGet<Bloom>(out var instanceBloom));
        instanceBloom.intensity.value = 9f;
        Assert.True(shared.TryGet<Bloom>(out var sharedBloom));
        Assert.Equal(2f, sharedBloom.intensity.value);

        var stack = new VolumeStack();
        stack.Update(Vector3.zero, ~0);
        Assert.Equal(9f, stack.GetComponent<Bloom>().intensity.value); // the renderer reads the live instance
    }
}
