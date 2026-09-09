using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Carries a collected omni crystal's BODY onto the ball the Scarab's skimmer just forged out
    /// of it — the Scarab's bespoke replacement for the shared husk spray. Full record:
    /// <c>_Scripts/Controller/Vessel/R_VesselActions/SCARAB_CRYSTAL_MORPH.md</c>.
    ///
    /// The mode's own sentence is *"fly your skimmer through a crystal and the crystal BECOMES your
    /// ball, in place and at rest"*. Until now the crystal also burst into the shared spent-crystal
    /// spray, so the forge read as two unrelated events — something exploded, and separately a ball
    /// appeared. It now reads as one: the cage closes onto the ball's hull and the ball takes the
    /// surface from underneath it.
    ///
    /// ── The cage CLOSES; it does not fly apart into facets ────────────────────────────────────
    /// Every vertex of the crystal slides along its own ray from the ball's centre until it meets
    /// the ball's faceted hull, and takes that facet's normal
    /// (<see cref="CrystalMorphMeshBuilder.ConvexHullTarget"/>). There is no panel-to-face
    /// assignment and none is possible: the cage has 64 non-quad panels against a subdivided
    /// icosphere's 320 facets, so any 1:1 reading would leave most of the ball unclaimed. What the
    /// hull mapping gives instead is exactness at the end — the last frame lies ON the ball's real
    /// surface wearing the ball's real per-facet normals — which is the property the hand-off
    /// actually needs.
    ///
    /// ── The window is MORPH then DISSOLVE ─────────────────────────────────────────────────────
    /// <c>MorphFraction</c> of the window is the geometry; the tail is the hand-off. At the boundary
    /// the two states are EQUIVALENT — same surface, same facet normals, same colour pair — so the
    /// ball takes over there and the crystal's shells dissolve off the top of it rather than being
    /// cut. The shader's window is the geometry half ALONE, so the last staggered solid has landed
    /// before the boundary; a stagger that ran past it would leave late struts short of the surface
    /// exactly when the ball comes up behind them.
    ///
    /// The tail is a dissolve, not a cross-fade of two different-looking things. It exists because
    /// the two are drawn by different shaders in different queues and no amount of matched colour
    /// changes that: the ball is OPAQUE and z-writing (<c>BlockGraph</c>), the crystal is four
    /// alpha-blended, non-z-writing shells in the transparent queue (<c>ShepardGraph</c>). So the
    /// object that WINS is the real one, and the crystal simply stops contributing to it.
    ///
    /// ── Three things are carried across, not just position ────────────────────────────────────
    /// 1. <b>Geometry</b> — every vertex lands on the ball's own hull.
    /// 2. <b>Normals</b> — each vertex arrives wearing its facet's outward normal (TEXCOORD3,
    ///    blended by <c>CrystalMorphNormal</c>). Without it the morph lands with the CAGE's normals
    ///    on the ball's facets, and since both shaders shade from <c>(1 − N·V)⁴</c> the shape would
    ///    be right and the shading nonsense.
    /// 3. <b>Colour</b> — <c>_DullCrystalColor</c>/<c>_BrightCrystalColor</c> converge on the ball's
    ///    own <c>_DarkColor</c>/<c>_BrightColor</c>, read off the ball's LIVE property block rather
    ///    than a theme lookup, because the ball animates that pair every frame through its domain
    ///    phase. Both graphs compose the pair through the same <c>FresnelColors</c> subgraph at
    ///    power 4, so matching the pair is matching the shading.
    ///
    /// ── What makes it seamless at both ends ───────────────────────────────────────────────────
    /// • <b>It draws the crystal's own renderers.</b> Mesh, shared materials and property block are
    ///   copied off the live crystal, so frame 0 IS the crystal — including the Shepard shells' band
    ///   animation and the collectability tint. A rebuilt look-alike would pop on the one frame that
    ///   has to be free.
    /// • <b>It ends ON the real ball.</b> The target is read from the ball's own shipped hull mesh
    ///   at its own radius, so there is no second authority to drift from: retune the ball's
    ///   subdivision or size and the animation follows for free.
    /// • <b>It is parented to the ball.</b> A forged ball is at rest, but it is live and strikeable
    ///   from frame 0 — so if a pilot hits it mid-morph the crystal goes with it instead of being
    ///   left behind in space.
    /// • <b>It starts in the pose the crystal HAD.</b> Never the live transform — see
    ///   <see cref="CrystalForgeOrigin"/>.
    ///
    /// ── MECHANICALLY INSTANT, VISUALLY GRADUAL ────────────────────────────────────────────────
    /// Only the ball's PHOTONS wait (<see cref="AstroLeagueBall.SetMorphStandIn"/>). Its collider,
    /// its rigidbody and its whole strike path are live from the frame it is forged, so a pilot
    /// arriving one frame later strikes a finished ball while the crystal is still landing on it.
    /// That is the clock-material law's own division — gameplay final at the start, only photons
    /// animate (<c>Docs/PRISM_ANIMATION.md §4</c>) — applied to a hand-off.
    ///
    /// Cost: one Mesh build per forge (the cage's ~2.9k distinct points cast once each against 320
    /// facets, by best-fit facet with a barycentric verify) and ONE stamp. The geometry then runs
    /// entirely in the vertex stage off <c>_PrismClock</c>; the only per-frame writes are uniforms —
    /// the colour convergence and the tail opacity, a handful of property-block values per shell.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScarabCrystalMorph : MonoBehaviour
    {
        static readonly int MorphId = Shader.PropertyToID("_CrystalMorph");
        static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        // ShepardGraph's pair — the crystal's base body and its fresnel rim.
        static readonly int DullCrystalId = Shader.PropertyToID("_DullCrystalColor");
        static readonly int BrightCrystalId = Shader.PropertyToID("_BrightCrystalColor");

        AstroLeagueBall _ball;
        CrystalMorphConfigSO _config;
        readonly List<Renderer> _shells = new();
        readonly List<MaterialPropertyBlock> _blocks = new();
        readonly List<Color> _startDull = new();
        readonly List<Color> _startBright = new();
        Color _targetDull, _targetBright;
        bool _haveTargetColour;
        Mesh _mesh;
        float _startTime;
        float _morphSeconds;
        bool _handedOff;

        /// <summary>
        /// Stands a morph up in the pose the crystal HAD WHEN IT WAS SPENT, wearing its shells, and
        /// stamps it to close onto <paramref name="ball"/>'s hull. Runs on EVERY peer — the ball
        /// replicates <see cref="CrystalForgeOrigin"/> and each peer starts its own copy.
        ///
        /// Returns null whenever it cannot land honestly, and every one of those exits is NAMED,
        /// because they all look identical on screen (the ball appears and the crystal is gone) and
        /// the difference between "the retirement never ran", "the crystal could not be resolved on
        /// this peer" and "the ball had no hull yet" is not recoverable after the fact. A null
        /// return leaves the ball to bloom in normally — continuity of existence holds either way.
        /// </summary>
        public static ScarabCrystalMorph Begin(AstroLeagueBall ball, in CrystalForgeOrigin origin)
        {
            if (ball == null || !origin.Valid) return null;

            var hull = ball.HullMesh;
            if (hull == null)
            {
                CSDebug.LogWarning("[ScarabCrystalMorph] the ball has no hull mesh yet, so the " +
                                   "crystal cannot be told where to land — this forge falls back " +
                                   "to the ball's ordinary birth bloom. SetupVisuals runs from " +
                                   "Awake, so this means Begin ran before the ball awoke.");
                return null;
            }

            var crystal = ResolveCrystal(origin);
            if (crystal == null)
            {
                CSDebug.LogWarning($"[ScarabCrystalMorph] no crystal with id {origin.CrystalId} on " +
                                   "this peer, so there is no body to carry onto the ball — it " +
                                   "falls back to its ordinary birth bloom. The crystal is resolved " +
                                   "through the cell nearest where it was spent; a peer whose cell " +
                                   "has not finished initialising cannot find it.");
                return null;
            }

            var go = new GameObject($"CrystalMorph_{crystal.name}");
            // Parented to the BALL, not to the world: a forged ball is at rest but fully live, so a
            // pilot may strike it mid-morph and the crystal has to travel with it.
            go.transform.SetParent(ball.transform, false);
            go.transform.SetPositionAndRotation(origin.Position, origin.Rotation);
            go.transform.localScale = LocalScaleFor(ball.transform, origin.Scale);

            var morph = go.AddComponent<ScarabCrystalMorph>();
            morph._ball = ball;
            morph._config = CrystalMorphConfigSO.Instance;

            if (!morph.AdoptShells(crystal))
            {
                CSDebug.LogWarning($"[ScarabCrystalMorph] '{crystal.name}' exposed no drawable shell " +
                                   "(a model with a MeshFilter AND a MeshRenderer), so there is " +
                                   "nothing to morph — the ball falls back to its birth bloom.");
                Destroy(go);
                return null;
            }

            if (!morph.Stamp(crystal))
            {
                Destroy(go);
                return null;
            }

            CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                $"[CrystalMorph] Scarab: '{crystal.name}' at {origin.Position} closing onto a " +
                $"{ball.name} hull of {hull.triangles.Length / 3} facets over " +
                $"{morph._morphSeconds:F2}s + {morph._config.duration - morph._morphSeconds:F2}s of " +
                $"dissolve ({morph._shells.Count} shells, {morph._mesh.vertexCount} morph vertices, " +
                $"colour target {(morph._haveTargetColour ? "read" : "NOT FOUND")}).");
            return morph;
        }

        /// <summary>
        /// Finds the spent crystal on THIS peer, by the id the forge stamped. Resolved through the
        /// cell containing the pose the crystal was spent in rather than a global registry, because
        /// <c>CellRuntimeDataSO</c> is where crystals are actually indexed — and looked up from the
        /// COLLECT pose rather than the crystal's current position, which on a remote peer is
        /// usually its next home already.
        /// </summary>
        static Crystal ResolveCrystal(in CrystalForgeOrigin origin)
        {
            var cell = Cell.FindCellContaining(origin.Position) ?? Cell.FindNearestActiveCell(origin.Position);
            var runtime = cell != null ? cell.RuntimeData : null;
            if (runtime == null) return null;
            return runtime.TryGetCrystalById(origin.CrystalId, out var crystal) ? crystal : null;
        }

        /// <summary>Local scale that reproduces <paramref name="worldScale"/> under
        /// <paramref name="parent"/>. Exact here because a ball's scale is uniform
        /// (<c>_baseScale * factor</c>); the per-axis divide is written out anyway so a future
        /// non-uniform ball degrades to the right answer on each axis instead of a silent skew.</summary>
        static Vector3 LocalScaleFor(Transform parent, Vector3 worldScale)
        {
            Vector3 p = parent.lossyScale;
            return new Vector3(
                Mathf.Abs(p.x) > 1e-5f ? worldScale.x / p.x : worldScale.x,
                Mathf.Abs(p.y) > 1e-5f ? worldScale.y / p.y : worldScale.y,
                Mathf.Abs(p.z) > 1e-5f ? worldScale.z / p.z : worldScale.z);
        }

        /// <summary>
        /// Copies the crystal's model renderers onto this object — one child per shell, sharing the
        /// crystal's meshes, its shared materials and its property block.
        ///
        /// Nothing is cloned and nothing is re-authored: an omni crystal draws four coincident copies
        /// of one cage, each showing a different band of a travelling wave, and any reconstruction of
        /// that would be a second authority for the crystal's look. This is also why the copy takes
        /// the property BLOCK — <c>Crystal.ApplyColorSetTint</c> paints the collectability colour
        /// there, over the shared material, so a copy that skipped it would start on a visibly
        /// different crystal.
        /// </summary>
        bool AdoptShells(Crystal crystal)
        {
            var models = crystal.CrystalModels;
            if (models == null) return false;

            Mesh first = null;
            for (int i = 0; i < models.Count; i++)
            {
                var model = models[i]?.model;
                if (model == null) continue;
                if (!model.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null) continue;
                if (!model.TryGetComponent<MeshRenderer>(out var source)) continue;

                // ONE morph mesh drives every shell, so every shell has to be the same cage — which
                // an omni crystal's four are by construction (four coincident copies of one model,
                // differing only in which band of the travelling wave each renders). A crystal built
                // any other way would be silently drawn as shell 0's geometry, so it is dropped and
                // named instead.
                if (first == null) first = filter.sharedMesh;
                else if (filter.sharedMesh != first)
                {
                    CSDebug.LogWarning($"[ScarabCrystalMorph] '{crystal.name}' shell {i} draws " +
                                       $"'{filter.sharedMesh.name}', not the first shell's " +
                                       $"'{first.name}' — the morph carries one mesh, so this shell " +
                                       "is left out.");
                    continue;
                }

                var shell = new GameObject($"Shell{i}");
                shell.transform.SetParent(transform, false);
                shell.transform.SetLocalPositionAndRotation(model.transform.localPosition,
                                                            model.transform.localRotation);
                shell.transform.localScale = model.transform.localScale;

                shell.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var renderer = shell.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = source.sharedMaterials;
                renderer.shadowCastingMode = source.shadowCastingMode;
                renderer.receiveShadows = source.receiveShadows;
                shell.layer = model.layer;

                var block = new MaterialPropertyBlock();
                source.GetPropertyBlock(block);
                renderer.SetPropertyBlock(block);

                // The colour this shell STARTS at, so the convergence below is a lerp from what it
                // is actually drawing rather than from the shader's default.
                var mat = source.sharedMaterial;
                _startDull.Add(mat != null && mat.HasProperty(DullCrystalId)
                    ? mat.GetColor(DullCrystalId) : Color.white);
                _startBright.Add(mat != null && mat.HasProperty(BrightCrystalId)
                    ? mat.GetColor(BrightCrystalId) : Color.white);

                _shells.Add(renderer);
                _blocks.Add(block);
            }
            return _shells.Count > 0;
        }

        /// <summary>
        /// Resolves the ball's hull into this object's frame, bakes the morph mesh and writes the
        /// ONE stamp that runs the whole animation. Returns false, named, when the hull cannot be
        /// measured or the cage cannot be read.
        /// </summary>
        bool Stamp(Crystal crystal)
        {
            // The hull is read in the frame the ball DRAWS it in, then brought into this object's
            // local space — the mesh's targets have to live in the same frame as its vertices.
            Transform visual = _ball.VisualRoot;
            Matrix4x4 toLocal = transform.worldToLocalMatrix * visual.localToWorldMatrix;
            Vector3 centre = transform.InverseTransformPoint(_ball.transform.position);

            if (!CrystalMorphMeshBuilder.ConvexHullTarget.TryFromMesh(
                    _ball.HullMesh, toLocal, centre, out var target, out string hullDiagnosis))
            {
                CSDebug.LogWarning($"[ScarabCrystalMorph] cannot read the ball's hull: {hullDiagnosis}. " +
                                   "The ball falls back to its ordinary birth bloom.");
                return false;
            }

            var source = _shells[0].GetComponent<MeshFilter>().sharedMesh;
            _mesh = CrystalMorphMeshBuilder.TryBuild(source, in target,
                                                     _config.phaseNear, _config.phaseFar,
                                                     out string diagnosis);
            if (_mesh == null)
            {
                CSDebug.LogError($"[ScarabCrystalMorph] cannot morph '{crystal.name}': {diagnosis}");
                return false;
            }

            for (int i = 0; i < _shells.Count; i++)
                _shells[i].GetComponent<MeshFilter>().sharedMesh = _mesh;

            _haveTargetColour = _ball.TryGetShellColours(out _targetDull, out _targetBright);
            if (!_haveTargetColour)
                CSDebug.LogWarning("[ScarabCrystalMorph] the ball exposed no _DarkColor/_BrightColor " +
                                   "pair, so the morph will land in the CRYSTAL's colours and the " +
                                   "hand-off will show a colour change. The ball only carries that " +
                                   "pair when it is drawing with the prism fresnel material.");

            // Photons only: the ball is live, strikeable mass from the frame it was forged.
            _ball.SetMorphStandIn(true);

            _startTime = PrismClock.Now;
            _morphSeconds = _config.duration * Mathf.Clamp01(_config.morphFraction);

            // The shader's window is the GEOMETRY half only, so the LAST staggered solid has landed
            // by the time the dissolve starts. Handing it the whole duration instead would leave the
            // late struts short of the surface at the very moment the ball comes up behind them.
            var morph = new Vector3(_startTime, _morphSeconds, Mathf.Clamp01(_config.stagger));
            for (int i = 0; i < _shells.Count; i++)
            {
                _shells[i].GetPropertyBlock(_blocks[i]);
                _blocks[i].SetVector(MorphId, morph);
                _blocks[i].SetFloat(OpacityId, 1f);
                _shells[i].SetPropertyBlock(_blocks[i]);
            }
            return true;
        }

        void LateUpdate()
        {
            if (_ball == null) { Destroy(gameObject); return; }

            float elapsed = PrismClock.Now - _startTime;
            float g = Mathf.Clamp01(elapsed / Mathf.Max(1e-4f, _morphSeconds));

            // Colour convergence, finished BEFORE the hand-off so the two surfaces are already the
            // same colour when they overlap. The ball animates its own pair every frame, so the
            // TARGET is re-read rather than snapshotted — otherwise the morph converges on the
            // colour the ball wore a third of a second ago.
            if (!_handedOff)
            {
                if (_ball.TryGetShellColours(out var dark, out var bright))
                {
                    _targetDull = dark;
                    _targetBright = bright;
                    _haveTargetColour = true;
                }

                if (_haveTargetColour)
                {
                    float c = Mathf.Clamp01(g / Mathf.Clamp01(_config.colourBlendFraction <= 0f
                        ? 1f : _config.colourBlendFraction));
                    c = c * c * (3f - 2f * c);
                    for (int i = 0; i < _shells.Count; i++)
                    {
                        if (!_shells[i]) continue;
                        _shells[i].GetPropertyBlock(_blocks[i]);
                        _blocks[i].SetColor(DullCrystalId, Color.Lerp(_startDull[i], _targetDull, c));
                        _blocks[i].SetColor(BrightCrystalId, Color.Lerp(_startBright[i], _targetBright, c));
                        _shells[i].SetPropertyBlock(_blocks[i]);
                    }
                }
            }

            // The hand-off happens where the two states are EQUIVALENT: the geometry has landed on
            // the hull, the normals have landed on its facets, and the colours have landed on the
            // ball's pair. The ball takes over the surface there and the crystal's shells DISSOLVE
            // off the top of it.
            if (!_handedOff && elapsed >= _morphSeconds)
            {
                Release();
                _handedOff = true;
                CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                    $"[CrystalMorph] Scarab: geometry landed at {elapsed:F2}s — the ball is now " +
                    "drawing itself; dissolving the crystal's shells off it.");
            }

            if (_handedOff)
            {
                float tail = Mathf.Max(1e-4f, _config.duration - _morphSeconds);
                float d = Mathf.Clamp01((elapsed - _morphSeconds) / tail);
                SetOpacity(1f - (d * d * (3f - 2f * d)));
                if (d >= 1f) Destroy(gameObject);
            }
        }

        void SetOpacity(float opacity)
        {
            for (int i = 0; i < _shells.Count; i++)
            {
                if (!_shells[i]) continue;
                _shells[i].GetPropertyBlock(_blocks[i]);
                _blocks[i].SetFloat(OpacityId, Mathf.Clamp01(opacity));
                _shells[i].SetPropertyBlock(_blocks[i]);
            }
        }

        /// <summary>Hands rendering back to the ball. Idempotent, and called from teardown too — an
        /// unreleased hold would leave the ball invisible for the rest of its life.</summary>
        void Release()
        {
            if (_ball != null) _ball.SetMorphStandIn(false);
        }

        void OnDestroy()
        {
            Release();
            if (_mesh) Destroy(_mesh);
        }
    }
}
