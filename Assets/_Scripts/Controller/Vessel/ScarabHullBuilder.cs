using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Builds the Scarab's hull as a procedural mesh (design: R_VesselActions/SCARAB.md §3.0).
    /// The Scarab is a dung beetle, and its silhouette is the read: a wide domed carapace split
    /// down the middle by an elytra seam, a raised pronotum shield ahead of it, a flat clypeus, the
    /// signature curved horn, and six jointed legs under the shell. Nothing else in the fleet is
    /// shaped like that, which is the point — the vessel shipped wearing <c>SparrowModel1.fbx</c>
    /// and was indistinguishable from the Sparrow in flight.
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
    /// **1**. The mesh is therefore built with two submeshes — 0 = chassis (belly, clypeus, legs)
    /// on the shared body material, 1 = carapace + pronotum + horn, which is what takes the domain
    /// colour. Authoring them the other way round would paint the underside and leave the shell
    /// grey.
    ///
    /// EVERY PROFILE FUNCTION CLAMPS BEFORE <c>Pow</c>. In float32 <c>Sin(PI)</c> is ≈ -8.74e-8 —
    /// negative — and <c>Pow(negative, fractional)</c> is NaN. One unclamped profile put a NaN Y on
    /// every vertex at t = 1, which Unity surfaced as a rejected <c>localPosition</c> assignment on
    /// the horn and `abnormal mesh bounds ... -nan(ind)` on three meshes, and which froze the
    /// puppetry because un-cullable renderers stop updating. Clamp at the source, every time.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class ScarabHullBuilder : MonoBehaviour
    {
        [Header("Proportions (world units, +Z forward)")]
        [Tooltip("Nose-to-tail length of the CARAPACE. Horn and legs extend past this — the fit " +
                 "pass measures the carapace only, so appendages cannot squash the body.")]
        [SerializeField, Min(1f)] float length = 9f;
        [Tooltip("Full width at the widest point of the carapace.")]
        [SerializeField, Min(1f)] float width = 7.4f;
        [Tooltip("Height of the carapace dome above the centreline.")]
        [SerializeField, Min(0.2f)] float domeHeight = 2.15f;
        [Tooltip("Depth of the belly plate below the centreline. A beetle is domed on top and " +
                 "nearly flat underneath — keep this well under domeHeight.")]
        [SerializeField, Min(0.05f)] float bellyDepth = 0.8f;

        [Header("Elytra & pronotum")]
        [Tooltip("Half-width of the seam gap between the two wing cases, as a fraction of the " +
                 "half-width. This groove is most of what reads as 'beetle' at speed.")]
        [SerializeField, Range(0.01f, 0.25f)] float seamFraction = 0.055f;
        [Tooltip("Where the wing cases end and the pronotum begins, along the hull (0 = tail).")]
        [SerializeField, Range(0.4f, 0.85f)] float elytraFront = 0.63f;
        [Tooltip("Where the pronotum ends and the head begins.")]
        [SerializeField, Range(0.6f, 0.98f)] float pronotumFront = 0.90f;
        [Tooltip("How far the pronotum stands proud of the elytra profile. This step is the " +
                 "second-strongest beetle read after the seam.")]
        [SerializeField, Range(1f, 1.3f)] float pronotumSwell = 1.09f;
        [Tooltip("Number of raised ridges (striae) running the length of each wing case. Zero " +
                 "leaves a bald dome, which reads as a pebble rather than a shell.")]
        [SerializeField, Range(0, 8)] int striaeCount = 4;
        [Tooltip("Height of the striae as a fraction of the dome height.")]
        [SerializeField, Range(0f, 0.15f)] float striaeDepth = 0.045f;
        [Tooltip("Mesh resolution along the length. Higher = smoother shell, more verts.")]
        [SerializeField, Range(6, 40)] int lengthSegments = 22;
        [Tooltip("Mesh resolution across one wing case.")]
        [SerializeField, Range(3, 20)] int widthSegments = 10;

        [Header("Horn")]
        [Tooltip("Length of the clypeal horn as a fraction of the hull length. 0 removes it.")]
        [SerializeField, Range(0f, 0.8f)] float hornLength = 0.42f;
        [Tooltip("How far the horn curves upward over its span, in radians of total sweep. Enough " +
                 "of it that the tip finishes ABOVE the dome — a horn that ends level with the " +
                 "shell is read as a snout, and the horn is the whole silhouette.")]
        [SerializeField, Range(0f, 1.6f)] float hornCurve = 1.25f;
        [SerializeField, Range(3, 12)] int hornSides = 7;

        [Header("Legs")]
        [Tooltip("Total leg reach as a fraction of the half-width. Beetle legs are SHORT — at 0.6 " +
                 "the hull measures wider across the legs than across the shell and reads as a " +
                 "spider.")]
        [SerializeField, Range(0f, 1.2f)] float legLength = 0.34f;
        [Tooltip("Thickness of the femur as a fraction of the half-width. The tibia is thinner.")]
        [SerializeField, Range(0.01f, 0.2f)] float legThickness = 0.055f;

        [Header("Legacy model")]
        [Tooltip("Root of the inherited FBX model instance. Its RENDERERS are disabled at build " +
                 "time so only this hull draws; its colliders, Animator and transforms are left " +
                 "alone because the vessel's impact collider and rig references live on them.")]
        [SerializeField] Transform legacyModelRoot;

        const int ChassisSubmesh = 0;
        const int ShellSubmesh = 1;

        /// <summary>
        /// One movable piece of the ship. The hull is emitted as SEVERAL of these rather than one
        /// mesh so <see cref="ScarabAnimation"/> has something to puppet: a static ship reads as
        /// a prop no matter how good the flight model is. Each part carries its own PIVOT, because
        /// a wing case that hinges about the seam and a leg that swings from its socket cannot
        /// share one origin.
        /// </summary>
        sealed class Part
        {
            public string Name;
            public Vector3 Pivot;            // pre-fit; becomes the child's localPosition
            public bool IsCarapace;          // counts toward the authored width/length fit
            public readonly List<Vector3> Verts = new();
            public readonly List<Vector2> Uvs = new();
            public readonly List<int> Chassis = new();
            public readonly List<int> Shell = new();
            public Mesh Mesh;
        }

        readonly List<Part> _parts = new();
        Part _part;                          // the one currently being emitted into

        // Convenience aliases so the geometry code below reads unchanged.
        List<Vector3> _verts => _part.Verts;
        List<Vector2> _uvs => _part.Uvs;
        List<int> _chassisTris => _part.Chassis;
        List<int> _shellTris => _part.Shell;

        Material _lastDomainMaterial;

        void Awake() => Rebuild();

        /// <summary>Right-click the component to preview the shape in the editor without entering
        /// play mode. Runtime always rebuilds in <see cref="Awake"/>, so a stale preview mesh can
        /// never ship.</summary>
        [ContextMenu("Rebuild Hull")]
        public void Rebuild()
        {
            _parts.Clear();

            float halfWidth = width * 0.5f;
            float seamHalf = halfWidth * seamFraction;

            // CORE stays on this GameObject's own renderer, because that is the object
            // VesselCustomization paints (`_shipGeometries`). The movable pieces become children
            // and inherit its materials — see PropagateMaterials.
            Begin("Core", Vector3.zero, carapace: true);
            BuildBelly(halfWidth);
            BuildClypeus(halfWidth);

            // The wing cases hinge about the seam, so their pivot is the centreline.
            Begin("elytron.r", Vector3.zero, carapace: true); BuildShell(+1f, seamHalf, halfWidth);
            Begin("elytron.l", Vector3.zero, carapace: true); BuildShell(-1f, seamHalf, halfWidth);

            Begin("pronotum", Vector3.zero, carapace: true); BuildPronotum(halfWidth);

            if (hornLength > 0.001f) BuildHorn();
            if (legLength > 0.001f) BuildLegs(halfWidth);

            // Built from RATIOS, then fitted, so `length` and `width` are the finished CARAPACE's
            // real extents. Appendages ride the same scale but are excluded from the measurement:
            // measuring them instead let the legs and horn drive the divisor and squashed the body
            // to ~70% of its authored size, which is most of why the hull read as a lump.
            FitToAuthoredExtents();
            EmitParts();
            HideLegacyModel();
        }

        /// <summary>Start emitting into a new part. Geometry is written in hull space; the pivot is
        /// subtracted at emit time and becomes the child's localPosition.</summary>
        void Begin(string name, Vector3 pivot, bool carapace = false)
        {
            _part = new Part { Name = name, Pivot = pivot, IsCarapace = carapace };
            _parts.Add(_part);
        }

        void EmitParts()
        {
            for (int i = 0; i < _parts.Count; i++)
            {
                var part = _parts[i];
                for (int v = 0; v < part.Verts.Count; v++) part.Verts[v] -= part.Pivot;

                var mesh = new Mesh { name = "Scarab_" + part.Name };
                mesh.SetVertices(part.Verts);
                mesh.SetUVs(0, part.Uvs);
                mesh.subMeshCount = 2;
                mesh.SetTriangles(part.Chassis, ChassisSubmesh);
                mesh.SetTriangles(part.Shell, ShellSubmesh);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                part.Mesh = mesh;

                if (i == 0)
                {
                    GetComponent<MeshFilter>().sharedMesh = mesh;   // Core, on our own renderer
                    continue;
                }

                var child = transform.Find(part.Name);
                if (!child)
                {
                    var go = new GameObject(part.Name) { layer = gameObject.layer };
                    child = go.transform;
                    child.SetParent(transform, false);
                    go.AddComponent<MeshFilter>();
                    go.AddComponent<MeshRenderer>();
                }
                child.localPosition = part.Pivot;
                child.localRotation = Quaternion.identity;
                child.GetComponent<MeshFilter>().sharedMesh = mesh;
            }

            PropagateMaterials(force: true);
        }

        /// <summary>
        /// Keep the movable parts wearing the same materials as the core. The fleet paints the
        /// DOMAIN colour onto slot 1 of the object listed in `VesselCustomization._shipGeometries`
        /// — which is this GameObject — and that list is serialized, so parts created at runtime
        /// can never be in it. Rather than fight that, the parts simply mirror the core, which also
        /// means they share its material instance instead of minting one each.
        /// </summary>
        void PropagateMaterials(bool force = false)
        {
            var source = GetComponent<MeshRenderer>();
            if (!source) return;
            var mats = source.sharedMaterials;
            if (mats == null || mats.Length == 0) return;

            Material domain = mats.Length > 1 ? mats[1] : mats[0];
            if (!force && domain == _lastDomainMaterial) return;
            _lastDomainMaterial = domain;

            for (int i = 0; i < transform.childCount; i++)
            {
                var r = transform.GetChild(i).GetComponent<MeshRenderer>();
                if (r) r.sharedMaterials = mats;
            }
        }

        // One reference compare per frame. The domain material is swapped by ShipHelper at spawn
        // AND on any later domain change (the domain-changer toy), and neither raises an event this
        // component could bind to, so it is watched rather than pushed.
        void LateUpdate() => PropagateMaterials();

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

        /// <summary>
        /// Remap the part parameter onto the interior of the profile's arch. Sampling the arch's
        /// literal endpoints pinches BOTH ends of the shell to a point, which is right for the head
        /// and wrong for the tail — a beetle's elytra end in a broad rounded skirt. Starting at
        /// 0.10 leaves the tail at ~2/3 width, which is what makes the silhouette read as a shell
        /// rather than a lens.
        /// </summary>
        static float ShellU(float t) => Mathf.Lerp(0.10f, 0.995f, Mathf.Clamp01(t));

        /// <summary>Half-width of the shell at longitudinal position t (0 = tail, 1 = nose), as a
        /// fraction of the half-width. Broad through the middle, tapering to a narrow head.</summary>
        static float WidthAt(float t)
        {
            float s = Mathf.Sin(Mathf.Pow(ShellU(t), 0.80f) * Mathf.PI);
            return Mathf.Pow(Mathf.Max(0f, s), 0.55f);
        }

        /// <summary>Dome height factor at t. Peaks behind the middle so the shell looks loaded at
        /// the back, then falls away toward the head shield.</summary>
        static float HeightAt(float t)
        {
            float s = Mathf.Sin(Mathf.Pow(ShellU(t), 0.95f) * Mathf.PI);
            return Mathf.Pow(Mathf.Max(0f, s), 0.65f);
        }

        float ZAt(float t) => (t - 0.5f) * length;

        // ---------------------------------------------------------------- parts

        /// <summary>
        /// One wing case: a domed quarter-shell from the seam out to the rim, running from the
        /// tail to <see cref="elytraFront"/>. The rim lands exactly on y = 0, which is where the
        /// belly's outer edge lands too, so the two close without a seam gap.
        /// </summary>
        void BuildShell(float side, float seamHalf, float halfWidth)
        {
            int baseIndex = _verts.Count;

            for (int i = 0; i <= lengthSegments; i++)
            {
                float t = i / (float)lengthSegments * elytraFront;
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
                    // Striae: shallow longitudinal ridges. Faded out at both the seam and the rim
                    // so they never break the closure with the belly or the seam groove.
                    if (striaeCount > 0 && striaeDepth > 0f)
                    {
                        float edgeFade = Mathf.Sin(v * Mathf.PI);
                        y += Mathf.Cos(v * striaeCount * Mathf.PI * 2f)
                             * striaeDepth * domeHeight * edgeFade;
                    }
                    _verts.Add(new Vector3(x, y, z));
                    _uvs.Add(new Vector2(v, t));
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

        /// <summary>
        /// The pronotum — the beetle's thorax shield. A single dome spanning the FULL width (no
        /// seam) from just under the elytra's front edge to the head, standing slightly proud of
        /// the shell profile so there is a visible step where the wing cases begin.
        /// </summary>
        void BuildPronotum(float halfWidth)
        {
            int baseIndex = _verts.Count;
            int row = widthSegments * 2 + 1;
            // Overlap the elytra slightly: a butt joint at exactly elytraFront shows daylight the
            // moment the wing cases flare.
            float tBack = elytraFront - 0.04f;
            // The pronotum spans a quarter of the hull, so it does not need the shell's resolution.
            int segments = Mathf.Max(4, lengthSegments / 2);

            for (int i = 0; i <= segments; i++)
            {
                float t = Mathf.Lerp(tBack, pronotumFront, i / (float)segments);
                float w = WidthAt(t) * halfWidth * pronotumSwell;
                float h = HeightAt(t) * domeHeight * pronotumSwell;
                float z = ZAt(t);

                for (int j = 0; j < row; j++)
                {
                    float s = j / (float)(row - 1) * 2f - 1f;        // -1 .. +1 across
                    _verts.Add(new Vector3(w * s, h * Mathf.Cos(s * Mathf.PI * 0.5f), z));
                    _uvs.Add(new Vector2((s + 1f) * 0.5f, t));
                }
            }

            for (int i = 0; i < segments; i++)
            for (int j = 0; j < row - 1; j++)
            {
                int a = baseIndex + i * row + j;
                AddQuad(_shellTris, a, a + 1, a + row + 1, a + row, flip: false);
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
                }
            }

            for (int i = 0; i < lengthSegments; i++)
            for (int j = 0; j < row - 1; j++)
            {
                int a = baseIndex + i * row + j;
                AddQuad(_chassisTris, a, a + 1, a + row + 1, a + row, flip: true);   // faces down
            }
        }

        /// <summary>
        /// The clypeus — the flat shovel a scarab pushes with. A SOLID wedge, not a plate: the
        /// first cut of this was one quad emitted twice with opposite winding on the same four
        /// vertices, which makes <c>RecalculateNormals</c> average +n and -n to zero at every
        /// corner and renders the head as a black smear. Anything double-sided needs its own
        /// vertices, or it needs thickness; this has thickness.
        /// </summary>
        void BuildClypeus(float halfWidth)
        {
            float wBack = WidthAt(pronotumFront) * halfWidth * pronotumSwell;
            float wFront = Mathf.Max(wBack * 0.72f, halfWidth * 0.30f);
            float zBack = ZAt(pronotumFront);
            float zFront = ZAt(1f) + length * 0.10f;

            float topBack = HeightAt(pronotumFront) * domeHeight * pronotumSwell;
            float botBack = -bellyDepth * HeightAt(pronotumFront) * 0.6f;
            // Tilted nose-down and thinned to a lip, so it catches light separately from the dome.
            float topFront = topBack * 0.16f;
            float botFront = topFront - Mathf.Max(0.08f, domeHeight * 0.10f);

            AddWedge(zBack, zFront, wBack, wFront, topBack, botBack, topFront, botFront,
                     _chassisTris);
        }

        /// <summary>The horn: a tapered spike off the head shield that sweeps up and forward. The
        /// single most identifying feature of the silhouette, so it rides the DOMAIN submesh.</summary>
        void BuildHorn()
        {
            float span = length * hornLength;
            float rootRadius = width * 0.075f;
            float zRoot = ZAt(pronotumFront) + length * 0.04f;
            float yRoot = HeightAt(pronotumFront) * domeHeight * 0.55f;

            Begin("horn", new Vector3(0f, yRoot, zRoot));   // hinges at the head, not the hull centre

            const int rings = 8;
            int firstRing = _verts.Count;
            float sweepScale = Mathf.Max(0.05f, hornCurve);

            // Rings stop one short of the tip and fan to a single APEX vertex. Carrying the ring
            // all the way to a zero radius instead collapses `hornSides` quads onto one point —
            // geometry that renders as nothing and validates as degenerate triangles.
            for (int r = 0; r < rings; r++)
            {
                float u = r / (float)rings;
                float sweep = u * hornCurve;
                // Arc forward and up; the taper is superlinear so the tip is a real point.
                float z = zRoot + span * Mathf.Sin(sweep) / sweepScale;
                float y = yRoot + span * (1f - Mathf.Cos(sweep)) / sweepScale;
                float radius = rootRadius * Mathf.Pow(1f - u, 1.5f);

                // The ring plane is perpendicular to the ARC TANGENT. Holding every ring in the XY
                // plane (the first cut) flattens the horn into a smear exactly where it curves
                // most, which is the part of it anybody looks at.
                Vector3 centre = new(0f, y, z);
                Vector3 axisX = Vector3.right;
                Vector3 axisY = new(0f, Mathf.Cos(sweep), -Mathf.Sin(sweep));

                for (int s = 0; s < hornSides; s++)
                {
                    float a = s / (float)hornSides * Mathf.PI * 2f;
                    _verts.Add(centre + axisX * (Mathf.Cos(a) * radius)
                                      + axisY * (Mathf.Sin(a) * radius));
                    _uvs.Add(new Vector2(s / (float)hornSides, u));
                }
            }

            int apex = AddVert(new Vector3(0f,
                                           yRoot + span * (1f - Mathf.Cos(hornCurve)) / sweepScale,
                                           zRoot + span * Mathf.Sin(hornCurve) / sweepScale),
                               new Vector2(0.5f, 1f));

            for (int r = 0; r < rings - 1; r++)
            for (int s = 0; s < hornSides; s++)
            {
                int s2 = (s + 1) % hornSides;
                int a = firstRing + r * hornSides + s;
                int b = firstRing + r * hornSides + s2;
                int c = firstRing + (r + 1) * hornSides + s;
                int d = firstRing + (r + 1) * hornSides + s2;
                AddQuad(_shellTris, a, b, d, c, flip: false);
            }

            int lastRing = firstRing + (rings - 1) * hornSides;
            for (int s = 0; s < hornSides; s++)
                AddTriangle(_shellTris, lastRing + s, lastRing + (s + 1) % hornSides, apex);
        }

        /// <summary>
        /// Six JOINTED legs, three per side: a femur out from the socket to a knee, then a tibia
        /// down and back to the foot. The knee is what makes a swing read as a leg rather than a
        /// spike rotating — the two segments sweep through different arcs from the same pivot.
        /// Front legs sit under the pronotum, which is where a beetle carries them.
        /// </summary>
        void BuildLegs(float halfWidth)
        {
            float reach = halfWidth * legLength;
            float[] anchors = { 0.22f, 0.44f, 0.72f };
            float[] sweeps = { -0.85f, -0.15f, 0.55f };              // rear legs trail, front reach

            for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < anchors.Length; i++)
            {
                float t = anchors[i];
                float w = WidthAt(t) * halfWidth;
                Vector3 root = new(w * side * 0.94f, -bellyDepth * HeightAt(t) * 0.45f, ZAt(t));
                Vector3 knee = root + new Vector3(side * reach * 0.85f,
                                                  -reach * 0.16f,
                                                  sweeps[i] * reach * 0.55f);
                Vector3 foot = knee + new Vector3(side * reach * 0.28f,
                                                  -reach * 0.70f,
                                                  sweeps[i] * reach * 0.45f);

                // Its own part, pivoted at the socket, so it swings from the body like a leg.
                Begin($"leg.{(side < 0 ? "l" : "r")}{i + 1}", root);
                AddSegment(root, knee, halfWidth * legThickness, halfWidth * legThickness * 0.7f);
                AddSegment(knee, foot, halfWidth * legThickness * 0.7f, halfWidth * legThickness * 0.25f);
            }
        }

        /// <summary>A capped four-sided tapered prism between two points. Capped because an open
        /// tube shows its own interior the moment a leg swings past the camera.</summary>
        void AddSegment(Vector3 from, Vector3 to, float radiusFrom, float radiusTo)
        {
            Vector3 axis = (to - from).normalized;
            if (axis.sqrMagnitude < 1e-6f) return;
            Vector3 up = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
            Vector3 nx = Vector3.Cross(up, axis).normalized;
            Vector3 ny = Vector3.Cross(axis, nx).normalized;

            int b = _verts.Count;
            for (int i = 0; i < 4; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 0.5f;
                Vector3 offset = nx * Mathf.Cos(a) + ny * Mathf.Sin(a);
                AddVert(from + offset * radiusFrom, new Vector2(i / 4f, 0f));
            }
            for (int i = 0; i < 4; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 0.5f;
                Vector3 offset = nx * Mathf.Cos(a) + ny * Mathf.Sin(a);
                AddVert(to + offset * radiusTo, new Vector2(i / 4f, 1f));
            }

            for (int i = 0; i < 4; i++)
            {
                int i2 = (i + 1) % 4;
                AddQuad(_chassisTris, b + i, b + i2, b + 4 + i2, b + 4 + i, flip: false);
            }
            AddQuad(_chassisTris, b + 0, b + 1, b + 2, b + 3, flip: true);
            AddQuad(_chassisTris, b + 4, b + 5, b + 6, b + 7, flip: false);
        }

        /// <summary>A solid six-faced wedge spanning two Z stations, each with its own half-width
        /// and top/bottom. Distinct vertices per face-pair, so normals stay hard at the edges.</summary>
        void AddWedge(float zBack, float zFront, float wBack, float wFront,
                      float topBack, float botBack, float topFront, float botFront,
                      List<int> tris)
        {
            int b = _verts.Count;
            AddVert(new Vector3(-wBack, topBack, zBack), new Vector2(0f, 0f));
            AddVert(new Vector3(+wBack, topBack, zBack), new Vector2(1f, 0f));
            AddVert(new Vector3(+wBack, botBack, zBack), new Vector2(1f, 0.2f));
            AddVert(new Vector3(-wBack, botBack, zBack), new Vector2(0f, 0.2f));
            AddVert(new Vector3(-wFront, topFront, zFront), new Vector2(0f, 1f));
            AddVert(new Vector3(+wFront, topFront, zFront), new Vector2(1f, 1f));
            AddVert(new Vector3(+wFront, botFront, zFront), new Vector2(1f, 0.8f));
            AddVert(new Vector3(-wFront, botFront, zFront), new Vector2(0f, 0.8f));

            AddQuad(tris, b + 0, b + 1, b + 5, b + 4, flip: false);   // top
            AddQuad(tris, b + 3, b + 2, b + 6, b + 7, flip: true);    // bottom
            AddQuad(tris, b + 0, b + 3, b + 7, b + 4, flip: true);    // left
            AddQuad(tris, b + 1, b + 2, b + 6, b + 5, flip: false);   // right
            AddQuad(tris, b + 4, b + 5, b + 6, b + 7, flip: false);   // front lip
        }

        /// <summary>
        /// Scale X/Z so the finished CARAPACE measures exactly <see cref="width"/> ×
        /// <see cref="length"/>, and centre the whole hull on the origin so the vessel's pivot is
        /// its visual centre (what the follow camera frames and what the corridor measures from).
        /// Y is left at its authored scale — <see cref="domeHeight"/> and <see cref="bellyDepth"/>
        /// are already absolute — and only recentred.
        /// </summary>
        void FitToAuthoredExtents()
        {
            bool any = false;
            Vector3 min = Vector3.zero, max = Vector3.zero;
            foreach (var part in _parts)
            {
                if (!part.IsCarapace) continue;
                foreach (var v in part.Verts)
                {
                    if (!any) { min = max = v; any = true; continue; }
                    min = Vector3.Min(min, v);
                    max = Vector3.Max(max, v);
                }
            }
            if (!any) return;

            Vector3 size = max - min;
            Vector3 centre = (min + max) * 0.5f;
            float sx = size.x > 1e-4f ? width / size.x : 1f;
            float sz = size.z > 1e-4f ? length / size.z : 1f;

            foreach (var part in _parts)
            {
                for (int i = 0; i < part.Verts.Count; i++)
                    part.Verts[i] = Fit(part.Verts[i], centre, sx, sz);
                part.Pivot = Fit(part.Pivot, centre, sx, sz);
            }
        }

        static Vector3 Fit(Vector3 p, Vector3 centre, float sx, float sz)
        {
            var q = p - centre;
            return new Vector3(q.x * sx, q.y, q.z * sz);
        }

        // ---------------------------------------------------------------- primitives

        int AddVert(Vector3 position, Vector2 uv)
        {
            _verts.Add(position);
            _uvs.Add(uv);
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
