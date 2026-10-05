using System.Collections.Generic;
using CosmicShore.Engine;

namespace CosmicShore.Tests;

public class RenderChangeTrackingTests
{
    [Fact]
    public void Writes_MarkTheRenderersTheyCanMove_OncePerEpoch_AndNothingWhenOff()
    {
        using var loop = new GameLoop();
        var root = new GameObject("root");
        var child = new GameObject("child");
        child.transform.SetParent(root.transform);
        var r = child.AddComponent<MeshRenderer>();
        child.AddComponent<MeshFilter>();
        var other = new GameObject("other").AddComponent<MeshRenderer>();
        var drained = new List<Renderer>();
        try
        {
            Renderer.TrackChanges = true;           // turning on marks every live renderer
            Renderer.DrainDirty(drained);
            Assert.Contains(r, drained);
            Assert.Contains(other, drained);

            Renderer.DrainDirty(drained);
            Assert.Empty(drained);                   // nothing changed since

            root.transform.localRotation = Quaternion.Euler(0f, 30f, 0f); // an ancestor moves
            root.transform.localPosition = new Vector3(1f, 2f, 3f);        // again, same epoch
            Renderer.DrainDirty(drained);
            Assert.Equal(new[] { (Renderer)r }, drained); // once, and not the unrelated renderer

            child.transform.SetParent(null);         // a reparent
            Renderer.DrainDirty(drained);
            Assert.Equal(new[] { (Renderer)r }, drained);

            other.sharedMaterials = new Material[1]; // a material assignment
            child.GetComponent<MeshFilter>().sharedMesh = new Mesh(); // a mesh assignment
            Renderer.DrainDirty(drained);
            Assert.Equal(2, drained.Count);

            Renderer.TrackChanges = false;
            root.transform.localScale = Vector3.one * 2f;
            other.sharedMaterials = new Material[1];
            Renderer.DrainDirty(drained);
            Assert.Empty(drained);
        }
        finally { Renderer.TrackChanges = false; }
    }
}
