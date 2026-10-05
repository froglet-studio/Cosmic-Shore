using CosmicShore.Engine;
using CosmicShore.Engine.UI;

namespace CosmicShore.Tests;

// A Graphic that builds its own geometry (the ability lockup's trapezoid plates, the scope
// petal, generated rings) must reach the screen through its own OnPopulateMesh. The UI
// renderer used to draw only Image/RawImage/text and silently skip everything else.
public class CustomGraphicMeshTests
{
    sealed class Triangle : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            vh.AddVert(new Vector3(0, 0), color, new Vector2(0, 0));
            vh.AddVert(new Vector3(10, 0), color, new Vector2(1, 0));
            vh.AddVert(new Vector3(0, 10), color, new Vector2(0, 1));
            vh.AddTriangle(0, 1, 2);
        }
    }

    sealed class PetalImage : RawImage
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            for (int i = 0; i < 5; i++) vh.AddVert(new Vector3(i, i), color, new Vector2(0, 0));
            vh.AddTriangle(0, 1, 2); vh.AddTriangle(0, 2, 3); vh.AddTriangle(0, 3, 4);
        }
    }

    [Fact]
    public void OverridingTypes_AreDetected_BuiltInsAreNot()
    {
        Assert.True(Graphic.BuildsOwnMesh(typeof(Triangle)));
        Assert.True(Graphic.BuildsOwnMesh(typeof(PetalImage)));
        Assert.False(Graphic.BuildsOwnMesh(typeof(Image)));
        Assert.False(Graphic.BuildsOwnMesh(typeof(RawImage)));
    }

    [Fact]
    public void PopulateMeshForRendering_EmitsTheOverridesGeometry()
    {
        using var loop = new GameLoop();
        var go = new GameObject("petal", typeof(RectTransform));
        var petal = go.AddComponent<PetalImage>();
        var vh = new VertexHelper();
        petal.PopulateMeshForRendering(vh);
        Assert.Equal(5, vh.currentVertCount);
        Assert.Equal(9, vh.currentIndexCount);
    }
}
