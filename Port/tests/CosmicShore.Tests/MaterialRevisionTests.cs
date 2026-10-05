using CosmicShore.Engine;

namespace CosmicShore.Tests;

// A renderer caches what it derives from a material; Revision is how it learns a runtime
// edit (ThemeManager stamping _PrismLitDomain, a tint change, a shader swap) must be re-read.
public class MaterialRevisionTests
{
    [Fact]
    public void EveryMutationBumpsTheRevision()
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        int r = m.Revision;
        void Bumped(string what) { Assert.True(m.Revision > r, what + " did not bump the revision"); r = m.Revision; }

        m.SetFloat("_A", 1f); Bumped("SetFloat");
        m.SetColor("_B", Color.red); Bumped("SetColor");
        m.SetVector("_C", Vector4.one); Bumped("SetVector");
        m.SetInt("_D", 2); Bumped("SetInt");
        m.SetTexture("_E", null); Bumped("SetTexture");
        m.renderQueue = 3000; Bumped("renderQueue");
        m.shader = Shader.Find("Universal Render Pipeline/Lit"); Bumped("shader");
        m.EnableKeyword("_K"); Bumped("EnableKeyword");
        m.DisableKeyword("_K"); Bumped("DisableKeyword");
        m.CopyPropertiesFromMaterial(new Material(m)); Bumped("CopyPropertiesFromMaterial");
        m.Lerp(m, m, 0.5f); Bumped("Lerp");
    }

    [Fact]
    public void ReadsDoNotBumpTheRevision()
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        m.SetFloat("_A", 1f);
        int r = m.Revision;
        _ = m.GetFloat("_A");
        _ = m.HasProperty("_A");
        _ = m.color;
        Assert.Equal(r, m.Revision);
    }
}
