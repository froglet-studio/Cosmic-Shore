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
    /// into whichever octahedron their own solid was assigned to and are absorbed. Nothing is
    /// invented and nothing is spare.
    ///
    /// ── The three things that make it seamless ────────────────────────────────────────────
    /// 1. **It draws the crystal's own renderers.** Mesh, materials and MaterialPropertyBlock
    ///    are copied off the live crystal, so frame 0 of the morph is the crystal, including
    ///    the Shepard shells' band animation. A rebuilt look-alike would pop.
    /// 2. **It ends ON the real prisms.** The targets are read from the prisms the ring builder
    ///    actually laid — their own shield semi-axes, their own final pose — so the last frame
    ///    of the morph and the first frame of the ring are the same geometry. There is no
    ///    second authority to drift: retune `SpawnableRings` and the animation follows.
    /// 3. **The prisms are laid at once and only their PHOTONS wait.** Colliders, mass, shield
    ///    state and spatial index all go final the instant the ring is laid — the ring is
    ///    skimmable from frame 0 while the morph is still in flight — and
    ///    <see cref="Prism.SetVisualStandIn"/> holds nothing but their rendering. That is the
    ///    clock-material law's own division (Docs/PRISM_ANIMATION.md §4) applied to a hand-off.
    ///
    /// Cost: one Mesh build per collect (~4.3k vertices; the face partition itself is cached
    /// per source mesh), and ONE stamp. The animation runs entirely in the vertex stage off
    /// `_PrismClock` — no per-frame, per-vertex or per-prism CPU work. The only per-frame write
    /// is the tail cross-dissolve's `_Opacity`, one uniform per shell renderer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SquirrelCrystalMorph : MonoBehaviour
    {
        /// <summary>Authored feel, handed over by the retirement SO.</summary>
        public struct Settings
        {
            public float Duration;
            public float Stagger;
            /// <summary>Fraction of the duration at which the real ring is revealed and the
            /// morph starts dissolving out over it.</summary>
            public float HandoffFraction;
            public float FillerPhase;
            public float PanelPhaseStart;
            public float PanelPhaseEnd;
            /// <summary>How long to wait for the ring before giving up and fading out.</summary>
            public float RingGraceSeconds;
        }

        static readonly int MorphId = Shader.PropertyToID("_CrystalMorph");
        static readonly int OpacityId = Shader.PropertyToID("_Opacity");

        Settings _settings;
        Domains _domain;
        readonly List<Renderer> _shells = new();
        readonly List<MaterialPropertyBlock> _blocks = new();
        readonly List<Prism> _held = new();
        Mesh _mesh;
        float _startTime;
        float _giveUpAt;
        bool _stamped;
        bool _handedOff;
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
            if (crystal == null) return null;

            var go = new GameObject($"CrystalMorph_{crystal.name}");
            go.transform.SetPositionAndRotation(collectedAt, crystal.transform.rotation);
            go.transform.localScale = crystal.transform.lossyScale;

            var morph = go.AddComponent<SquirrelCrystalMorph>();
            if (!morph.AdoptShells(crystal))
            {
                Destroy(go);
                return null;
            }

            morph._settings = settings;
            morph._domain = domain;
            morph._giveUpAt = Time.time + Mathf.Max(0.05f, settings.RingGraceSeconds);
            BoostRingBuilder.RingLaid += morph.OnRingLaid;
            morph._subscribed = true;
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

                _shells.Add(renderer);
                _blocks.Add(block);
            }
            return _shells.Count > 0;
        }

        void OnRingLaid(BoostRingLay lay)
        {
            if (_stamped || lay.Prisms == null || lay.Prisms.Count == 0) return;
            if (lay.Domain != _domain) return;
            if (lay.Spec.Kind != PrismKind.Shielded) return;   // the morph ends on SHIELD octahedra

            Unsubscribe();

            // Resolved in WORLD space off each prism, then brought into this object's frame —
            // the mesh's targets have to be in the same space as its vertices.
            var targets = new List<CrystalMorphMeshBuilder.OctahedronTarget>(lay.Prisms.Count);
            for (int i = 0; i < lay.Prisms.Count; i++)
                if (TryResolveShield(lay.Prisms[i], out var world)) targets.Add(ToLocal(world));

            if (targets.Count != lay.Prisms.Count)
            {
                CSDebug.LogWarning($"[SquirrelCrystalMorph] {targets.Count}/{lay.Prisms.Count} ring " +
                                   "prisms exposed a shield octahedron — falling back to a fade.");
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
            var morph = new Vector3(_startTime, _settings.Duration, _settings.Stagger);
            for (int i = 0; i < _shells.Count; i++)
            {
                _shells[i].GetPropertyBlock(_blocks[i]);
                _blocks[i].SetVector(MorphId, morph);
                _shells[i].SetPropertyBlock(_blocks[i]);
            }
        }

        /// <summary>
        /// The eight faces of one laid prism's shield, in this morph's local space.
        ///
        /// Built from the shield's OWN semi-axes (<see cref="PrismOctahedronShield"/> derives
        /// them from the authored BoxCollider and the circumscribing scale) and the prism's
        /// FINAL scale rather than its live one — under the clock law a prism's transform is
        /// final at its stamp, but reading <c>TargetScale</c> makes that independent of when
        /// this runs. The face set is the eight octants (±x, ±y, ±z), which is exactly the face
        /// set <see cref="OctahedronMeshGenerator"/> builds; only the winding differs, and a
        /// target is a set of three corner POSITIONS, so winding does not reach it.
        /// </summary>
        static bool TryResolveShield(Prism prism, out CrystalMorphMeshBuilder.OctahedronTarget target)
        {
            target = default;
            if (!prism || !prism.TryGetComponent<PrismOctahedronShield>(out var shield)) return false;
            if (prism.prismProperties == null || !prism.prismProperties.IsShielded) return false;

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

        /// <summary>
        /// Converts a target built in WORLD space into this morph's local space. Called once per
        /// target, right after <see cref="TryResolveShield"/> — kept separate so the shield
        /// resolution stays a pure statement about the prism.
        /// </summary>
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
            if (!_stamped)
            {
                // The ring never arrived (a mode that wires no ring, or a spawner that declined).
                // Fade the crystal's body out rather than leaving it hanging or popping it —
                // continuity of existence holds whether or not the morph found its target.
                if (Time.time < _giveUpAt) return;
                float fade = Mathf.InverseLerp(_giveUpAt, _giveUpAt + _settings.Duration, Time.time);
                SetOpacity(1f - fade);
                if (fade >= 1f) Destroy(gameObject);
                return;
            }

            float t = Mathf.Clamp01((PrismClock.Now - _startTime) / Mathf.Max(1e-4f, _settings.Duration));
            float handoff = Mathf.Clamp01(_settings.HandoffFraction);

            if (!_handedOff && t >= handoff)
            {
                // The morph is on the octahedra by now, so the ring appears UNDER a body that is
                // already its own shape — the cross-dissolve below has nothing to reveal but the
                // change of material.
                Release();
                _handedOff = true;
            }

            if (t >= handoff)
                SetOpacity(1f - Mathf.InverseLerp(handoff, 1f, t));

            if (t >= 1f) Destroy(gameObject);
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
