using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Carries a collected omni crystal's BODY onto the eight shielded prisms of the boost ring
    /// the Squirrel's hit lays — the vessel's bespoke replacement for the shared husk spray.
    /// Full record: `_Scripts/Controller/Vessel/R_VesselActions/SQUIRREL_CRYSTAL_MORPH.md`.
    ///
    /// The crystal's cage has 40 triangular and 24 pentagonal faces — 64 — and eight octahedron
    /// shields show 8 × 8 = 64. So every panel of the crystal becomes exactly one face of a
    /// prism, 1:1, and the 660 leftover quads (the cage's struts and the panels' rims) collapse
    /// into whichever octahedron their own solid was assigned to and are absorbed.
    ///
    /// ── The transition is between EQUIVALENT STATES ───────────────────────────────────────
    /// The real prisms are revealed only at t = 1, when the morph's geometry IS their octahedra
    /// — same corners, same orientation — and its colour has been carried onto their shielded
    /// palette. Until that instant the prisms are held invisible; after it the morph is gone.
    /// There is no cross-fade between two different-looking things, because there is no moment
    /// at which they look different. (`HandoffFraction` can reveal them earlier for debugging;
    /// at anything below 1 the swap happens while the panels are still arriving, which is
    /// exactly the seam this design exists to remove.)
    ///
    /// ── What makes each half seamless ─────────────────────────────────────────────────────
    /// 1. **It draws the crystal's own renderers.** Mesh, materials and MaterialPropertyBlock
    ///    are copied off the live crystal, so frame 0 of the morph is the crystal, including
    ///    the Shepard shells' band animation. A rebuilt look-alike would pop.
    /// 2. **It ends ON the real prisms.** The targets are read from the prisms the ring builder
    ///    actually laid — their own shield semi-axes, their own final pose — and the colour it
    ///    converges to is read off the very material those prisms bound. There is no second
    ///    authority to drift: retune `SpawnableRings` and the animation follows.
    /// 3. **The prisms are laid at once and only their PHOTONS wait.** Colliders, mass, shield
    ///    state and spatial index all go final the instant the ring is laid — the ring is
    ///    skimmable from frame 0 while the morph is still in flight — and
    ///    <see cref="Prism.SetVisualStandIn"/> holds nothing but their rendering. That is the
    ///    clock-material law's own division (Docs/PRISM_ANIMATION.md §4) applied to a hand-off.
    ///
    /// ── It reports on itself, because its one dependency is invisible ─────────────────────
    /// The ring is laid by a SIBLING effect, and every way that can fail — the retirement never
    /// running, the ring never arriving, the ring arriving and being rejected — looks identical
    /// on screen: the prisms appear normally and the crystal fades out. So every rejection is a
    /// WARNING naming exactly what mismatched, and the whole path traces under
    /// <see cref="CSLogChannel.CrystalMorph"/> (FrogletTools ▸ Toolbox ▸ Logging). A silent
    /// fallback here is worse than no fallback.
    ///
    /// Cost: one Mesh build per collect (~4.3k vertices; the face partition is cached per source
    /// mesh) and ONE stamp. The geometry runs entirely in the vertex stage off `_PrismClock`.
    /// The per-frame writes are uniforms only — the colour convergence and the tail opacity,
    /// a handful of MaterialPropertyBlock values per shell.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SquirrelCrystalMorph : MonoBehaviour
    {
        /// <summary>Authored feel, handed over by the retirement SO.</summary>
        public struct Settings
        {
            public float Duration;
            public float Stagger;
            /// <summary>Fraction of the window at which the real ring is revealed. 1 = only at
            /// equivalence, which is the design; below 1 is a debugging aid.</summary>
            public float HandoffFraction;
            public float FillerPhase;
            public float PanelPhaseStart;
            public float PanelPhaseEnd;
            /// <summary>How long to wait for the ring before giving up and fading out.</summary>
            public float RingGraceSeconds;
            /// <summary>How much of the window is spent carrying the crystal's colour onto the
            /// shielded prism's. 1 = the whole flight.</summary>
            public float ColourBlendFraction;
        }

        static readonly int MorphId = Shader.PropertyToID("_CrystalMorph");
        static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        // ShepardGraph's pair — the crystal's base body and its fresnel rim.
        static readonly int DullCrystalId = Shader.PropertyToID("_DullCrystalColor");
        static readonly int BrightCrystalId = Shader.PropertyToID("_BrightCrystalColor");
        // BlockGraph's pair on the shielded prism — the same two roles (Docs/PALETTE.md).
        static readonly int DarkId = Shader.PropertyToID("_DarkColor");
        static readonly int BrightId = Shader.PropertyToID("_BrightColor");

        Settings _settings;
        Domains _domain;
        readonly List<Renderer> _shells = new();
        readonly List<MaterialPropertyBlock> _blocks = new();
        readonly List<Color> _startDull = new();
        readonly List<Color> _startBright = new();
        readonly List<Prism> _held = new();
        Color _targetDull, _targetBright;
        bool _haveTargetColour;
        Mesh _mesh;
        float _startTime;
        float _giveUpAt;
        int _ringsSeen;
        bool _stamped;
        bool _handedOff;
        bool _fading;
        bool _subscribed;

        /// <summary>
        /// Stands a morph up where the crystal WAS COLLECTED, wearing its shells, and waits for
        /// the ring. Returns null when the crystal has nothing to copy — the caller then leaves
        /// the shared husk spray in place rather than retiring the crystal invisibly.
        ///
        /// <paramref name="collectedAt"/> rather than the crystal's live position: collection
        /// and respawn are independent RPC chains, so on a remote peer the crystal may already
        /// have moved on. Rotation and scale still come from the crystal — a respawn moves it,
        /// it does not resize it.
        /// </summary>
        public static SquirrelCrystalMorph Begin(Crystal crystal, Vector3 collectedAt, Domains domain,
                                                 in Settings settings)
        {
            if (crystal == null)
            {
                CSDebug.LogWarning("[SquirrelCrystalMorph] no crystal to morph — the retirement " +
                                   "could not resolve the collected crystal by id, so this pickup " +
                                   "retires with no animation at all.");
                return null;
            }

            var go = new GameObject($"CrystalMorph_{crystal.name}");
            go.transform.SetPositionAndRotation(collectedAt, crystal.transform.rotation);
            go.transform.localScale = crystal.transform.lossyScale;

            var morph = go.AddComponent<SquirrelCrystalMorph>();
            if (!morph.AdoptShells(crystal))
            {
                CSDebug.LogWarning($"[SquirrelCrystalMorph] '{crystal.name}' exposed no drawable " +
                                   "shell (a model with a MeshFilter AND a MeshRenderer) — nothing " +
                                   "to morph, so this pickup retires with no animation.");
                Destroy(go);
                return null;
            }

            morph._settings = settings;
            morph._domain = domain;
            morph._giveUpAt = Time.time + Mathf.Max(0.05f, settings.RingGraceSeconds);
            BoostRingBuilder.RingLaid += morph.OnRingLaid;
            morph._subscribed = true;

            CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                $"[CrystalMorph] began on '{crystal.name}' at {collectedAt}, domain {domain}, " +
                $"{morph._shells.Count} shells, waiting up to {settings.RingGraceSeconds:F2}s for a " +
                $"{PrismKind.Shielded} ring.");
            return morph;
        }

        /// <summary>
        /// Copies the crystal's model renderers onto this object — one child per shell, sharing
        /// the crystal's meshes, its shared materials and its property block.
        ///
        /// Nothing is cloned and nothing is re-authored: an omni crystal draws four coincident
        /// copies of one cage, each showing a different band of a travelling wave, and any
        /// reconstruction of that would be a second authority for the crystal's look. This is
        /// also why the copy takes the property BLOCK — `Crystal.ApplyColorSetTint` paints the
        /// collectability colour there, over the shared material, so a copy that skipped it
        /// would start on a visibly different crystal.
        /// </summary>
        bool AdoptShells(Crystal crystal)
        {
            var models = crystal.CrystalModels;
            if (models == null) return false;

            for (int i = 0; i < models.Count; i++)
            {
                var model = models[i]?.model;
                if (model == null) continue;
                if (!model.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null) continue;
                if (!model.TryGetComponent<MeshRenderer>(out var source)) continue;

                // ONE morph mesh drives every shell, so every shell has to be the same cage —
                // which an omni crystal's four are by construction (four coincident copies of one
                // model, differing only in which band of the travelling wave each renders). A
                // crystal built any other way would be silently drawn as shell 0's geometry, so
                // it is dropped and named instead.
                if (_shells.Count > 0 && filter.sharedMesh != _shells[0].GetComponent<MeshFilter>().sharedMesh)
                {
                    CSDebug.LogWarning($"[SquirrelCrystalMorph] '{crystal.name}' shell {i} draws " +
                                       $"'{filter.sharedMesh.name}', not the first shell's " +
                                       $"'{_shells[0].GetComponent<MeshFilter>().sharedMesh.name}' — " +
                                       "the morph carries one mesh, so this shell is left out.");
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

                // The colour this shell STARTS at, so the convergence below is a lerp from what
                // it is actually drawing rather than from the shader's default.
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

        void OnRingLaid(BoostRingLay lay)
        {
            if (_stamped) return;
            _ringsSeen++;

            if (lay.Prisms == null || lay.Prisms.Count == 0)
            {
                CSDebug.LogVerbose(CSLogChannel.CrystalMorph, "[CrystalMorph] saw an empty ring.");
                return;
            }
            if (lay.Domain != _domain)
            {
                CSDebug.LogWarning($"[SquirrelCrystalMorph] ignoring a ring of domain {lay.Domain} " +
                                   $"— this morph belongs to {_domain}. If that is the ring this " +
                                   "pickup laid, the vessel's domain and the AOE's disagree.");
                return;
            }
            if (lay.Spec.Kind != PrismKind.Shielded)
            {
                CSDebug.LogWarning($"[SquirrelCrystalMorph] ignoring a {lay.Spec.Kind} ring — the " +
                                   "morph ends on SHIELD octahedra, so it can only take a " +
                                   $"{PrismKind.Shielded} one. Check isShielded on the ring spawner.");
                return;
            }

            // Resolved in WORLD space off each prism, then brought into this object's frame —
            // the mesh's targets have to be in the same space as its vertices.
            var targets = new List<CrystalMorphMeshBuilder.OctahedronTarget>(lay.Prisms.Count);
            string firstRefusal = null;
            for (int i = 0; i < lay.Prisms.Count; i++)
            {
                if (TryResolveShield(lay.Prisms[i], out var world, out var refusal)) targets.Add(ToLocal(world));
                else firstRefusal ??= $"prism {i}: {refusal}";
            }

            if (targets.Count != lay.Prisms.Count)
            {
                CSDebug.LogWarning($"[SquirrelCrystalMorph] {targets.Count}/{lay.Prisms.Count} ring " +
                                   $"prisms exposed a shield octahedron ({firstRefusal}) — the morph " +
                                   "cannot land on a ring it cannot measure, so it will fade instead.");
                return;
            }

            // The source mesh is shell 0's — every shell of an omni crystal is the same cage, so
            // one baked target set drives all four and the morph is ONE mesh on N renderers.
            var source = _shells[0].GetComponent<MeshFilter>().sharedMesh;
            _mesh = CrystalMorphMeshBuilder.TryBuild(source, targets, _settings.FillerPhase,
                                                     _settings.PanelPhaseStart, _settings.PanelPhaseEnd,
                                                     out string diagnosis);
            if (_mesh == null)
            {
                CSDebug.LogError($"[SquirrelCrystalMorph] cannot morph this crystal: {diagnosis}");
                return;
            }

            Unsubscribe();
            CaptureTargetColour(lay.Prisms);

            for (int i = 0; i < _shells.Count; i++)
                _shells[i].GetComponent<MeshFilter>().sharedMesh = _mesh;

            // Photons only: the ring is live mass from the frame it was laid.
            for (int i = 0; i < lay.Prisms.Count; i++)
            {
                var prism = lay.Prisms[i];
                if (!prism) continue;
                prism.SetVisualStandIn(true);
                _held.Add(prism);
            }

            _startTime = PrismClock.Now;
            _stamped = true;
            _fading = false;
            var morph = new Vector3(_startTime, _settings.Duration, _settings.Stagger);
            for (int i = 0; i < _shells.Count; i++)
            {
                _shells[i].GetPropertyBlock(_blocks[i]);
                _blocks[i].SetVector(MorphId, morph);
                _blocks[i].SetFloat(OpacityId, 1f);   // a late ring cancels a fade already begun
                _shells[i].SetPropertyBlock(_blocks[i]);
            }

            CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                $"[CrystalMorph] took ring #{_ringsSeen}: {lay.Prisms.Count} prisms held, " +
                $"{_mesh.vertexCount} morph vertices, stamped at {_startTime:F2} for " +
                $"{_settings.Duration:F2}s (colour target {(_haveTargetColour ? "read" : "NOT FOUND")}).");
        }

        /// <summary>
        /// The palette the morph has to arrive wearing, read off the material the laid prisms
        /// actually bound rather than from a ThemeManager lookup — the same "read the thing that
        /// shipped, never re-derive it" rule the geometry targets follow. `_DarkColor` and
        /// `_BrightColor` are the prism's base face and its fresnel rim (Docs/PALETTE.md), which
        /// are the same two roles ShepardGraph calls Dull and Bright.
        /// </summary>
        void CaptureTargetColour(IReadOnlyList<Prism> prisms)
        {
            for (int i = 0; i < prisms.Count; i++)
            {
                if (!prisms[i] || !prisms[i].TryGetComponent<MeshRenderer>(out var r)) continue;
                var mat = r.sharedMaterial;
                if (mat == null || !mat.HasProperty(DarkId) || !mat.HasProperty(BrightId)) continue;
                _targetDull = mat.GetColor(DarkId);
                _targetBright = mat.GetColor(BrightId);
                _haveTargetColour = true;
                return;
            }
            CSDebug.LogWarning("[SquirrelCrystalMorph] no ring prism exposed _DarkColor/_BrightColor " +
                               "— the morph will land in the CRYSTAL's colours, so the hand-off to " +
                               "the shielded prisms will show a colour change.");
        }

        /// <summary>
        /// The eight faces of one laid prism's shield, in world space.
        ///
        /// Built from the shield's OWN semi-axes (<see cref="PrismOctahedronShield"/> derives
        /// them from the authored BoxCollider and the circumscribing scale) and the prism's
        /// FINAL scale rather than its live one — under the clock law a prism's transform is
        /// final at its stamp, but reading <c>TargetScale</c> makes that independent of when
        /// this runs. The face set is the eight octants (±x, ±y, ±z), which is exactly the face
        /// set <see cref="OctahedronMeshGenerator"/> builds; only the winding differs, and a
        /// target is a set of three corner POSITIONS, so winding does not reach it.
        /// </summary>
        static bool TryResolveShield(Prism prism, out CrystalMorphMeshBuilder.OctahedronTarget target,
                                     out string refusal)
        {
            target = default;
            refusal = null;
            if (!prism) { refusal = "the prism is gone"; return false; }
            if (!prism.TryGetComponent<PrismOctahedronShield>(out var shield))
            {
                refusal = "no PrismOctahedronShield component";
                return false;
            }
            if (prism.prismProperties == null) { refusal = "no prismProperties"; return false; }
            if (!prism.prismProperties.IsShielded)
            {
                refusal = "IsShielded is false — the shield had not engaged when the ring was announced";
                return false;
            }

            Vector3 semi = shield.ShellSemiAxesLocal;
            Vector3 centre = shield.ShellCenterLocal;
            Vector3 scale = prism.TargetScale;
            if (scale == Vector3.zero) scale = prism.transform.localScale;

            var toWorld = Matrix4x4.TRS(prism.transform.position, prism.transform.rotation, scale);

            Vector3 px = toWorld.MultiplyPoint3x4(centre + new Vector3(semi.x, 0f, 0f));
            Vector3 nx = toWorld.MultiplyPoint3x4(centre - new Vector3(semi.x, 0f, 0f));
            Vector3 py = toWorld.MultiplyPoint3x4(centre + new Vector3(0f, semi.y, 0f));
            Vector3 ny = toWorld.MultiplyPoint3x4(centre - new Vector3(0f, semi.y, 0f));
            Vector3 pz = toWorld.MultiplyPoint3x4(centre + new Vector3(0f, 0f, semi.z));
            Vector3 nz = toWorld.MultiplyPoint3x4(centre - new Vector3(0f, 0f, semi.z));

            var corners = new Vector3[24];
            int w = 0;
            for (int sx = 0; sx < 2; sx++)
                for (int sy = 0; sy < 2; sy++)
                    for (int sz = 0; sz < 2; sz++)
                    {
                        corners[w++] = sx == 0 ? px : nx;
                        corners[w++] = sy == 0 ? py : ny;
                        corners[w++] = sz == 0 ? pz : nz;
                    }

            target = new CrystalMorphMeshBuilder.OctahedronTarget(
                toWorld.MultiplyPoint3x4(centre), corners);
            return true;
        }

        /// <summary>Brings a world-space target into this morph's local space — the mesh's
        /// targets have to live in the same frame as its vertices.</summary>
        CrystalMorphMeshBuilder.OctahedronTarget ToLocal(in CrystalMorphMeshBuilder.OctahedronTarget world)
        {
            var corners = new Vector3[world.FaceCorners.Length];
            for (int i = 0; i < corners.Length; i++)
                corners[i] = transform.InverseTransformPoint(world.FaceCorners[i]);
            return new CrystalMorphMeshBuilder.OctahedronTarget(
                transform.InverseTransformPoint(world.Centre), corners);
        }

        void LateUpdate()
        {
            if (!_stamped) { TickWaiting(); return; }

            float t = Mathf.Clamp01((PrismClock.Now - _startTime) / Mathf.Max(1e-4f, _settings.Duration));

            // Colour convergence: by the time the geometry IS the octahedra, the morph is already
            // wearing the palette they are about to be handed over in — so the swap changes
            // nothing the eye can catch.
            if (_haveTargetColour)
            {
                float c = Mathf.Clamp01(t / Mathf.Max(1e-4f, _settings.ColourBlendFraction));
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

            float handoff = Mathf.Clamp01(_settings.HandoffFraction);
            if (!_handedOff && t >= handoff)
            {
                Release();
                _handedOff = true;
                CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                    $"[CrystalMorph] handed off at t={t:F2} — the ring is now drawing itself.");
            }

            if (t >= 1f)
            {
                Release();          // idempotent; guarantees the ring is visible before we vanish
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Still waiting for the ring. Holds the crystal's body EXACTLY as it was — no shrink, no
        /// drift — because until the ring exists there is nothing to move toward, and a morph
        /// that starts guessing is worse than one that waits. Past the grace it fades out, loudly.
        /// A ring that arrives during the fade still wins.
        /// </summary>
        void TickWaiting()
        {
            if (Time.time < _giveUpAt) return;

            if (!_fading)
            {
                _fading = true;
                CSDebug.LogWarning(
                    $"[SquirrelCrystalMorph] no usable {PrismKind.Shielded} boost ring arrived within " +
                    $"{_settings.RingGraceSeconds:F2}s ({_ringsSeen} ring(s) seen), so the crystal is " +
                    "fading out instead of morphing. The ring is laid by a SIBLING effect " +
                    "(VesselExplosionByCrystalEffectSO → AOEShieldedRingSpawner) — check it is still " +
                    "in this vessel's VesselCrystalEffects. Trace with FrogletTools ▸ Toolbox ▸ " +
                    "Logging ▸ CrystalMorph.");
            }

            float fade = Mathf.InverseLerp(_giveUpAt, _giveUpAt + _settings.Duration, Time.time);
            SetOpacity(1f - fade);
            if (fade >= 1f) Destroy(gameObject);
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

        /// <summary>Hands rendering back to the prisms. Idempotent, and called from teardown too
        /// — an unreleased hold would leave the ring invisible for the rest of its life.</summary>
        void Release()
        {
            for (int i = 0; i < _held.Count; i++)
                if (_held[i]) _held[i].SetVisualStandIn(false);
            _held.Clear();
        }

        void Unsubscribe()
        {
            if (!_subscribed) return;
            BoostRingBuilder.RingLaid -= OnRingLaid;
            _subscribed = false;
        }

        void OnDestroy()
        {
            Unsubscribe();
            Release();
            if (_mesh) Destroy(_mesh);
        }
    }
}
