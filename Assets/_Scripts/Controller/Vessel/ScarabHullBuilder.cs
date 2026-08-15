using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Builds the Scarab's hull as a procedural mesh (design: R_VesselActions/SCARAB.md §3.0).
    /// The Scarab is a dung beetle, and its silhouette is the read: a wide domed carapace split
    /// down the middle by an elytra seam, a flat head shield, the signature curved horn, and six
    /// short legs tucked under the shell. Nothing else in the fleet is shaped like that, which is
    /// the point — the vessel shipped wearing <c>SparrowModel1.fbx</c> and was indistinguishable
    /// from the Sparrow in flight.
    ///
    /// WHY PROCEDURAL RATHER THAN A DIFFERENT FBX. The model hangs off the vessel as a
    /// <c>PrefabInstance</c> of the Sparrow FBX carrying ~40 per-child modifications plus stripped
    /// references from the vessel root (the hull GameObject that owns the ImpactCollider and the
    /// vessel's BoxCollider, the Animator, several transforms). Repointing that instance's guid at
    /// another FBX dangles every one of them — the exact failure `Docs/GAMECANVAS.md` records for
    /// hard-copied prefabs. So the legacy instance stays, keeping its colliders and rig wiring
    /// intact, and only its RENDERERS are switched off; this component draws the ship. When real
    /// Scarab art lands it replaces this component, not the scaffolding.
    ///
    /// MATERIAL CONTRACT (`ShipHelper.ApplyShipMaterial`): a MeshRenderer hull is painted on slot
    /// **1**. The mesh is therefore built with two submeshes — 0 = chassis (belly, head shield,
    /// legs) on the shared body material, 1 = carapace + horn, which is what takes the domain
    /// colour. Authoring them the other way round would paint the underside and leave the shell
    /// grey.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class ScarabHullBuilder : MonoBehaviour
    {
        [Header("Proportions (world units, +Z forward)")]
        [Tooltip("Nose-to-tail length of the shell.")]
        [SerializeField, Min(1f)] float length = 9f;
        [Tooltip("Full width at the widest point of the carapace.")]
        [SerializeField, Min(1f)] float width = 7.2f;
        [Tooltip("Height of the carapace dome above the centreline.")]
        [SerializeField, Min(0.2f)] float domeHeight = 2.6f;
        [Tooltip("Depth of the belly plate below the centreline. A beetle is domed on top and " +
                 "nearly flat underneath — keep this well under domeHeight.")]
        [SerializeField, Min(0.05f)] float bellyDepth = 0.85f;

        [Header("Elytra")]
        [Tooltip("Half-width of the seam gap between the two wing cases, as a fraction of the " +
                 "half-width. This groove is most of what reads as 'beetle' at speed.")]
        [SerializeField, Range(0.01f, 0.25f)] float seamFraction = 0.07f;
        [Tooltip("Mesh resolution along the length. Higher = smoother shell, more verts.")]
        [SerializeField, Range(6, 40)] int lengthSegments = 20;
        [Tooltip("Mesh resolution across one wing case.")]
        [SerializeField, Range(3, 20)] int widthSegments = 9;

        [Header("Horn")]
        [Tooltip("Length of the clypeal horn as a fraction of the hull length. 0 removes it.")]
        [SerializeField, Range(0f, 0.6f)] float hornLength = 0.34f;
        [Tooltip("How far the horn curves upward over its span, in radians of total sweep.")]
        [SerializeField, Range(0f, 1.6f)] float hornCurve = 0.95f;
        [SerializeField, Range(3, 12)] int hornSides = 6;

        [Header("Legs")]
        [Tooltip("Leg length as a fraction of the half-width.")]
        [SerializeField, Range(0f, 1f)] float legLength = 0.52f;

        [Header("Legacy model")]
        [Tooltip("Root of the inherited FBX model instance. Its RENDERERS are disabled at build " +
                 "time so only this hull draws; its colliders, Animator and transforms are left " +
                 "alone because the vessel's impact collider and rig references live on them.")]
        [SerializeField] Transform legacyModelRoot;

        static readonly int ChassisSubmesh = 0;
        static readonly int ShellSubmesh = 1;

        readonly List<Vector3> _verts = new();
        readonly List<Vector3> _normals = new();
        readonly List<Vector2> _uvs = new();
        readonly List<int> _chassisTris = new();
        readonly List<int> _shellTris = new();

        Mesh _mesh;

        void Awake() => Rebuild();

        /// <summary>Right-click the component to preview the shape in the editor without entering
        /// play mode. Runtime always rebuilds in <see cref="Awake"/>, so a stale preview mesh can
        /// never ship.</summary>
        [ContextMenu("Rebuild Hull")]
        public void Rebuild()
        {
            _verts.Clear(); _normals.Clear(); _uvs.Clear();
            _chassisTris.Clear(); _shellTris.Clear();

            float halfWidth = width * 0.5f;
            float seamHalf = halfWidth * seamFraction;

            // Carapace: one wing case per side, mirrored. Split so the seam is a real groove
            // rather than a texture line — it survives any material.
            BuildShell(+1f, seamHalf, halfWidth);
            BuildShell(-1f, seamHalf, halfWidth);
            BuildBelly(halfWidth);
            BuildHeadShield(halfWidth);
            if (hornLength > 0.001f) BuildHorn();
            if (legLength > 0.001f) BuildLegs(halfWidth);

            // The parts are built from RATIOS and then fitted, so `length` and `width` are the
            // finished hull's real extents rather than the shell's. Without this the head shield
            // and horn are purely additive and the vessel ends up ~40% longer than authored —
            // which is not just cosmetic: the occlusion corridor and the speed-tunnel camera both
            // size themselves off the hull's circumscribing radius.
            FitToAuthoredExtents();

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "ScarabHull" };
                _mesh.MarkDynamic();
            }
            _mesh.Clear();
            _mesh.indexFormat = _verts.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            _mesh.SetVertices(_verts);
            _mesh.SetUVs(0, _uvs);
            _mesh.subMeshCount = 2;
            _mesh.SetTriangles(_chassisTris, ChassisSubmesh);
            _mesh.SetTriangles(_shellTris, ShellSubmesh);
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();

            GetComponent<MeshFilter>().sharedMesh = _mesh;

            HideLegacyModel();
        }

        /// <summary>
        /// Switch off the inherited model's renderers — never its GameObjects. The vessel's
        /// ImpactCollider and BoxCollider live on that subtree, and `VesselCustomization` /
        /// `VesselStatus` hold references into it; deactivating it would silently drop the ship
        /// out of the collision world. Disabled renderers are also excluded from the occlusion
        /// corridor's hull measurement, so the corridor sizes itself to THIS hull automatically.
        /// </summary>
        void HideLegacyModel()
        {
            if (!legacyModelRoot) return;
            var renderers = legacyModelRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                if (renderers[i].transform.IsChildOf(transform)) continue;   // never hide ourselves
                renderers[i].enabled = false;
            }
        }

        // ---------------------------------------------------------------- shape profiles

        /// <summary>Half-width of the shell at longitudinal position t (0 = tail, 1 = nose).
        /// Widest just behind the middle and tapering to a narrow head — the beetle proportion,
        /// and the reason the silhouette reads as "shell first, head second".</summary>
        float WidthAt(float t)
        {
            float shaped = Mathf.Sin(Mathf.Pow(t, 0.78f) * Mathf.PI);
            return Mathf.Max(0.02f, shaped);
        }

        /// <summary>Dome height factor at t. Peaks behind the middle so the shell looks loaded at
        /// the back, then falls away toward the head shield.</summary>
        float HeightAt(float t) => Mathf.Pow(Mathf.Sin(Mathf.Pow(t, 0.85f) * Mathf.PI), 0.7f);

        float ZAt(float t) => (t - 0.45f) * length;

        // ---------------------------------------------------------------- parts

        void BuildShell(float side, float seamHalf, float halfWidth)
        {
            int baseIndex = _verts.Count;

            for (int i = 0; i <= lengthSegments; i++)
            {
                float t = i / (float)lengthSegments;
                float w = WidthAt(t) * halfWidth;
                float h = HeightAt(t) * domeHeight;
                float z = ZAt(t);
                float inner = Mathf.Min(seamHalf, w * 0.9f);

                for (int j = 0; j <= widthSegments; j++)
                {
                    float v = j / (float)widthSegments;               // 0 = seam, 1 = outer edge
                    float x = Mathf.Lerp(inner, w, v) * side;
                    // Elliptical cross-section: full dome height at the seam, falling to the rim.
                    float y = h * Mathf.Cos(v * Mathf.PI * 0.5f);
                    _verts.Add(new Vector3(x, y, z));
                    _uvs.Add(new Vector2(v, t));
                    _normals.Add(Vector3.up);
                }
            }

            // Winding flips with the mirror so both wing cases face outward.
            bool flip = side < 0f;
            for (int i = 0; i < lengthSegments; i++)
            for (int j = 0; j < widthSegments; j++)
            {
                int row = widthSegments + 1;
                int a = baseIndex + i * row + j;
                int b = a + 1;
                int c = a + row;
                int d = c + 1;
                AddQuad(_shellTris, a, b, d, c, flip);
            }
        }

        void BuildBelly(float halfWidth)
        {
            int baseIndex = _verts.Count;
            int row = widthSegments * 2 + 1;

            for (int i = 0; i <= lengthSegments; i++)
            {
                float t = i / (float)lengthSegments;
                float w = WidthAt(t) * halfWidth;
                float z = ZAt(t);

                for (int j = 0; j < row; j++)
                {
                    float s = j / (float)(row - 1) * 2f - 1f;         // -1 .. +1 across
                    float x = w * s;
                    // Shallow keel — flat-ish, deepest on the centreline.
                    float y = -bellyDepth * HeightAt(t) * Mathf.Cos(s * Mathf.PI * 0.5f);
                    _verts.Add(new Vector3(x, y, z));
                    _uvs.Add(new Vector2((s + 1f) * 0.5f, t));
                    _normals.Add(Vector3.down);
                }
            }

            for (int i = 0; i < lengthSegments; i++)
            for (int j = 0; j < row - 1; j++)
            {
                int a = baseIndex + i * row + j;
                int b = a + 1;
                int c = a + row;
                int d = c + 1;
                AddQuad(_chassisTris, a, b, d, c, flip: true);        // faces down
            }
        }

        /// <summary>The clypeus — the flat shovel a scarab pushes with. A short trapezoid ahead of
        /// the shell, tilted nose-down so it catches light separately from the carapace.</summary>
        void BuildHeadShield(float halfWidth)
        {
            float w = WidthAt(1f) * halfWidth;
            float wFront = Mathf.Max(w * 1.35f, halfWidth * 0.42f);
            float zBack = ZAt(1f);
            float zFront = zBack + length * 0.16f;
            float yBack = HeightAt(1f) * domeHeight * 0.5f;
            float yFront = -bellyDepth * 0.35f;

            int b0 = AddVert(new Vector3(-w, yBack, zBack), new Vector2(0f, 0f));
            int b1 = AddVert(new Vector3(+w, yBack, zBack), new Vector2(1f, 0f));
            int f0 = AddVert(new Vector3(-wFront, yFront, zFront), new Vector2(0f, 1f));
            int f1 = AddVert(new Vector3(+wFront, yFront, zFront), new Vector2(1f, 1f));

            AddQuad(_chassisTris, b0, b1, f1, f0, flip: false);
            AddQuad(_chassisTris, b0, b1, f1, f0, flip: true);        // double-sided: it is a plate
        }

        /// <summary>The horn: a tapered spike off the head shield that sweeps up and forward. The
        /// single most identifying feature of the silhouette, so it rides the DOMAIN submesh.</summary>
        void BuildHorn()
        {
            float span = length * hornLength;
            float rootRadius = width * 0.085f;
            float zRoot = ZAt(1f) + length * 0.1f;
            float yRoot = HeightAt(1f) * domeHeight * 0.35f;

            const int rings = 7;
            int firstRing = _verts.Count;

            for (int r = 0; r <= rings; r++)
            {
                float u = r / (float)rings;
                float sweep = u * hornCurve;
                // Arc forward and up; the taper is cubic so the tip is a real point.
                float z = zRoot + span * Mathf.Sin(sweep) / Mathf.Max(0.05f, hornCurve);
                float y = yRoot + span * (1f - Mathf.Cos(sweep)) / Mathf.Max(0.05f, hornCurve);
                float radius = rootRadius * Mathf.Pow(1f - u, 1.6f);

                for (int s = 0; s < hornSides; s++)
                {
                    float a = s / (float)hornSides * Mathf.PI * 2f;
                    _verts.Add(new Vector3(Mathf.Cos(a) * radius, y + Mathf.Sin(a) * radius, z));
                    _uvs.Add(new Vector2(s / (float)hornSides, u));
                    _normals.Add(Vector3.up);
                }
            }

            for (int r = 0; r < rings; r++)
            for (int s = 0; s < hornSides; s++)
            {
                int s2 = (s + 1) % hornSides;
                int a = firstRing + r * hornSides + s;
                int b = firstRing + r * hornSides + s2;
                int c = firstRing + (r + 1) * hornSides + s;
                int d = firstRing + (r + 1) * hornSides + s2;
                AddQuad(_shellTris, a, b, d, c, flip: false);
            }
        }

        /// <summary>Six tapered struts, three per side, splayed out and back under the shell.</summary>
        void BuildLegs(float halfWidth)
        {
            float reach = halfWidth * legLength;
            float[] anchors = { 0.28f, 0.52f, 0.76f };
            float[] sweeps = { -0.55f, -0.1f, 0.3f };                // rear legs trail, front reach

            for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < anchors.Length; i++)
            {
                float t = anchors[i];
                float w = WidthAt(t) * halfWidth;
                Vector3 root = new(w * side * 0.92f, -bellyDepth * 0.35f, ZAt(t));
                Vector3 tip = root + new Vector3(side * reach, -reach * 0.75f, sweeps[i] * reach);
                AddStrut(root, tip, halfWidth * 0.055f);
            }
        }

        /// <summary>A four-sided tapered prism from root to tip — cheap, and at leg scale nobody
        /// counts the sides.</summary>
        void AddStrut(Vector3 root, Vector3 tip, float radius)
        {
            Vector3 axis = (tip - root).normalized;
            Vector3 up = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
            Vector3 nx = Vector3.Cross(up, axis).normalized * radius;
            Vector3 ny = Vector3.Cross(axis, nx).normalized * radius;

            int b = _verts.Count;
            AddVert(root + nx + ny, new Vector2(0, 0));
            AddVert(root - nx + ny, new Vector2(1, 0));
            AddVert(root - nx - ny, new Vector2(1, 1));
            AddVert(root + nx - ny, new Vector2(0, 1));
            int tipIndex = AddVert(tip, new Vector2(0.5f, 1f));

            for (int i = 0; i < 4; i++)
            {
                int a = b + i;
                int c = b + (i + 1) % 4;
                AddTriangle(_chassisTris, a, c, tipIndex);
            }
        }

        /// <summary>
        /// Scale X/Z so the finished hull measures exactly <see cref="width"/> × <see cref="length"/>,
        /// and centre the whole thing on the origin so the vessel's pivot is its visual centre
        /// (what the follow camera frames and what the corridor measures from). Y is left at its
        /// authored scale — <see cref="domeHeight"/> and <see cref="bellyDepth"/> are already
        /// absolute — and only recentred.
        /// </summary>
        void FitToAuthoredExtents()
        {
            if (_verts.Count == 0) return;

            Vector3 min = _verts[0], max = _verts[0];
            for (int i = 1; i < _verts.Count; i++)
            {
                min = Vector3.Min(min, _verts[i]);
                max = Vector3.Max(max, _verts[i]);
            }

            Vector3 size = max - min;
            Vector3 centre = (min + max) * 0.5f;
            float sx = size.x > 1e-4f ? width / size.x : 1f;
            float sz = size.z > 1e-4f ? length / size.z : 1f;

            for (int i = 0; i < _verts.Count; i++)
            {
                var p = _verts[i] - centre;
                _verts[i] = new Vector3(p.x * sx, p.y, p.z * sz);
            }
        }

        // ---------------------------------------------------------------- primitives

        int AddVert(Vector3 position, Vector2 uv)
        {
            _verts.Add(position);
            _uvs.Add(uv);
            _normals.Add(Vector3.up);
            return _verts.Count - 1;
        }

        static void AddTriangle(List<int> tris, int a, int b, int c)
        {
            tris.Add(a); tris.Add(b); tris.Add(c);
        }

        static void AddQuad(List<int> tris, int a, int b, int c, int d, bool flip)
        {
            if (flip)
            {
                AddTriangle(tris, a, c, b);
                AddTriangle(tris, a, d, c);
            }
            else
            {
                AddTriangle(tris, a, b, c);
                AddTriangle(tris, a, c, d);
            }
        }
    }
}
