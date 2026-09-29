using CosmicShore.Engine;
using Xunit;

public class TransformWorldCacheTests : System.IDisposable
{
    readonly GameLoop _loop = new();
    public void Dispose() => _loop.Dispose();

    // The uncached composition the cache replaced, kept here as the reference.
    static Vector3 RefPos(Transform t) => t.parent is null ? t.localPosition
        : RefPos(t.parent) + RefRot(t.parent) * Vector3.Scale(RefLossy(t.parent), t.localPosition);
    static Quaternion RefRot(Transform t) => t.parent is null ? t.localRotation : RefRot(t.parent) * t.localRotation;
    static Vector3 RefLossy(Transform t) => t.parent is null ? t.localScale : Vector3.Scale(RefLossy(t.parent), t.localScale);
    static Matrix4x4 RefMatrix(Transform t) => t.parent is null
        ? Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale)
        : RefMatrix(t.parent) * Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale);

    static void AssertBitIdentical(Transform t)
    {
        Assert.Equal(RefPos(t).x, t.position.x); Assert.Equal(RefPos(t).y, t.position.y); Assert.Equal(RefPos(t).z, t.position.z);
        var r = RefRot(t); var c = t.rotation;
        Assert.Equal(r.x, c.x); Assert.Equal(r.y, c.y); Assert.Equal(r.z, c.z); Assert.Equal(r.w, c.w);
        Assert.Equal(RefLossy(t).x, t.lossyScale.x); Assert.Equal(RefLossy(t).z, t.lossyScale.z);
        var rm = RefMatrix(t); var cm = t.localToWorldMatrix;
        for (int i = 0; i < 16; i++) Assert.Equal(rm[i], cm[i]);
    }

    static Transform[] Chain(int depth)
    {
        var ts = new Transform[depth];
        for (int i = 0; i < depth; i++)
        {
            ts[i] = new GameObject($"n{i}").transform;
            if (i > 0) ts[i].SetParent(ts[i - 1], false);
            ts[i].localPosition = new Vector3(i + 0.5f, -i * 0.25f, 2f);
            ts[i].localRotation = Quaternion.Euler(10f * i, 7f, -3f * i);
            ts[i].localScale = new Vector3(1f + 0.1f * i, 0.9f, 1.2f);
        }
        return ts;
    }

    [Fact]
    public void CachedWorldPose_IsBitIdenticalToTheUncachedComposition()
    {
        var ts = Chain(8);
        foreach (var t in ts) AssertBitIdentical(t);
    }

    [Fact]
    public void FieldWriteOnAnAncestor_InvalidatesEveryDescendant()
    {
        var ts = Chain(6);
        _ = ts[5].position; // warm the cache
        ts[1].localRotation = Quaternion.Euler(45f, 0f, 90f);   // a FIELD write: no setter runs
        AssertBitIdentical(ts[5]);
        ts[0].localScale = new Vector3(2f, 3f, 4f);
        AssertBitIdentical(ts[5]);
        ts[3].position = new Vector3(100f, 0f, -50f);
        AssertBitIdentical(ts[5]);
    }

    [Fact]
    public void Reparenting_InvalidatesTheCache()
    {
        var a = Chain(3); var b = Chain(3);
        _ = a[2].position;
        a[2].SetParent(b[1], false);
        AssertBitIdentical(a[2]);
        a[2].SetParent(null, false);
        AssertBitIdentical(a[2]);
    }
}
