using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A Shrike nest flora (design: <c>R_VesselActions/SHRIKE.md</c> §3.5): a stem and a bud that GROWS LARGE MISSILES
    /// you can see (Garrett, 2026-10-15: "collecting a crystal should morph it into a nest flora that visibly grows large
    /// missiles"). A collected crystal becomes one; detaching with the Mass ability leaves one on top of the nest it
    /// leaves behind, carrying a single missile.
    ///
    /// <para>The missiles stand round the bud like petals, point up: the ready ones full size, the growing one swelling
    /// from nothing as <see cref="ShrikeRules.GrowTick"/> fills it. The executor owns the counts; this object only draws
    /// them, so every peer that runs the same presses draws the same flora.</para>
    ///
    /// <para>Visual only: no collider, nothing touches it but the executor's own queries.</para>
    /// </summary>
    public sealed class ShrikeNestFlora : MonoBehaviour
    {
        Transform[] _missiles;
        Transform _bud;
        float _missileLength;
        float _clock;

        /// <summary>Make a flora at <paramref name="at"/>, up along <paramref name="up"/>, with room for
        /// <paramref name="slots"/> missiles of <paramref name="missileLength"/>, dressed in <paramref name="material"/>.</summary>
        public static ShrikeNestFlora Create(Vector3 at, Vector3 up, int slots, float missileLength, Material material)
        {
            var go = new GameObject("ShrikeNestFlora");
            go.transform.SetPositionAndRotation(at, Quaternion.FromToRotation(Vector3.up, up.sqrMagnitude > 1e-6f ? up.normalized : Vector3.up));
            var flora = go.AddComponent<ShrikeNestFlora>();
            flora.Build(Mathf.Max(1, slots), missileLength, material);
            return flora;
        }

        void Build(int slots, float missileLength, Material material)
        {
            _missileLength = missileLength;
            // The stem, rising from where the crystal was; the bud on top.
            var stem = ShrikeMeshes.Part("Stem", transform, ShrikeMeshes.Spindle(), material);
            stem.localPosition = new Vector3(0f, missileLength * 0.35f, 0f);
            stem.localScale = new Vector3(0.6f, missileLength * 0.35f, 0.6f);
            _bud = ShrikeMeshes.Part("Bud", transform, ShrikeMeshes.Bud(), material);
            _bud.localPosition = new Vector3(0f, missileLength * 0.75f, 0f);
            _bud.localScale = Vector3.one * (missileLength * 0.3f);

            _missiles = new Transform[slots];
            for (int i = 0; i < slots; i++)
            {
                float a = i * Mathf.PI * 2f / slots;
                var m = ShrikeMeshes.Part("Missile" + i, transform, ShrikeMeshes.Missile(), material);
                m.localPosition = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (missileLength * 0.42f) + new Vector3(0f, missileLength * 0.8f, 0f);
                m.localRotation = Quaternion.Euler(-90f, 0f, 0f) * Quaternion.AngleAxis(-a * Mathf.Rad2Deg, Vector3.forward);
                m.localScale = Vector3.zero;
                _missiles[i] = m;
            }
        }

        /// <summary>The octahedral bud, the part a collected crystal folds into (<see cref="ShrikeCrystalMorph"/>).</summary>
        public Transform Bud => _bud;

        static readonly int DarkColorId = Shader.PropertyToID("_DarkColor");
        static readonly int BrightColorId = Shader.PropertyToID("_BrightColor");
        static readonly int Color1Id = Shader.PropertyToID("_Color1");
        static readonly int Color2Id = Shader.PropertyToID("_Color2");

        /// <summary>
        /// Holds the flora's PHOTONS while a morph draws in its place: every part's renderer is forced off, and nothing
        /// else changes (the executor's missile count and growth carry on). <see cref="Renderer.forceRenderingOff"/>
        /// composes with anything else that enables or disables a part.
        /// </summary>
        public void SetMorphStandIn(bool standIn)
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++) renderers[i].forceRenderingOff = standIn;
        }

        /// <summary>The colour pair the bud draws, read as (dark, bright): <c>_DarkColor</c>/<c>_BrightColor</c> where
        /// its material has them, else the hull material's <c>_Color1</c>/<c>_Color2</c>. Alpha forced to 1.</summary>
        public bool TryGetColours(out Color dark, out Color bright)
        {
            dark = bright = default;
            var mat = _bud && _bud.TryGetComponent<Renderer>(out var r) ? r.sharedMaterial : null;
            if (!mat) return false;
            if (mat.HasProperty(DarkColorId) && mat.HasProperty(BrightColorId))
            {
                dark = mat.GetColor(DarkColorId);
                bright = mat.GetColor(BrightColorId);
            }
            else if (mat.HasProperty(Color1Id) && mat.HasProperty(Color2Id))
            {
                dark = mat.GetColor(Color2Id);
                bright = mat.GetColor(Color1Id);
            }
            else return false;
            dark.a = bright.a = 1f;
            return true;
        }

        /// <summary>The world position a missile leaves from: the top of the bud.</summary>
        public Vector3 LaunchPoint => _bud ? _bud.position + transform.up * _missileLength * 0.4f : transform.position;

        /// <summary>Draw <paramref name="ready"/> full missiles and the growing one at <paramref name="growing01"/>.</summary>
        public void Show(int ready, float growing01)
        {
            if (_missiles == null) return;
            _clock += Time.deltaTime;
            for (int i = 0; i < _missiles.Length; i++)
            {
                float s = i < ready ? 1f : i == ready ? Mathf.Clamp01(growing01) : 0f;
                // A ready missile breathes, so the flora reads as armed from across the cell.
                if (i < ready) s *= 1f + 0.06f * Mathf.Sin(_clock * 5f + i);
                _missiles[i].localScale = new Vector3(0.45f, 0.45f, 1f) * (_missileLength * s);
            }
            if (_bud) _bud.localRotation = Quaternion.Euler(0f, _clock * 40f, 0f);
        }
    }

    /// <summary>The Shrike's small procedural meshes (turret flora, missiles), each built once per process and
    /// shared. Unit-sized: the caller scales them.</summary>
    public static class ShrikeMeshes
    {
        static Mesh _bud, _missile, _spindle;

        public static Transform Part(string label, Transform parent, Mesh mesh, Material material)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            if (material) renderer.sharedMaterial = material;
            return go.transform;
        }

        /// <summary>An octahedron of radius 1.</summary>
        public static Mesh Bud()
        {
            if (_bud) return _bud;
            Vector3[] v = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            int[] faces = { 0, 4, 3, 0, 3, 5, 0, 5, 2, 0, 2, 4, 1, 3, 4, 1, 5, 3, 1, 2, 5, 1, 4, 2 };
            return _bud = Flat("ShrikeBud", v, faces);
        }

        /// <summary>A missile along +Z, length 1 (tail at −0.5, nose at +0.5), width 1: a six-sided body, a pointed nose
        /// and four fins.</summary>
        public static Mesh Missile()
        {
            if (_missile) return _missile;
            var m = new ShrikeHullForm.MeshData();
            ShrikeHullForm.Frustum(m, Vector3.zero, -0.5f, 0.32f, 0.25f, 0.32f, 6, accent: false);
            ShrikeHullForm.Frustum(m, Vector3.zero, 0.25f, 0.32f, 0.5f, 0f, 6, accent: false);
            for (int k = 0; k < 4; k++)
                ShrikeHullForm.Box(m, Quaternion.AngleAxis(45f + k * 90f, Vector3.forward) * new Vector3(0.36f, 0f, -0.38f),
                    new Vector3(0.18f, 0.02f, 0.12f), Quaternion.AngleAxis(45f + k * 90f, Vector3.forward), accent: false);
            return _missile = ToMesh("ShrikeMissile", m);
        }

        /// <summary>A six-sided column from y −1 to +1, radius 1, tapering to 0.6 at the top.</summary>
        public static Mesh Spindle()
        {
            if (_spindle) return _spindle;
            var m = new ShrikeHullForm.MeshData();
            ShrikeHullForm.Frustum(m, Vector3.zero, -1f, 1f, 1f, 0.6f, 6, accent: false);
            // Built along Z; stand it up.
            var up = Quaternion.Euler(-90f, 0f, 0f);
            for (int i = 0; i < m.Verts.Count; i++) { m.Verts[i] = up * m.Verts[i]; m.Normals[i] = up * m.Normals[i]; }
            return _spindle = ToMesh("ShrikeSpindle", m);
        }

        static Mesh Flat(string label, Vector3[] v, int[] faces)
        {
            var verts = new Vector3[faces.Length];
            var tris = new int[faces.Length];
            for (int i = 0; i < faces.Length; i++) { verts[i] = v[faces[i]]; tris[i] = i; }
            var mesh = new Mesh { name = label, vertices = verts, triangles = tris };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh ToMesh(string label, ShrikeHullForm.MeshData m)
        {
            var mesh = new Mesh { name = label };
            mesh.SetVertices(m.Verts);
            mesh.SetNormals(m.Normals);
            mesh.SetUVs(0, m.Uvs);
            var all = new System.Collections.Generic.List<int>(m.Body);
            all.AddRange(m.Accent);
            mesh.SetTriangles(all, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
