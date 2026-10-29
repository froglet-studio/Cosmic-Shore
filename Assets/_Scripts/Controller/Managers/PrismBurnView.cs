using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Draws burning prisms as PAPER burning away (PrismPaperBurn.shader), owned and fed by <see cref="PrismBurnManager"/>.
    ///
    /// <para><b>Why proxies.</b> Prisms are drawn by Entities Graphics (PrismRenderService): their MeshRenderers are
    /// disabled, so swapping a prism's material shows nothing, and adding a per-prism burn override would split the
    /// prototypes' batches. Instead each burning prism gets an instanced PROXY - a unit cube scaled onto the prism's
    /// authored bounds - drawn with <see cref="Graphics.RenderMeshInstanced"/> (one call per tier), and the prism itself
    /// is hidden with <see cref="Prism.SetOwnerHidden"/> once the proxy fully covers it. The prism's entity, collider,
    /// mass and pooling are untouched.</para>
    ///
    /// <para><b>Tiers</b> (<see cref="PrismBurnFxConfigSO"/>): NEAR full curl, MID cheap dissolve, FAR nothing - the
    /// prism keeps its own look and vanishes at burnout. Coverage per tier is a dithered screen-door: NEAR draws the
    /// pixels with dither &lt; n*A, MID the complement (dither &gt;= 1-(1-n)*A), so across the band they never overlap or gap.
    /// The prism is hidden only at full coverage (hysteresis 0.99/0.999), so the far fade reveals it again smoothly.</para>
    ///
    /// Cost: per burning prism per frame one distance, one matrix multiply and two array writes; two instanced draws.
    /// </summary>
    public sealed class PrismBurnView
    {
        const int MaxCap = 1023;
        static readonly int BurnId = Shader.PropertyToID("_Burn");

        sealed class Entry
        {
            public Prism Prism;
            public Matrix4x4 Local;     // unit cube -> prism local (authored bounds, inflated)
            public float LitAt, EndAt, Seed;
            public bool NoHoles, Hidden, Skip;
        }

        readonly Dictionary<Prism, Entry> _entries = new();
        readonly List<Entry> _list = new();
        readonly Stack<Entry> _free = new();

        Matrix4x4[] _nearM, _midM;
        Vector4[] _nearB, _midB;
        MaterialPropertyBlock _nearProps, _midProps;
        Mesh _nearMesh, _midMesh;
        PrismBurnFxConfigSO _cfg;

        public PrismBurnFxConfigSO Config => _cfg;
        public bool Active => _cfg != null && _cfg.IsUsable;
        public bool SilentBurnout => Active && _cfg.SilentBurnout;
        public int Count => _list.Count;

        public PrismBurnView(PrismBurnFxConfigSO cfg)
        {
            _cfg = cfg;
            if (!Active) return;
            int cap = Mathf.Clamp(cfg.MaxDrawn, 1, MaxCap);
            _nearM = new Matrix4x4[cap]; _midM = new Matrix4x4[cap];
            _nearB = new Vector4[cap]; _midB = new Vector4[cap];
            _nearProps = new MaterialPropertyBlock(); _midProps = new MaterialPropertyBlock();
            _nearMesh = BuildCube(cfg.NearSubdivisions, "PrismBurnProxyNear");
            _midMesh = BuildCube(1, "PrismBurnProxyMid");
        }

        /// <summary>A new burn was lit on <paramref name="prism"/>, ending at <paramref name="endAt"/>.</summary>
        public void Begin(Prism prism, float now, float endAt, bool noHoles)
        {
            if (!Active || prism == null || _entries.ContainsKey(prism)) return;
            if (_list.Count >= _nearM.Length) return;   // over budget: this burn keeps the ordinary look

            var e = _free.Count > 0 ? _free.Pop() : new Entry();
            e.Prism = prism;
            e.LitAt = now;
            e.EndAt = Mathf.Max(endAt, now + 0.05f);
            e.NoHoles = noHoles;
            e.Hidden = false;
            // A prism another owner already hides (a swarm body, a crystal morph) is drawn elsewhere: leave it alone.
            e.Skip = prism.OwnerHidden;
            e.Seed = (Mathf.Abs(prism.GetInstanceID() * 0.6180339f) % 61f) + 1f;

            var mf = prism.GetComponent<MeshFilter>();
            var b = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
            var size = Vector3.Max(b.size, Vector3.one * 0.01f) * _cfg.Inflate;
            e.Local = Matrix4x4.TRS(b.center, Quaternion.identity, size);

            _entries.Add(prism, e);
            _list.Add(e);
        }

        /// <summary>The burn was upgraded to convert-on-burnout: the prism will survive, so it must not burn through.</summary>
        public void SetNoHoles(Prism prism)
        {
            if (prism != null && _entries.TryGetValue(prism, out var e)) e.NoHoles = true;
        }

        /// <summary>The burn ended (burnout resolved, destroyed elsewhere, or cleared). Shows the prism again.</summary>
        public void End(Prism prism)
        {
            if (ReferenceEquals(prism, null) || !_entries.TryGetValue(prism, out var e)) return;
            _entries.Remove(prism);
            int i = _list.IndexOf(e);
            if (i >= 0) { _list[i] = _list[_list.Count - 1]; _list.RemoveAt(_list.Count - 1); }
            Release(e);
        }

        public void Clear()
        {
            foreach (var e in _list) Release(e);
            _list.Clear();
            _entries.Clear();
        }

        void Release(Entry e)
        {
            // Destroyed or not: a destroyed prism is not drawn anyway, and a restorable one must not come back invisible.
            if (e.Hidden && e.Prism != null) e.Prism.SetOwnerHidden(false);
            e.Prism = null;
            e.Hidden = false;
            _free.Push(e);
        }

        /// <summary>Every frame: tier, coverage and progress per burn; hide/show the prisms; two instanced draws.</summary>
        public void Draw(float now)
        {
            if (!Active || _list.Count == 0) return;
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 eye = cam.transform.position;

            float n0 = _cfg.NearDistance - _cfg.NearBand * 0.5f, n1 = _cfg.NearDistance + _cfg.NearBand * 0.5f;
            float f0 = _cfg.MidDistance - _cfg.FarBand * 0.5f, f1 = _cfg.MidDistance + _cfg.FarBand * 0.5f;
            float appear = Mathf.Max(_cfg.AppearSeconds, 1e-4f);
            int nearCount = 0, midCount = 0;
            Vector3 lo = new(float.MaxValue, float.MaxValue, float.MaxValue), hi = -lo;

            for (int i = 0; i < _list.Count; i++)
            {
                var e = _list[i];
                var p = e.Prism;
                if (e.Skip || p == null || p.destroyed) continue;

                var t = p.transform;
                Vector3 pos = t.position;
                float d = Vector3.Distance(eye, pos);
                float vis = 1f - Smooth(f0, f1, d);
                float a = Mathf.Clamp01((now - e.LitAt) / appear) * vis;
                // hide the prism only once the proxy fully covers it; show it again as the far fade opens gaps
                if (!e.Hidden && a >= 0.999f) { p.SetOwnerHidden(true); e.Hidden = true; }
                else if (e.Hidden && a < 0.99f) { p.SetOwnerHidden(false); e.Hidden = false; }
                if (a <= 0f) continue;

                float near = 1f - Smooth(n0, n1, d);
                float progress = Mathf.Clamp01((now - e.LitAt) / (e.EndAt - e.LitAt));
                float flags = e.NoHoles ? 2f : 0f;
                var m = t.localToWorldMatrix * e.Local;

                if (near * a > 0.001f && nearCount < _nearM.Length)
                {
                    _nearM[nearCount] = m;
                    _nearB[nearCount++] = new Vector4(progress, e.Seed, near * a, flags);
                }
                if ((1f - near) * a > 0.001f && midCount < _midM.Length)
                {
                    _midM[midCount] = m;
                    _midB[midCount++] = new Vector4(progress, e.Seed, (1f - near) * a, flags + 1f);
                }
                lo = Vector3.Min(lo, pos); hi = Vector3.Max(hi, pos);
            }

            if (nearCount + midCount == 0) return;
            var bounds = new Bounds((lo + hi) * 0.5f, hi - lo + Vector3.one * 64f);
            if (nearCount > 0) Submit(_cfg.NearMaterial, _nearMesh, _nearProps, _nearM, _nearB, nearCount, bounds);
            if (midCount > 0) Submit(_cfg.MidMaterial, _midMesh, _midProps, _midM, _midB, midCount, bounds);
        }

        static void Submit(Material mat, Mesh mesh, MaterialPropertyBlock props, Matrix4x4[] m, Vector4[] b, int count, Bounds bounds)
        {
            props.SetVectorArray(BurnId, b);   // full fixed-size array: the block's array length is set once
            var rp = new RenderParams(mat)
            {
                matProps = props,
                worldBounds = bounds,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = 0,
            };
            Graphics.RenderMeshInstanced(rp, mesh, 0, m, count);
        }

        static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / Mathf.Max(b - a, 1e-4f));
            return t * t * (3f - 2f * t);
        }

        /// <summary>A unit cube (-0.5..0.5) with <paramref name="n"/> x <paramref name="n"/> quads per face, flat normals per face.</summary>
        static Mesh BuildCube(int n, string name)
        {
            n = Mathf.Max(1, n);
            int perFace = (n + 1) * (n + 1);
            var verts = new Vector3[perFace * 6];
            var normals = new Vector3[perFace * 6];
            var tris = new int[n * n * 6 * 6];
            Vector3[] fn = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            int v = 0, ti = 0;
            for (int f = 0; f < 6; f++)
            {
                Vector3 nrm = fn[f];
                Vector3 u = f < 2 ? Vector3.forward : Vector3.right;
                Vector3 w = Vector3.Cross(nrm, u);   // Cull Off in the shader, so winding does not matter
                int start = v;
                for (int y = 0; y <= n; y++)
                for (int x = 0; x <= n; x++)
                {
                    verts[v] = nrm * 0.5f + u * ((float)x / n - 0.5f) + w * ((float)y / n - 0.5f);
                    normals[v++] = nrm;
                }
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int i0 = start + y * (n + 1) + x, i1 = i0 + 1, i2 = i0 + n + 1, i3 = i2 + 1;
                    tris[ti++] = i0; tris[ti++] = i2; tris[ti++] = i1;
                    tris[ti++] = i1; tris[ti++] = i2; tris[ti++] = i3;
                }
            }
            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.triangles = tris;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);   // the curl pushes past the cube
            return mesh;
        }

        public void Dispose()
        {
            Clear();
            if (_nearMesh != null) Object.Destroy(_nearMesh);
            if (_midMesh != null) Object.Destroy(_midMesh);
        }
    }
}
