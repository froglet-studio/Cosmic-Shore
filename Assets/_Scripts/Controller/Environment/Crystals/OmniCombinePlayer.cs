using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Plays the OMNI COMBINE on a stage: the four elemental crystals idle with their own effects, fly in one
    /// after another (Space, Charge, Mass, Time) and turn into their shares of the omni crystal, which then
    /// dithers in over the landed plates and wakes with all four of its effects. The Elemental Converter on the
    /// Ark screen owns one (<c>ElementalConverterView</c>).
    ///
    /// <para><b>The motion is <see cref="OmniCombineCore"/>, a line-for-line port of the Omni Combine Lab</b>
    /// (Docs/Studios/OmniCombineLab.html), held to it vertex for vertex by
    /// <c>Tools/Build/omni_combine_parity/run.sh</c>. This component only turns its positions into meshes:
    /// every crystal is built here from the art file's own geometry (the shipped elemental FBXs are other
    /// exports, so their vertices cannot carry the art file's morph targets - OMNI_COMBINE_LAB.md).</para>
    ///
    /// <para><b>Meshes are unwelded per triangle with one normal per FACE.</b> The art data shares a slab's
    /// corners between its faces; drawn welded, the crystals would shade smooth, and the Charge discharge -
    /// whose bake (<see cref="CrystalEdgeArcMeshBaker"/>) decides a crease by "two triangles that do NOT
    /// share raw vertices" - would find no crease at all and draw nothing. So each plate's triangles are
    /// grouped into faces first (adjacent and within the baker's own 20 degrees), every face gets its own
    /// corners, and the Charge crystal and the omni's pentagons go through the game's own bake.</para>
    ///
    /// <para><b>Stepped by its owner, on unscaled time.</b> Every menu screen but HOME runs at
    /// <c>Time.timeScale = 0</c> (<c>ScreenSwitcher.NavigateTo</c>), so <see cref="Tick"/> takes its
    /// delta from the caller.</para>
    /// </summary>
    public sealed class OmniCombinePlayer : MonoBehaviour
    {
        static readonly int OpacityLower = Shader.PropertyToID("_opacity");     // ChargeCrystal (FadeIn's name)
        static readonly int OpacityUpper = Shader.PropertyToID("_Opacity");     // OmniCrystalFresnelShader
        static readonly int ArcIntensity = Shader.PropertyToID("_ArcIntensity");
        static readonly int NodeIntensity = Shader.PropertyToID("_NodeIntensity");
        const float CoplanarDegrees = 20f;   // CrystalEdgeArcMeshBaker.CoplanarAngleDegrees

        OmniCombineConfigSO _config;
        OmniCombineCore _core;
        OmniCombineParams _params;
        readonly Piece[] _crystals = new Piece[4];
        Piece _omni;
        MeshRenderer _omniEdges;
        Mesh _omniEdgesMesh;
        float[][] _poses;
        float[] _effect = new float[4];
        float[] _omniPose;
        MaterialPropertyBlock _block;
        float _chargeArcBase, _chargeNodeBase, _edgesArcBase, _edgesNodeBase;
        double _t;
        bool _playing;
        System.Random _rng;

        /// <summary>Seconds into the current play.</summary>
        public double PlayTime => _t;
        /// <summary>The farthest a crystal reaches from the stage centre, in world units, over any play.</summary>
        public float FramingRadius { get; private set; }
        /// <summary>How far from the stage centre anything drawn reaches RIGHT NOW, in world units: the four spread
        /// out while they wait, less as each lands, down to the woken omni. The view eases its camera on it.</summary>
        public float StageRadius { get; private set; }
        /// <summary>Seconds from a play's start until the omni has fully woken.</summary>
        public double Duration => OmniCombineCore.MorphEnds(_params) + _config.HandOffSeconds + _params.EffectFade;

        /// <summary>A player under <paramref name="parent"/>, its crystals built and hidden until <see cref="Play"/>.
        /// Null (and an error naming what is missing) when the config cannot build one.</summary>
        public static OmniCombinePlayer Create(Transform parent, OmniCombineConfigSO config)
        {
            if (!config || !config.CombineData)
            {
                CSDebug.LogError("[OmniCombinePlayer] No OmniCombineConfig, or it has no combine data: the converter cannot play. " +
                                 "Run Tools/Build/author_elemental_converter.py.");
                return null;
            }
            if (!config.ChargeMaterial || !config.ShellMaterial || !config.OmniMaterial || !config.OmniChargeEdgesMaterial)
            {
                CSDebug.LogError("[OmniCombinePlayer] OmniCombineConfig is missing a material (Charge, shell, omni or omni charge edges).", config);
                return null;
            }
            var go = new GameObject("OmniCombine");
            go.transform.SetParent(parent, false);
            var player = go.AddComponent<OmniCombinePlayer>();
            if (player.Build(config)) return player;
            Destroy(go);
            return null;
        }

        bool Build(OmniCombineConfigSO config)
        {
            _config = config;
            OmniCombineData data;
            try
            {
                data = JsonUtility.FromJson<OmniCombineData>(config.CombineData.text);
                _core = new OmniCombineCore(data);
            }
            catch (System.Exception e)
            {
                CSDebug.LogError($"[OmniCombinePlayer] '{config.CombineData.name}' does not load: {e.Message}", config);
                return false;
            }
            _rng = new System.Random();
            _params = OmniCombineParams.LabDefault;
            _block = new MaterialPropertyBlock();
            float scale = config.OmniWorldRadius / (float)_core.OmniRadius;
            transform.localScale = Vector3.one * scale;
            FramingRadius = config.OmniWorldRadius * config.MaxStartRadii;
            var bounds = new Bounds(Vector3.zero, Vector3.one * (2f * FramingRadius / scale));

            _poses = new float[4][];
            for (int k = 0; k < 4; k++)
            {
                var d = _core.CrystalData(k);
                _poses[k] = new float[d.basis.Length];
                bool charge = k == OmniCombineCore.Charge;
                _crystals[k] = Piece.Build(transform, OmniCombineCore.Order[k], d.basis, d.tris, d.plate,
                                           charge ? config.ChargeMaterial : config.ShellMaterial, charge, bounds);
                if (_crystals[k] == null)
                {
                    CSDebug.LogError($"[OmniCombinePlayer] The {OmniCombineCore.Order[k]} crystal did not build.", config);
                    return false;
                }
            }
            _omniPose = new float[data.omni.verts.Length];
            _omni = Piece.Build(transform, "Omni", data.omni.verts, data.omni.tris, data.omni.plate, config.OmniMaterial, false, bounds);
            if (_omni == null)
            {
                CSDebug.LogError("[OmniCombinePlayer] The omni crystal did not build.", config);
                return false;
            }

            // The omni's twelve pentagons carry the discharge, baked from the omni's face-split rest mesh. The
            // pentagons never move (only the rhombi, squares and triangles do), so the bake is drawn as is.
            var source = Piece.FaceSplitSource(data.omni.verts, data.omni.tris, data.omni.plate, out _);
            _omniEdgesMesh = CrystalEdgeArcMeshBaker.Bake(source, 10);
            Destroy(source);
            if (_omniEdgesMesh)
            {
                var edges = new GameObject("OmniChargeEdges");
                edges.transform.SetParent(transform, false);
                edges.AddComponent<MeshFilter>().sharedMesh = _omniEdgesMesh;
                _omniEdges = edges.AddComponent<MeshRenderer>();
                _omniEdges.sharedMaterial = config.OmniChargeEdgesMaterial;
                _omniEdges.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _omniEdges.enabled = false;
            }
            var cm = config.ChargeMaterial;
            _chargeArcBase = cm.HasProperty(ArcIntensity) ? cm.GetFloat(ArcIntensity) : 0f;
            _chargeNodeBase = cm.HasProperty(NodeIntensity) ? cm.GetFloat(NodeIntensity) : 0f;
            var em = config.OmniChargeEdgesMaterial;
            _edgesArcBase = em.HasProperty(ArcIntensity) ? em.GetFloat(ArcIntensity) : 0f;
            _edgesNodeBase = em.HasProperty(NodeIntensity) ? em.GetFloat(NodeIntensity) : 0f;
            ShowCrystals(false);
            _omni.Renderer.enabled = false;
            return true;
        }

        /// <summary>A lead so long the four never set off on their own: they idle until <see cref="Convert"/>.</summary>
        const double Waiting = 1e9;
        /// <summary>Seconds between a press of CONVERT and the first crystal setting off.</summary>
        const double ConvertDelay = 0.3;

        /// <summary>True while the four idle, waiting for <see cref="Convert"/>.</summary>
        public bool IsWaiting => _playing && _params.Lead >= Waiting;

        /// <summary>
        /// Shows the four crystals fading in and idling with their own effects, under a freshly drawn
        /// choreography (or the one <paramref name="seed"/> names, for a repeatable play). With
        /// <paramref name="convert"/> they set off after the drawn lead; without it they wait for <see cref="Convert"/>.
        /// </summary>
        public void Begin(bool convert, int? seed = null)
        {
            if (_core == null) return;
            if (seed.HasValue) _rng = new System.Random(seed.Value);
            _params = _config.Draw(_rng);
            if (!convert) _params.Lead = Waiting;
            _t = 0;
            _playing = true;
            ShowCrystals(true);
            _omni.Renderer.enabled = false;
            if (_omniEdges) _omniEdges.enabled = false;
        }

        /// <summary>Sends the waiting crystals in from exactly where they are; once a combine has run, plays a new one.</summary>
        public void Convert()
        {
            if (_core == null) return;
            if (IsWaiting) _params.Lead = _t + ConvertDelay;
            else Begin(convert: true);
        }

        /// <summary>Advances the play by <paramref name="dt"/> seconds and writes every mesh.
        /// <paramref name="cameraWorld"/> is where the viewing camera is: the Mass depth effect reads it.</summary>
        public void Tick(float dt, Vector3 cameraWorld)
        {
            if (!_playing || _core == null) return;
            _t += Mathf.Max(0f, dt);
            var toward = transform.InverseTransformPoint(cameraWorld);
            var camDir = new D3(toward.x, toward.y, toward.z).Unit;
            double m = OmniCombineCore.MorphEnds(_params);
            double handOff = _config.HandOffSeconds;

            // The crystals: in flight until the last one lands, then held where they landed under the omni's dither.
            bool crystalsVisible = _t < m + handOff;
            if (crystalsVisible)
            {
                _core.Pose(System.Math.Min(_t, m), _params, camDir, _poses, _effect);
                float fadeIn = _config.FadeInSeconds > 0f ? (float)OmniCombineCore.Smooth(_t / _config.FadeInSeconds) : 1f;
                float reach = 0f;
                for (int k = 0; k < 4; k++)
                {
                    reach = Mathf.Max(reach, Reach(_poses[k]));
                    _crystals[k].Write(_poses[k]);
                    bool charge = k == OmniCombineCore.Charge;
                    SetBlock(_crystals[k].Renderer, charge ? OpacityLower : OpacityUpper, fadeIn,
                             charge ? _chargeArcBase * _effect[k] : -1f, charge ? _chargeNodeBase * _effect[k] : -1f);
                }
                StageRadius = reach * transform.lossyScale.x;
            }
            ShowCrystals(crystalsVisible);

            // The omni: dithers in over the landed plates, then wakes its four effects.
            bool omniVisible = _t >= m;
            _omni.Renderer.enabled = omniVisible;
            if (_omniEdges) _omniEdges.enabled = omniVisible;
            if (!omniVisible) return;
            float appear = (float)OmniCombineCore.Smooth((_t - m) / handOff);
            double fx = OmniCombineCore.Smooth((_t - m - handOff) / _params.EffectFade);
            _core.OmniPose(_t, _params, fx, camDir, _omniPose);
            _omni.Write(_omniPose);
            if (!crystalsVisible) StageRadius = Reach(_omniPose) * transform.lossyScale.x;
            SetBlock(_omni.Renderer, OpacityUpper, appear, -1f, -1f);
            if (_omniEdges) SetBlock(_omniEdges, -1, 1f, _edgesArcBase * (float)fx, _edgesNodeBase * (float)fx);
        }

        /// <summary>The largest distance of any vertex from the stage centre, in the stage's units.</summary>
        static float Reach(float[] p)
        {
            float best = 0f;
            for (int i = 0; i < p.Length; i += 3) best = Mathf.Max(best, p[i] * p[i] + p[i + 1] * p[i + 1] + p[i + 2] * p[i + 2]);
            return Mathf.Sqrt(best);
        }

        /// <summary>The face-split rest mesh the discharge bake reads (see <c>Piece.FaceSplitSource</c>); for the tests.</summary>
        internal static Mesh BuildFaceSplitSource(float[] points, int[] tris, int[] plate, out int[] faceOfTri) =>
            Piece.FaceSplitSource(points, tris, plate, out faceOfTri);

        void ShowCrystals(bool on)
        {
            foreach (var c in _crystals) if (c != null && c.Renderer.enabled != on) c.Renderer.enabled = on;
        }

        /// <summary>Read-modify-write, so another writer's override on the same renderer survives (FadeIn's lesson).
        /// A negative id or value leaves that property alone.</summary>
        void SetBlock(Renderer r, int opacityId, float opacity, float arcs, float nodes)
        {
            r.GetPropertyBlock(_block);
            if (opacityId >= 0) _block.SetFloat(opacityId, opacity);
            if (arcs >= 0f) _block.SetFloat(ArcIntensity, arcs);
            if (nodes >= 0f) _block.SetFloat(NodeIntensity, nodes);
            r.SetPropertyBlock(_block);
        }

        void OnDestroy()
        {
            foreach (var c in _crystals) c?.Dispose();
            _omni?.Dispose();
            if (_omniEdgesMesh) Destroy(_omniEdgesMesh);
        }

        /// <summary>One drawn mesh: unwelded per triangle (so each face has its own normal), fed from the core's
        /// per-vertex positions through <c>_corner</c> (drawn vertex -> art vertex) and <c>_face</c> (drawn
        /// triangle -> face, for the per-face normal).</summary>
        sealed class Piece
        {
            public MeshRenderer Renderer;
            Mesh _mesh;
            int[] _corner, _triFace;
            int _faces;
            Vector3[] _verts, _normals, _faceNormals;

            public static Piece Build(Transform parent, string name, float[] points, int[] tris, int[] plate,
                                      Material material, bool chargeBake, Bounds bounds)
            {
                var source = FaceSplitSource(points, tris, plate, out var faceOfTri);
                Mesh mesh;
                if (chargeBake)
                {
                    // The game's bake keeps one vertex per triangle corner, in triangle order - the order used here.
                    mesh = CrystalEdgeArcMeshBaker.Bake(source, 0);
                    if (!mesh) { Destroy(source); return null; }
                }
                else mesh = Unweld(source);
                Destroy(source);
                mesh.name = $"OmniCombine {name}";
                mesh.MarkDynamic();
                mesh.bounds = bounds;

                var oriented = Orient(points, tris, plate);
                var piece = new Piece
                {
                    _mesh = mesh,
                    _corner = oriented,
                    _triFace = faceOfTri,
                    _verts = new Vector3[oriented.Length],
                    _normals = new Vector3[oriented.Length],
                };
                foreach (int f in faceOfTri) piece._faces = Mathf.Max(piece._faces, f + 1);
                piece._faceNormals = new Vector3[piece._faces];

                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                piece.Renderer = go.AddComponent<MeshRenderer>();
                piece.Renderer.sharedMaterial = material;
                piece.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                piece.Renderer.receiveShadows = false;
                return piece;
            }

            public void Write(float[] pose)
            {
                for (int c = 0; c < _corner.Length; c++)
                {
                    int i = _corner[c];
                    _verts[c] = new Vector3(pose[3 * i], pose[3 * i + 1], pose[3 * i + 2]);
                }
                System.Array.Clear(_faceNormals, 0, _faces);
                for (int t = 0; t < _triFace.Length; t++)
                {
                    var a = _verts[3 * t];
                    _faceNormals[_triFace[t]] += Vector3.Cross(_verts[3 * t + 1] - a, _verts[3 * t + 2] - a);
                }
                for (int t = 0; t < _triFace.Length; t++)
                {
                    var n = _faceNormals[_triFace[t]].normalized;
                    _normals[3 * t] = _normals[3 * t + 1] = _normals[3 * t + 2] = n;
                }
                _mesh.SetVertices(_verts);
                _mesh.SetNormals(_normals);
            }

            public void Dispose()
            {
                if (_mesh) Destroy(_mesh);
            }

            /// <summary>Each triangle's three art-vertex indices, wound so the face points out of its plate
            /// (Unity's front face: Cross(b - a, c - a) outward).</summary>
            static int[] Orient(float[] p, int[] tris, int[] plate)
            {
                var centre = PlateCentres(p, plate);
                var corners = new int[tris.Length];
                for (int t = 0; t < tris.Length; t += 3)
                {
                    int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                    Vector3 pa = P(p, a), pb = P(p, b), pc = P(p, c);
                    var n = Vector3.Cross(pb - pa, pc - pa);
                    bool flip = Vector3.Dot(n, (pa + pb + pc) / 3f - centre[plate[a]]) < 0f;
                    corners[t] = a;
                    corners[t + 1] = flip ? c : b;
                    corners[t + 2] = flip ? b : c;
                }
                return corners;
            }

            /// <summary>
            /// A mesh whose every FACE has its own corners (shared within the face, never across faces), with
            /// per-face normals and outward winding - the shape an FBX import has, which is what the discharge
            /// bake reads. Faces are found per plate: triangles that share an edge and sit within the bake's
            /// own coplanar angle (the art file's quads are not all planar).
            /// </summary>
            public static Mesh FaceSplitSource(float[] p, int[] tris, int[] plate, out int[] faceOfTri)
            {
                var corners = Orient(p, tris, plate);
                int nt = corners.Length / 3;
                var normal = new Vector3[nt];
                for (int t = 0; t < nt; t++)
                {
                    Vector3 a = P(p, corners[3 * t]), b = P(p, corners[3 * t + 1]), c = P(p, corners[3 * t + 2]);
                    normal[t] = Vector3.Cross(b - a, c - a).normalized;
                }
                var parent = new int[nt];
                for (int t = 0; t < nt; t++) parent[t] = t;
                int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
                var edgeOwner = new Dictionary<long, int>();
                float coplanar = Mathf.Cos(CoplanarDegrees * Mathf.Deg2Rad);
                for (int t = 0; t < nt; t++)
                    for (int e = 0; e < 3; e++)
                    {
                        int i = corners[3 * t + e], j = corners[3 * t + (e + 1) % 3];
                        long key = i < j ? ((long)i << 32) | (uint)j : ((long)j << 32) | (uint)i;
                        if (edgeOwner.TryGetValue(key, out int o))
                        {
                            if (plate[corners[3 * o]] == plate[i] && Vector3.Dot(normal[o], normal[t]) >= coplanar)
                                parent[Find(t)] = Find(o);
                        }
                        else edgeOwner[key] = t;
                    }
                faceOfTri = new int[nt];
                var faceIds = new Dictionary<int, int>();
                for (int t = 0; t < nt; t++)
                {
                    int root = Find(t);
                    if (!faceIds.TryGetValue(root, out int id)) faceIds[root] = id = faceIds.Count;
                    faceOfTri[t] = id;
                }
                var faceNormal = new Vector3[faceIds.Count];
                for (int t = 0; t < nt; t++)
                {
                    Vector3 a = P(p, corners[3 * t]), b = P(p, corners[3 * t + 1]), c = P(p, corners[3 * t + 2]);
                    faceNormal[faceOfTri[t]] += Vector3.Cross(b - a, c - a);
                }
                var verts = new List<Vector3>();
                var normals = new List<Vector3>();
                var index = new Dictionary<long, int>();
                var outTris = new int[corners.Length];
                for (int t = 0; t < nt; t++)
                    for (int k = 0; k < 3; k++)
                    {
                        int i = corners[3 * t + k];
                        long key = ((long)faceOfTri[t] << 32) | (uint)i;
                        if (!index.TryGetValue(key, out int v))
                        {
                            index[key] = v = verts.Count;
                            verts.Add(P(p, i));
                            normals.Add(faceNormal[faceOfTri[t]].normalized);
                        }
                        outTris[3 * t + k] = v;
                    }
                var mesh = new Mesh { name = "OmniCombine source", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.SetVertices(verts);
                mesh.SetNormals(normals);
                mesh.SetTriangles(outTris, 0);
                mesh.RecalculateBounds();
                return mesh;
            }

            /// <summary>One vertex per triangle corner, in triangle order (the order the discharge bake writes).</summary>
            static Mesh Unweld(Mesh source)
            {
                var sv = source.vertices;
                var sn = source.normals;
                var st = source.triangles;
                var v = new Vector3[st.Length];
                var n = new Vector3[st.Length];
                var t = new int[st.Length];
                for (int i = 0; i < st.Length; i++)
                {
                    v[i] = sv[st[i]];
                    n[i] = sn[st[i]];
                    t[i] = i;
                }
                var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.SetVertices(v);
                mesh.SetNormals(n);
                mesh.SetTriangles(t, 0);
                return mesh;
            }

            static Vector3[] PlateCentres(float[] p, int[] plate)
            {
                int np = 0;
                foreach (int q in plate) np = Mathf.Max(np, q + 1);
                var sum = new Vector3[np];
                var count = new int[np];
                for (int i = 0; i < plate.Length; i++)
                {
                    sum[plate[i]] += P(p, i);
                    count[plate[i]]++;
                }
                for (int q = 0; q < np; q++) sum[q] /= Mathf.Max(1, count[q]);
                return sum;
            }

            static Vector3 P(float[] p, int i) => new Vector3(p[3 * i], p[3 * i + 1], p[3 * i + 2]);
        }
    }
}
