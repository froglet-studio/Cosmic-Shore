using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Rendering;

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
    /// <c>Tools/Build/omni_combine_parity/run.sh</c>. This component only turns its positions into meshes.</para>
    ///
    /// <para><b>The Mass crystal and the omni's triangles are the GAME's</b> (designer, 2026-10-13: "fix the mass
    /// crystal so it is consistent with the gameplay"). Mass is <c>MassCrystalPrisms.asset</c>'s 60 prisms (the
    /// generator turns them onto the art file's Mass crystal - a quarter turn about Z - and re-derives the wedge
    /// morph on them) drawn with the game's <c>MassDepthPrisms</c> material, its columns stepped by the game's own
    /// <see cref="DepthRatchet"/> and <see cref="CrystalDepthPrismProfileSO"/> against the converter's camera, exactly
    /// as <see cref="CrystalDepthPrisms"/> does in play. The omni's triangles are <c>OmniCrystalTriangles.asset</c> on
    /// <c>OmniDepthPrisms</c>, turned onto the omni, and its body has their holes cut, as in play. As the Mass prisms
    /// morph, the depth shader's shape (thickness, top-face height, plate radius, reach, split) blends from the Mass
    /// material's to the omni triangles', and each wedge's column centre slides to its triangle's centre, so the three
    /// landed wedges ARE the omni's triangle prism by the time it takes over. The game meshes have their thickness
    /// baked in; the combine's Mass prisms are the thin ones, so their thickness (from the data) goes on in the shader.</para>
    ///
    /// <para><b>Every effect is the game's round-5 one</b> (Omni Motion Lab, graduated 2026-10-13): the Mass columns step
    /// with <see cref="CrystalDepthPrisms.StepColumns"/> (sealed at the horizon, rising only while hidden); the Charge
    /// crystal waits with the seam walk (its edge crawl held at zero) and the woken omni discharges in chained flux arcs
    /// (<see cref="ChargeArcs"/>, the core <see cref="CrystalChargeArcs"/> draws in play); and the woken omni plays the
    /// game's cadence (<see cref="OmniCadence"/>, <c>OmniCrystalCadence.asset</c>): its squares turn as ONE rigid hop
    /// (<see cref="SquareTurnFit"/>, the fit the game's blend shapes are built from) and its rhombi flip one cued wave per
    /// wave beat, never at once. Its squares, rhombi and pentagons swell to the game's 1.1x thickness as its effects
    /// wake, so the hand-off from the thin landed plates is continuous, and the first beat starts once it is awake.</para>
    ///
    /// <para><b>Meshes are unwelded per triangle with one normal per FACE.</b> The art data shares a slab's
    /// corners between its faces; drawn welded, the crystals would shade smooth. So each plate's triangles are
    /// grouped into faces first (adjacent and within 20 degrees: the art file's quads are not all planar) and every
    /// face gets its own corners. (Until 2026-10-13 the Charge crystal and the omni's pentagons also went through the
    /// edge-crawl bake; the Charge light is now drawn on the CPU, and Charge wears the omni's body material.)</para>
    ///
    /// <para><b>Stepped by its owner, on unscaled time.</b> Every menu screen but HOME runs at
    /// <c>Time.timeScale = 0</c> (<c>ScreenSwitcher.NavigateTo</c>), so <see cref="Tick"/> takes its
    /// delta from the caller.</para>
    /// </summary>
    public sealed class OmniCombinePlayer : MonoBehaviour
    {
        static readonly int OpacityUpper = Shader.PropertyToID("_Opacity");     // OmniCrystalFresnel / OmniDepthPrism
        static readonly int DepthStateID = Shader.PropertyToID("_DepthState");
        static readonly int DepthDrivenID = Shader.PropertyToID("_DepthDriven");
        static readonly int ThicknessID = Shader.PropertyToID("_Thickness");
        static readonly int TopHeightID = Shader.PropertyToID("_TopHeight");
        static readonly int SplitUnitID = Shader.PropertyToID("_SplitUnit");
        static readonly int ReachID = Shader.PropertyToID("_Reach");
        static readonly int FaceSplitID = Shader.PropertyToID("_FaceSplit");
        static readonly int SizeStartID = Shader.PropertyToID("_PlateScaleStart");
        const float CoplanarDegrees = 20f;   // CrystalEdgeArcMeshBaker.CoplanarAngleDegrees

        OmniCombineConfigSO _config;
        OmniCombineCore _core;
        OmniCombineParams _params;
        readonly Piece[] _crystals = new Piece[4];
        Piece _omni;
        ArcLayer _chargeArcs, _omniArcs;
        Vector3[] _strikeCentres;
        float[] _strikeRadii, _strikePlaces, _strikeSeatRadii;
        Vector3[] _strikeSeats;
        ChargeSeamWalker _walker;
        ChargeFluxChain _chain;
        ChargeArcSettings _arcSettings;
        OmniCadence _cadence;
        float _plateThickness = 1f;
        float[] _omniRest;
        OmniCombineMotion _motion;
        DepthColumns _massColumns, _triColumns;
        MeshRenderer _triangles;
        DepthShape _massShape, _triShape, _triThinShape;
        float[][] _poses, _morph;
        readonly float[] _centres = new float[12];
        readonly float[] _effect = new float[4];
        float[] _omniPose;
        Vector4[] _massPlates;
        Vector3[] _massSum;   // WriteMass's per-plate scratch, reused every frame
        int[] _massCount;
        int[][] _massSites;
        MaterialPropertyBlock _block;
        double _t;
        bool _playing;
        System.Random _rng;
        // The camera about the stage's vertical: where it is and how fast it circles, so each crystal can be sent in from
        // the side the camera will be on (OmniCombineCore.FaceCamera).
        double _cameraAzimuth, _cameraTurnRate;
        bool _cameraSeen;

        /// <summary>Seconds into the current play.</summary>
        public double PlayTime => _t;
        /// <summary>The farthest a crystal reaches from the stage centre, in world units, over any play.</summary>
        public float FramingRadius { get; private set; }
        /// <summary>How far from the stage centre anything drawn reaches RIGHT NOW, in world units: the four spread
        /// out while they wait, less as each lands, down to the woken omni. The view eases its camera on it.</summary>
        public float StageRadius { get; private set; }
        /// <summary>Seconds from a play's start until the omni has fully woken.</summary>
        public double Duration => OmniCombineCore.MorphEnds(_params) + _config.HandOffSeconds + _params.EffectFade;

        /// <summary>A player under <paramref name="parent"/>, its crystals built and hidden until <see cref="Begin"/>.
        /// Null (and an error naming what is missing) when the config cannot build one.</summary>
        public static OmniCombinePlayer Create(Transform parent, OmniCombineConfigSO config)
        {
            if (!config || !config.CombineData)
            {
                CSDebug.LogError("[OmniCombinePlayer] No OmniCombineConfig, or it has no combine data: the converter cannot play. " +
                                 "Run Tools/Build/author_elemental_converter.py.");
                return null;
            }
            if (!config.ShellMaterial || !config.OmniMaterial || !config.OmniArcsMaterial || !config.MassMaterial || !config.OmniTrianglesMaterial || !config.OmniTrianglesMesh ||
                !config.DepthProfile || !config.Cadence || !config.ChargeArcs)
            {
                CSDebug.LogError("[OmniCombinePlayer] OmniCombineConfig is missing a material, the omni triangles mesh, the depth profile, " +
                                 "the cadence or the Charge arcs profile. Run Tools/Build/author_elemental_converter.py.", config);
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
            var mass = _core.CrystalData(OmniCombineCore.Mass);
            if (mass.meshScale <= 0f || data.omniTriangleRotation == null || data.omniTriangleRotation.Length != 9 ||
                data.depth == null || data.depth.massThickness <= 0f || data.depth.omniThickness <= 0f)
            {
                CSDebug.LogError("[OmniCombinePlayer] The combine data predates the game's Mass crystal: re-run " +
                                 "Tools/Build/author_omni_combine_asset.py.", config);
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
            _morph = new float[4][];
            for (int k = 0; k < 4; k++)
            {
                var d = _core.CrystalData(k);
                _poses[k] = new float[d.basis.Length];
                int plates = 0;
                foreach (int q in d.plate) plates = Mathf.Max(plates, q + 1);
                _morph[k] = new float[plates];
                // Charge, Space and Time wear the omni's own body material, so the plates they land as are the omni's hue
                // from the start: the hand-off changes nothing at base, and Charge's only contrast is its lit state.
                var material = k == OmniCombineCore.Mass ? config.MassMaterial : config.ShellMaterial;
                _crystals[k] = Piece.Build(transform, OmniCombineCore.Order[k], d.basis, d.tris, d.plate, material,
                                           k == OmniCombineCore.Mass, bounds);
                if (_crystals[k] == null)
                {
                    CSDebug.LogError($"[OmniCombinePlayer] The {OmniCombineCore.Order[k]} crystal did not build.", config);
                    return false;
                }
            }

            // Mass: its plates, the three plates sharing each omni triangle, and the depth columns.
            int massPlates = _morph[OmniCombineCore.Mass].Length;
            if (massPlates > CrystalDepthPrisms.MaxPlates)
            {
                CSDebug.LogError($"[OmniCombinePlayer] The Mass crystal has {massPlates} plates; the depth shader holds {CrystalDepthPrisms.MaxPlates}.", config);
                return false;
            }
            _massPlates = new Vector4[massPlates];
            _massSum = new Vector3[massPlates];
            _massCount = new int[massPlates];
            var bySite = new Dictionary<int, List<int>>();
            for (int q = 0; q < massPlates; q++)
            {
                if (!bySite.TryGetValue(mass.plateSite[q], out var list)) bySite[mass.plateSite[q]] = list = new List<int>();
                list.Add(q);
            }
            _massSites = new int[massPlates][];
            foreach (var group in bySite.Values) foreach (int q in group) _massSites[q] = group.ToArray();
            _massColumns = new DepthColumns(massPlates);
            float meshUnit = mass.meshScale;   // stage units per MassCrystalPrisms unit
            // The combine's Mass prisms are thin (the art's plates); the shader gives them the thickness the game mesh bakes.
            _massShape = DepthShape.Of(config.MassMaterial, meshUnit, data.depth.massThickness);

            // The omni body, with its triangle holes cut (the depth prisms fill them, as CrystalDepthPrisms cuts them in play).
            _omniPose = new float[data.omni.verts.Length];
            var bodyTris = new List<int>();
            for (int t = 0; t < data.omni.tris.Length; t += 3)
                if (data.omni.plateOwner[data.omni.plate[data.omni.tris[t]]] != OmniCombineCore.Mass)
                    bodyTris.AddRange(new[] { data.omni.tris[t], data.omni.tris[t + 1], data.omni.tris[t + 2] });
            _omni = Piece.Build(transform, "Omni", data.omni.verts, bodyTris.ToArray(), data.omni.plate, config.OmniMaterial, false, bounds);
            if (_omni == null)
            {
                CSDebug.LogError("[OmniCombinePlayer] The omni crystal did not build.", config);
                return false;
            }

            // The omni's triangles: the game's own prisms, turned onto this omni.
            if (!BuildTriangles(config, data.omniTriangleRotation)) return false;
            // What the Mass prisms morph toward: the triangles' shape on THIN prisms, i.e. with the mesh's baked thickness.
            _triThinShape = DepthShape.Of(config.OmniTrianglesMaterial, 1f, data.depth.omniThickness);

            // The game's round-5 effects: the cadence, the square turns and thicker plates, the Charge light.
            _cadence = config.Cadence.Cadence;
            _plateThickness = config.Cadence.PlateThickness;
            _arcSettings = config.ChargeArcs.Settings;
            _omniRest = (float[])data.omni.verts.Clone();
            if (!OmniCombineMotion.TryFit(data, (float)_core.OmniRadius, _plateThickness, out _motion, out var problem)
                || !BuildChargeLight(data, bounds, out problem))
            {
                CSDebug.LogError($"[OmniCombinePlayer] {problem}", config);
                return false;
            }
            ShowCrystals(false);
            ShowOmni(false);
            return true;
        }

        /// <summary>The Charge light, as the game draws it: the seam walk over the Charge crystal's blooms and the flux
        /// arcs over the omni's pentagons (at the cadence's plate thickness), each its own additive layer.</summary>
        bool BuildChargeLight(OmniCombineData data, Bounds bounds, out string problem)
        {
            var charge = _core.CrystalData(OmniCombineCore.Charge);
            if (!ChargeSites.TryBuild(ToVectors(charge.basis), charge.tris, 1f, out var chargeSites, out problem))
            {
                problem = $"the combine's Charge crystal: {problem}";
                return false;
            }
            if (!ChargeSites.TryBuild(ToVectors(data.omni.verts), data.omni.tris, _plateThickness, out var omniSites, out problem))
            {
                problem = $"the combine's omni: {problem}";
                return false;
            }
            // The seam walk lights the petals in the omni's colours too, so they land lit the way the omni lights them.
            _chargeArcs = new ArcLayer(transform, "ChargeSeamWalk", chargeSites, _config.OmniArcsMaterial, bounds, ownCentre: true);
            _omniArcs = new ArcLayer(transform, "OmniFluxArcs", omniSites, _config.OmniArcsMaterial, bounds, ownCentre: false);
            problem = null;
            return true;
        }

        static Vector3[] ToVectors(float[] p)
        {
            var v = new Vector3[p.Length / 3];
            for (int i = 0; i < v.Length; i++) v[i] = new Vector3(p[3 * i], p[3 * i + 1], p[3 * i + 2]);
            return v;
        }

        bool BuildTriangles(OmniCombineConfigSO config, float[] r)
        {
            var mesh = config.OmniTrianglesMesh;
            if (!CrystalDepthPrisms.TryGetPlates(mesh, out var directions, out var centres, out var tops, out var plateBounds,
                                                 out float plateRadius, out var problem))
            {
                CSDebug.LogError($"[OmniCombinePlayer] {problem}", config);
                return false;
            }
            var go = new GameObject("OmniTriangles");
            go.transform.SetParent(transform, false);
            go.transform.localRotation = TriangleRotation(r);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            _triangles = go.AddComponent<MeshRenderer>();
            _triangles.sharedMaterial = config.OmniTrianglesMaterial;
            _triangles.shadowCastingMode = ShadowCastingMode.Off;
            _triangles.receiveShadows = false;
            _triShape = DepthShape.Of(config.OmniTrianglesMaterial, 1f, 1f);
            var b = mesh.bounds;
            float radius = (b.extents.magnitude + b.center.magnitude) * Mathf.Max(1f, _triShape.Reach) + _triShape.FaceSplit * _triShape.SplitUnit * 2f;
            _triangles.localBounds = new Bounds(Vector3.zero, Vector3.one * 2f * radius);
            _triColumns = new DepthColumns(directions.Length, directions, centres, tops, plateBounds, plateRadius);
            _strikeCentres = new Vector3[directions.Length];
            _strikeRadii = new float[directions.Length];
            return true;
        }

        /// <summary>The row-major rotation R (the game's triangle mesh -> this omni) as a quaternion: R's columns are
        /// where the mesh's axes go.</summary>
        internal static Quaternion TriangleRotation(float[] r) =>
            Quaternion.LookRotation(new Vector3(r[2], r[5], r[8]), new Vector3(r[1], r[4], r[7]));

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
            _params.ShaderDepth = true;    // the game's depth-prism shader draws the Mass prisms' depth effect
            _params.IdleTumble = false;    // designer, 2026-10-13: the Charge crystal's idle turn is vestigial
            if (!convert) _params.Lead = Waiting;
            _t = 0;
            if (convert) FaceCamera();
            _playing = true;
            _massColumns.Fresh = true;
            // The Charge light's own walk and chain, seeded by the play (a CONVERT that only ends the wait keeps them).
            _walker = new ChargeSeamWalker(_chargeArcs.Sites, _params.Seed, 0f);
            _chain = new ChargeFluxChain(_omniArcs.Sites, _params.Seed ^ 0x5bd1e995);
            ShowCrystals(true);
            ShowOmni(false);
        }

        /// <summary>Sends the waiting crystals in from exactly where they are; once a combine has run, plays a new one.</summary>
        public void Convert()
        {
            if (_core == null) return;
            if (IsWaiting)
            {
                _params.Lead = _t + ConvertDelay;
                FaceCamera();
            }
            else Begin(convert: true);
        }

        /// <summary>Aims every crystal but the first at the side the camera will be on as it arrives, so the rhombi that
        /// cross the omni do it behind it. Before the first frame the camera is unknown and the crystals fly straight in.</summary>
        void FaceCamera()
        {
            if (_cameraSeen) _core.FaceCamera(ref _params, _cameraAzimuth, _cameraTurnRate, _t);
        }

        /// <summary>Follows the camera round the stage's vertical: its azimuth, and how fast that turns (eased, so one
        /// uneven frame does not swing the next aim).</summary>
        void TrackCamera(D3 camDir, float dt)
        {
            double azimuth = System.Math.Atan2(camDir.X, camDir.Z);
            if (_cameraSeen && dt > 1e-4f)
            {
                double rate = OmniCombineCore.WrapAngle(azimuth - _cameraAzimuth) / dt;
                _cameraTurnRate += (rate - _cameraTurnRate) * (1 - System.Math.Exp(-dt / 0.5));
            }
            _cameraAzimuth = azimuth;
            _cameraSeen = true;
        }

        /// <summary>Advances the play by <paramref name="dt"/> seconds and writes every mesh.
        /// <paramref name="cameraWorld"/> is where the viewing camera is: the depth effects read it.</summary>
        public void Tick(float dt, Vector3 cameraWorld)
        {
            if (!_playing || _core == null) return;
            dt = Mathf.Max(0f, dt);
            _t += dt;
            var toward = transform.InverseTransformPoint(cameraWorld);
            var camDir = new D3(toward.x, toward.y, toward.z).Unit;
            TrackCamera(camDir, dt);
            double m = OmniCombineCore.MorphEnds(_params);
            double handOff = _config.HandOffSeconds;

            // The crystals: in flight until the last one lands, then held where they landed under the omni's dither.
            bool crystalsVisible = _t < m + handOff;
            if (crystalsVisible)
            {
                _core.Pose(System.Math.Min(_t, m), _params, camDir, _poses, _effect, _centres, _morph);
                float fadeIn = _config.FadeInSeconds > 0f ? (float)OmniCombineCore.Smooth(_t / _config.FadeInSeconds) : 1f;
                float reach = 0f;
                for (int k = 0; k < 4; k++)
                {
                    reach = Mathf.Max(reach, Reach(_poses[k]));
                    if (k == OmniCombineCore.Mass) continue;
                    _crystals[k].Write(_poses[k], Vector3.zero, null);
                    SetOpacity(_crystals[k].Renderer, fadeIn);
                }
                WriteMass(dt, cameraWorld, fadeIn);
                WriteSeamWalk(cameraWorld, fadeIn);
                StageRadius = reach * transform.lossyScale.x;
            }
            ShowCrystals(crystalsVisible);

            // The omni: dithers in over the landed plates, then wakes its four effects.
            bool omniVisible = _t >= m;
            ShowOmni(omniVisible);
            if (!omniVisible)
            {
                _triColumns.Fresh = true;
                return;
            }
            float appear = (float)OmniCombineCore.Smooth((_t - m) / handOff);
            double fx = OmniCombineCore.Smooth((_t - m - handOff) / _params.EffectFade);
            // The game's beats start once the omni is awake: its first move is a whole pentagon turn, never a turn
            // already under way.
            PoseOmni(fx, _t - (m + handOff + _params.EffectFade), camDir);
            _omni.Write(_omniPose, Vector3.zero, null);
            if (!crystalsVisible) StageRadius = Reach(_omniPose) * transform.lossyScale.x;
            SetOpacity(_omni.Renderer, appear);
            // The first discharge starts charging as the omni wakes.
            WriteFlux(cameraWorld, (float)fx, _t - (m + handOff));
            _triColumns.Step(_triangles.transform.InverseTransformPoint(cameraWorld), dt, _config.DepthProfile,
                             _triShape.Reach, _triShape.SizeStart, (float)fx);
            ApplyDepth(_triangles, _triColumns, _triShape, appear);
        }

        /// <summary>
        /// The game's Mass crystal: its object sits at the crystal's flight centre (so the shader's columns rise
        /// from the crystal's own centre, as in play), each plate's column centre slides to its omni triangle's
        /// centre as it morphs, and the depth shader's shape blends from the Mass material's to the triangles'.
        /// </summary>
        void WriteMass(float dt, Vector3 cameraWorld, float fadeIn)
        {
            int k = OmniCombineCore.Mass;
            var piece = _crystals[k];
            var centre = new Vector3(_centres[3 * k], _centres[3 * k + 1], _centres[3 * k + 2]);
            piece.Renderer.transform.localPosition = centre;
            var pose = _poses[k];
            var plate = _core.CrystalData(k).plate;
            var sum = _massSum;
            var count = _massCount;
            System.Array.Clear(sum, 0, sum.Length);
            System.Array.Clear(count, 0, count.Length);
            for (int i = 0; i < plate.Length; i++)
            {
                sum[plate[i]] += new Vector3(pose[3 * i], pose[3 * i + 1], pose[3 * i + 2]) - centre;
                count[plate[i]]++;
            }
            float morph = 0f;
            for (int q = 0; q < _massPlates.Length; q++)
            {
                var own = sum[q] / Mathf.Max(1, count[q]);
                var site = Vector3.zero;
                int n = 0;
                foreach (int s in _massSites[q])
                {
                    site += sum[s];
                    n += count[s];
                }
                site /= Mathf.Max(1, n);
                var c = Vector3.Lerp(own, site, _morph[k][q]);
                _massPlates[q] = new Vector4(c.x, c.y, c.z, q);
                morph += _morph[k][q];
            }
            morph /= Mathf.Max(1, _massPlates.Length);
            piece.Write(pose, centre, _massPlates);
            // The columns as CrystalDepthPrisms steps them in play: each plate's centre and top, and the plates' radius,
            // which the horizon seal and the hidden-rise test measure with.
            var shape = DepthShape.Lerp(_massShape, _triThinShape, morph);
            float radius = 0f;
            for (int q = 0; q < _massPlates.Length; q++)
            {
                var c = (Vector3)_massPlates[q];
                _massColumns.Centres[q] = c;
                _massColumns.Directions[q] = c.normalized;
                _massColumns.Tops[q] = shape.TopHeight;
                radius += c.magnitude;
            }
            _massColumns.PlateRadius = radius / Mathf.Max(1, _massPlates.Length);
            _massColumns.Step(piece.Renderer.transform.InverseTransformPoint(cameraWorld), dt, _config.DepthProfile,
                              shape.Reach, shape.SizeStart, _effect[k]);
            ApplyDepth(piece.Renderer, _massColumns, shape, fadeIn);
        }

        /// <summary>
        /// The woken omni in the game's motion: its plates swelled to the game's thickness by <paramref name="fx"/>, the
        /// rhombi flipping the wave the cadence cues, and the squares turning on it - <paramref name="beats"/> seconds into
        /// the cadence (negative: before the first beat, everything at rest).
        /// </summary>
        void PoseOmni(double fx, double beats, D3 camDir)
        {
            _motion.Swell(_core.Data.omni.verts, (float)fx, _omniRest);
            var turn = SquareTurn.None;
            float progress = 0f;
            double waveSeconds = -1;
            long wave = -1;
            if (beats >= 0)
            {
                turn = _cadence.SquaresAt((float)beats, out progress);
                if (_cadence.WaveAt((float)beats, out float secondsIn, out long index))
                {
                    wave = index;
                    waveSeconds = _core.Data.flip.first + secondsIn * _config.Cadence.WaveSpeed;
                }
            }
            _core.OmniPose(_t, _params, fx, camDir, _omniPose, _omniRest, waveSeconds, wave);
            if (turn == SquareTurn.None) return;

            var cadence = _config.Cadence;
            _motion.TurnSquares(_omniRest, _omniPose, turn == SquareTurn.Pentagon, OmniCadence.Smoother(progress),
                                cadence.PentagonLift * (float)_core.OmniRadius, cadence.TriangleLift, cadence.TriangleSide);
        }

        /// <summary>
        /// The Charge crystal's seam walk while it waits, carried on the crystal's rigid frame. It fades with the
        /// crystal's idle effect as it sets off and is gone before any petal starts to morph (the seams are the rest
        /// crystal's).
        /// </summary>
        void WriteSeamWalk(Vector3 cameraWorld, float fadeIn)
        {
            int k = OmniCombineCore.Charge;
            float morph = 0f;
            foreach (float w in _morph[k]) morph = Mathf.Max(morph, w);
            float strength = fadeIn * _effect[k] * (1f - (float)OmniCombineCore.Smooth(morph / 0.05));
            _walker.Advance((float)_t, _arcSettings);
            var layer = _chargeArcs;
            layer.Renderer.enabled = strength > 0.001f;
            if (!layer.Renderer.enabled) return;
            _core.CrystalFrame(k, _t, _params, out var centre, out var spinAxis, out double spin, out var tumbleAxis, out double tumble);
            layer.Transform.SetLocalPositionAndRotation(ToVector(centre),
                Quaternion.AngleAxis((float)(tumble * Mathf.Rad2Deg), ToVector(tumbleAxis)) *
                Quaternion.AngleAxis((float)(spin * Mathf.Rad2Deg), ToVector(spinAxis)));
            var settings = _arcSettings;
            settings.Bolts *= strength;
            settings.Glow *= strength;
            ChargeArcs.SeamWalk(layer.Sites, _walker, (float)_t, settings, layer.Writer, layer.Transform.InverseTransformPoint(cameraWorld), layer.Light);
            layer.Upload();
        }

        /// <summary>The woken omni's chained flux arcs, <paramref name="seconds"/> into their chain, ramped in by
        /// <paramref name="fx"/>.</summary>
        void WriteFlux(Vector3 cameraWorld, float fx, double seconds)
        {
            var layer = _omniArcs;
            layer.Renderer.enabled = fx > 0.001f;
            if (!layer.Renderer.enabled)
            {
                System.Array.Clear(_triColumns.Strike, 0, _triColumns.Strike.Length);
                return;
            }
            var settings = _arcSettings;
            settings.Bolts *= fx;
            settings.Glow *= fx;
            ChargeArcs.Flux(layer.Sites, _chain, (float)seconds, settings, layer.Writer, layer.Transform.InverseTransformPoint(cameraWorld), layer.Light);
            layer.Upload();
            StrikeTriangles((float)seconds, settings);
        }

        /// <summary>
        /// Lights every protracted triangle prism a flux arc passes through (<see cref="ChargeArcs.FluxStrikes"/>), as
        /// <see cref="CrystalChargeArcs"/> lights the omni's Mass columns in play: each prism's body, at its column's place,
        /// carried from the triangles' turned frame into the stage's (where the arcs are drawn).
        /// </summary>
        void StrikeTriangles(float seconds, in ChargeArcSettings settings)
        {
            var c = _triColumns;
            var turn = _triangles.transform.localRotation;
            float split = _triShape.FaceSplit * _triShape.SplitUnit;
            for (int j = 0; j < c.Count; j++)
            {
                CrystalDepthPrisms.ColumnBody(c.Centres[j], c.Bounds[j], c.State[j].x, _triShape.Reach, _triShape.SizeStart, split,
                                              out var centre, out _strikeRadii[j]);
                _strikeCentres[j] = turn * centre;
            }
            // Only a prism standing out lights, at its place as drawn (State.x, eased in with the omni's wake).
            if (_strikePlaces == null || _strikePlaces.Length != c.Count)
            {
                _strikePlaces = new float[c.Count];
                _strikeSeats = new Vector3[c.Count];
                _strikeSeatRadii = new float[c.Count];
            }
            for (int j = 0; j < c.Count; j++)
            {
                _strikePlaces[j] = c.State[j].x;
                CrystalDepthPrisms.ColumnBody(c.Centres[j], c.Bounds[j], 1f, _triShape.Reach, _triShape.SizeStart, split,
                                              out var seat, out _strikeSeatRadii[j]);
                _strikeSeats[j] = turn * seat;
            }
            ChargeArcs.FluxStrikes(_omniArcs.Sites, _chain, seconds, settings, _strikeCentres, _strikeRadii, _strikePlaces,
                                   _strikeSeats, _strikeSeatRadii, c.Count, c.Strike);
        }

        static Vector3 ToVector(D3 d) => new((float)d.X, (float)d.Y, (float)d.Z);

        void ApplyDepth(Renderer r, DepthColumns columns, DepthShape shape, float opacity)
        {
            r.GetPropertyBlock(_block);
            _block.SetVectorArray(DepthStateID, columns.State);
            _block.SetFloat(DepthDrivenID, 1f);
            _block.SetFloat(OpacityUpper, opacity);
            _block.SetFloat(ThicknessID, shape.Thickness);
            _block.SetFloat(TopHeightID, shape.TopHeight);
            _block.SetFloat(SplitUnitID, shape.SplitUnit);
            _block.SetFloat(ReachID, shape.Reach);
            _block.SetFloat(FaceSplitID, shape.FaceSplit);
            _block.SetFloat(SizeStartID, shape.SizeStart);
            r.SetPropertyBlock(_block);
        }

        /// <summary>The largest distance of any vertex from the stage centre, in the stage's units.</summary>
        static float Reach(float[] p)
        {
            float best = 0f;
            for (int i = 0; i < p.Length; i += 3) best = Mathf.Max(best, p[i] * p[i] + p[i + 1] * p[i + 1] + p[i + 2] * p[i + 2]);
            return Mathf.Sqrt(best);
        }

        /// <summary>The face-split rest mesh every piece is built from (see <c>Piece.FaceSplitSource</c>); for the tests.</summary>
        internal static Mesh BuildFaceSplitSource(float[] points, int[] tris, int[] plate, out int[] faceOfTri) =>
            Piece.FaceSplitSource(points, tris, plate, out faceOfTri);

        void ShowCrystals(bool on)
        {
            foreach (var c in _crystals) if (c != null && c.Renderer.enabled != on) c.Renderer.enabled = on;
            if (!on && _chargeArcs != null) _chargeArcs.Renderer.enabled = false;
        }

        void ShowOmni(bool on)
        {
            if (_omni != null) _omni.Renderer.enabled = on;
            if (!on && _omniArcs != null) _omniArcs.Renderer.enabled = false;
            if (_triangles) _triangles.enabled = on;
        }

        /// <summary>Read-modify-write, so another writer's override on the same renderer survives (FadeIn's lesson).
        /// (Every bolt is drawn on the CPU on the converter's own unscaled clock, so the menu's timeScale 0 freezes none.)</summary>
        void SetOpacity(Renderer r, float opacity)
        {
            r.GetPropertyBlock(_block);
            _block.SetFloat(OpacityUpper, opacity);
            r.SetPropertyBlock(_block);
        }

        void OnDestroy()
        {
            foreach (var c in _crystals) c?.Dispose();
            _omni?.Dispose();
            _chargeArcs?.Dispose();
            _omniArcs?.Dispose();
        }

        /// <summary>
        /// One depth-prism renderer's columns, stepped by the game's own <see cref="CrystalDepthPrisms.StepColumns"/>
        /// (the profile, the horizon seal and the hidden rise as in play) against this camera, and its state ramped by
        /// <c>strength</c> - 0 draws every prism sealed and solid, 1 is the ratchet as in play - so the effect hands over
        /// continuously in both directions.
        /// </summary>
        sealed class DepthColumns
        {
            public readonly Vector3[] Directions, Centres;
            public readonly float[] Tops, Bounds, Strike;
            public float PlateRadius;
            public readonly Vector4[] State = new Vector4[CrystalDepthPrisms.MaxPlates];
            public bool Fresh = true;
            readonly DepthRatchet.Column[] _columns;

            public DepthColumns(int plates) : this(plates, new Vector3[plates], new Vector3[plates], new float[plates], new float[plates], 0f) { }

            public DepthColumns(int plates, Vector3[] directions, Vector3[] centres, float[] tops, float[] bounds, float plateRadius)
            {
                _columns = new DepthRatchet.Column[plates];
                Directions = directions;
                Centres = centres;
                Tops = tops;
                Bounds = bounds;
                Strike = new float[plates];
                PlateRadius = plateRadius;
            }

            public int Count => _columns.Length;
            public float Place(int j) => _columns[j].Place;

            public void Step(Vector3 eye, float dt, CrystalDepthPrismProfileSO profile, float reach, float sizeAtTop, float strength)
            {
                if (eye.sqrMagnitude < 1e-8f || PlateRadius <= 0f) return;
                CrystalDepthPrisms.StepColumns(_columns, Directions, Centres, Tops, PlateRadius, eye, dt, Fresh, profile, reach, sizeAtTop);
                for (int j = 0; j < _columns.Length; j++)
                {
                    var c = _columns[j];
                    State[j] = new Vector4(Mathf.Lerp(1f, c.Place, strength), Mathf.Lerp(1f, c.Fade, strength),
                                           Mathf.Lerp(1f, c.LeavingPlace, strength), Strike[j]);
                }
                Fresh = false;
            }
        }

        /// <summary>One additive layer of the Charge light (<see cref="ChargeArcs"/>, on <c>CrystalChargeArcsShader</c>):
        /// its sites, a writer, the faces' light and a dynamic mesh with the stage's fixed bounds.</summary>
        sealed class ArcLayer
        {
            public readonly Transform Transform;
            public readonly MeshRenderer Renderer;
            public readonly ChargeSites Sites;
            public readonly ChargeArcMeshWriter Writer = new();
            public readonly float[] Light;
            readonly Mesh _mesh;
            readonly Bounds _bounds;
            readonly bool _ownCentre;

            public ArcLayer(Transform parent, string name, ChargeSites sites, Material material, Bounds bounds, bool ownCentre)
            {
                Sites = sites;
                Light = new float[sites.Faces.Length];
                _bounds = bounds;
                _ownCentre = ownCentre;
                _mesh = new Mesh { name = $"OmniCombine {name}", hideFlags = HideFlags.DontSave };
                _mesh.MarkDynamic();
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                Transform = go.transform;
                go.AddComponent<MeshFilter>().sharedMesh = _mesh;
                Renderer = go.AddComponent<MeshRenderer>();
                Renderer.sharedMaterial = material;
                Renderer.shadowCastingMode = ShadowCastingMode.Off;
                Renderer.receiveShadows = false;
                Renderer.enabled = false;
            }

            /// <summary>The lit faces after the bolts already written, then the whole mesh to the GPU.</summary>
            public void Upload()
            {
                for (int f = 0; f < Sites.Faces.Length; f++) Writer.Face(Sites.Faces[f], Light[f], _ownCentre);
                _mesh.Clear();
                _mesh.SetVertices(Writer.Positions);
                _mesh.SetUVs(0, Writer.Data);
                _mesh.SetTriangles(Writer.Indices, 0, false);
                _mesh.bounds = _bounds;
                Writer.Clear();
            }

            public void Dispose()
            {
                if (_mesh) Object.Destroy(_mesh);
            }
        }

        /// <summary>The depth-prism shader's shape, in one renderer's object units: what a material authors, scaled
        /// from its mesh's units, so the Mass prisms can blend into the omni triangles' shape.</summary>
        readonly struct DepthShape
        {
            public readonly float Thickness, TopHeight, SplitUnit, Reach, FaceSplit, SizeStart;

            DepthShape(float thickness, float topHeight, float splitUnit, float reach, float faceSplit, float sizeStart)
            {
                Thickness = thickness;
                TopHeight = topHeight;
                SplitUnit = splitUnit;
                Reach = reach;
                FaceSplit = faceSplit;
                SizeStart = sizeStart;
            }

            /// <param name="baked">Thickness to add on top of the material's: the mesh's baked thickness when the
            /// drawn prisms are the thin ones (stretching about the top face composes by multiplying).</param>
            public static DepthShape Of(Material m, float unit, float baked) => new(
                m.GetFloat(ThicknessID) * baked, m.GetFloat(TopHeightID) * unit, m.GetFloat(SplitUnitID) * unit,
                m.GetFloat(ReachID), m.GetFloat(FaceSplitID), m.GetFloat(SizeStartID));

            public static DepthShape Lerp(in DepthShape a, in DepthShape b, float t) => new(
                Mathf.Lerp(a.Thickness, b.Thickness, t), Mathf.Lerp(a.TopHeight, b.TopHeight, t),
                Mathf.Lerp(a.SplitUnit, b.SplitUnit, t), Mathf.Lerp(a.Reach, b.Reach, t),
                Mathf.Lerp(a.FaceSplit, b.FaceSplit, t), Mathf.Lerp(a.SizeStart, b.SizeStart, t));
        }

        /// <summary>One drawn mesh: unwelded per triangle (so each face has its own normal), fed from the core's
        /// per-vertex positions through <c>_corner</c> (drawn vertex -> art vertex) and <c>_face</c> (drawn
        /// triangle -> face, for the per-face normal). A depth piece also carries each corner's plate centre and
        /// index in TEXCOORD2, which the depth-prism shader reads.</summary>
        sealed class Piece
        {
            public MeshRenderer Renderer;
            Mesh _mesh;
            int[] _corner, _triFace, _cornerPlate;
            int _faces;
            Vector3[] _verts, _normals, _faceNormals;
            Vector4[] _uv2;

            public static Piece Build(Transform parent, string name, float[] points, int[] tris, int[] plate,
                                      Material material, bool depth, Bounds bounds)
            {
                var source = FaceSplitSource(points, tris, plate, out var faceOfTri);
                var mesh = Unweld(source);
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
                if (depth)
                {
                    piece._uv2 = new Vector4[oriented.Length];
                    piece._cornerPlate = new int[oriented.Length];
                    for (int c = 0; c < oriented.Length; c++) piece._cornerPlate[c] = plate[oriented[c]];
                }
                foreach (int f in faceOfTri) piece._faces = Mathf.Max(piece._faces, f + 1);
                piece._faceNormals = new Vector3[piece._faces];

                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                piece.Renderer = go.AddComponent<MeshRenderer>();
                piece.Renderer.sharedMaterial = material;
                piece.Renderer.shadowCastingMode = ShadowCastingMode.Off;
                piece.Renderer.receiveShadows = false;
                return piece;
            }

            /// <summary>Writes the pose (minus <paramref name="origin"/>, the object's own position) and, for a depth
            /// piece, each corner's plate centre + index from <paramref name="plateUv2"/>.</summary>
            public void Write(float[] pose, Vector3 origin, Vector4[] plateUv2)
            {
                for (int c = 0; c < _corner.Length; c++)
                {
                    int i = _corner[c];
                    _verts[c] = new Vector3(pose[3 * i], pose[3 * i + 1], pose[3 * i + 2]) - origin;
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
                // The stage-sized bounds were set once; recomputing them every frame would only shrink them.
                _mesh.SetVertices(_verts, 0, _verts.Length, MeshUpdateFlags.DontRecalculateBounds);
                _mesh.SetNormals(_normals, 0, _normals.Length, MeshUpdateFlags.DontRecalculateBounds);
                if (_uv2 != null && plateUv2 != null)
                {
                    for (int c = 0; c < _uv2.Length; c++) _uv2[c] = plateUv2[_cornerPlate[c]];
                    _mesh.SetUVs(2, _uv2, 0, _uv2.Length, MeshUpdateFlags.DontRecalculateBounds);
                }
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
            /// per-face normals and outward winding - the shape an FBX import has. Faces are found per plate:
            /// triangles that share an edge and sit within <c>CoplanarDegrees</c> (the art file's quads are not all
            /// planar).
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
                var mesh = new Mesh { name = "OmniCombine source", indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(verts);
                mesh.SetNormals(normals);
                mesh.SetTriangles(outTris, 0);
                mesh.RecalculateBounds();
                return mesh;
            }

            /// <summary>One vertex per triangle corner, in triangle order (the order <c>Write</c> fills).</summary>
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
                var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
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
