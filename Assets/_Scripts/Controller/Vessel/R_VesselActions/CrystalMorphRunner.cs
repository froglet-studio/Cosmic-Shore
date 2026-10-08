using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The half of a vessel's bespoke omni-crystal retirement that does not depend on what the
    /// crystal BECOMES: adopt the spent crystal's own renderers, stamp the morph once, carry the
    /// colour pair across, hand the surface to the real object, dissolve. A vessel's runner
    /// supplies only the target — the Scarab's ball (<see cref="ScarabCrystalMorph"/>), the
    /// Squirrel's eight shielded ring prisms (<see cref="SquirrelCrystalMorph"/>).
    ///
    /// ── What the omni crystal is made of, and which part moves ───────────────────────────────
    /// The omni crystal is ONE body plus overlays (Docs/PALETTE.md §2.10): slot 0 is the whole
    /// cage on <c>OmniCrystalFresnelShader</c> — the shader that carries the morph path — and
    /// slots 1-4 are Mass's Shepard-tone triangles falling onto it and their stationary rim, a
    /// different mesh on <c>OmniShepardFresnelShader</c>. Only the body can fold; the overlays
    /// FADE over <see cref="CrystalMorphConfigSO.overlayFadeFraction"/> of the geometry window, so
    /// they leave as the cage opens instead of vanishing on the pickup frame.
    ///
    /// ── Three things are carried across, not just position ───────────────────────────────────
    /// 1. <b>Geometry</b> — TEXCOORD2, <c>CrystalMorph</c>.
    /// 2. <b>Normals</b> — TEXCOORD3, <c>CrystalMorphNormal</c>, on the position's exact schedule.
    /// 3. <b>Shading</b> — the colour PAIR (<c>_DarkColor</c>/<c>_BrightColor</c>, the same two
    ///    names on the body and on both targets) converges on the target's here, and the colour
    ///    FORMULA converges in the shader: the body draws <c>lerp(Bright, Dark, (1+N·V)/2)</c>,
    ///    both targets draw BlockGraph's <c>lerp(Dark, Bright, (1−N·V)⁴)</c>, and each face blends
    ///    from one to the other on its own <c>CrystalMorphEase</c> weight. The same pair through two
    ///    formulas is still two surfaces, so the pair alone could not close the seam.
    ///
    /// ── The window is MORPH then DISSOLVE ────────────────────────────────────────────────────
    /// <see cref="CrystalMorphConfigSO.morphFraction"/> of the window is the geometry (the
    /// shader's window, so the last staggered face has landed before the boundary). At the
    /// boundary the two states are equivalent and the real object takes the surface
    /// (<see cref="HandOff"/>); the body then dissolves off the top of it, because the two are
    /// still drawn by different shaders and the object that WINS has to be the real one.
    ///
    /// ── Every exit is named ──────────────────────────────────────────────────────────────────
    /// Every way this can fail looks identical on screen — the crystal is gone and the target
    /// appears normally — so each refusal says which one it was, and the whole path traces under
    /// <see cref="CSLogChannel.CrystalMorph"/> (FrogletTools ▸ Toolbox ▸ Logging).
    ///
    /// Cost per pickup: one Mesh build and ONE stamp. The geometry runs entirely in the vertex
    /// stage off <c>_PrismClock</c>; the per-frame writes are a handful of property-block values.
    /// </summary>
    public abstract class CrystalMorphRunner : MonoBehaviour
    {
        static readonly int MorphId = Shader.PropertyToID("_CrystalMorph");
        static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        /// <summary>The colour pair, under the SAME names on the omni body
        /// (OmniCrystalFresnelShader) and on BlockGraph, which draws both targets.</summary>
        protected static readonly int DarkColorId = Shader.PropertyToID("_DarkColor");
        protected static readonly int BrightColorId = Shader.PropertyToID("_BrightColor");

        readonly List<Renderer> _body = new();
        readonly List<MaterialPropertyBlock> _bodyBlocks = new();
        readonly List<Color> _startDark = new();
        readonly List<Color> _startBright = new();
        readonly List<Renderer> _overlays = new();
        readonly List<MaterialPropertyBlock> _overlayBlocks = new();

        Mesh _mesh;
        float _startTime;
        float _morphSeconds;
        float _giveUpAt = float.PositiveInfinity;
        bool _stamped;
        bool _handedOff;
        bool _fading;
        Color _targetDark, _targetBright;
        bool _haveTargetColour;

        protected CrystalMorphConfigSO Config { get; private set; }

        /// <summary>Short name for the log lines ("Scarab", "Squirrel").</summary>
        protected abstract string Owner { get; }

        /// <summary>True once the morph has been stamped and is running.</summary>
        protected bool IsStamped => _stamped;

        /// <summary>Seconds of GEOMETRY in the window — the shader's window.</summary>
        protected float MorphSeconds => _morphSeconds;

        /// <summary>The cage the body draws — the source every morph mesh is built from.</summary>
        protected Mesh SourceMesh =>
            _body.Count > 0 && _body[0] && _body[0].TryGetComponent<MeshFilter>(out var f) ? f.sharedMesh : null;

        /// <summary>Renderer counts, for the trace line.</summary>
        protected string Census => $"{_body.Count} body, {_overlays.Count} overlay renderer(s)";

        protected int MorphVertexCount => _mesh ? _mesh.vertexCount : 0;

        protected bool HasTargetColour => _haveTargetColour;

        /// <summary>The colour pair the morph has to arrive wearing — the target's own, read off
        /// the thing that shipped. Called every frame until the hand-off, because a target may
        /// animate its pair (the ball runs a domain phase every frame).</summary>
        protected abstract bool TryReadTargetColours(out Color dark, out Color bright);

        /// <summary>The geometry has landed: the real object takes the surface NOW.</summary>
        protected abstract void HandOff();

        /// <summary>Teardown: give back anything held. Idempotent, and called on destroy too — an
        /// unreleased photon hold would leave the target invisible for the rest of its life.</summary>
        protected abstract void ReleaseHold();

        /// <summary>The morph gave up waiting for its target; it is fading out. Optional.</summary>
        protected virtual void OnGaveUp() { }

        /// <summary>
        /// Resolves the spent crystal on THIS peer by the id its collect stamped. Through the cell
        /// containing the collect pose — <c>CellRuntimeDataSO</c> is where crystals are indexed —
        /// and from the COLLECT pose rather than the crystal's current one, which on a remote peer
        /// is usually its next home already. Falls back to the live-crystal registry for a crystal
        /// no cell indexes (a manager-less local mint).
        /// </summary>
        protected static Crystal ResolveCrystal(in CrystalForgeOrigin origin)
        {
            var cell = Cell.FindCellContaining(origin.Position) ?? Cell.FindNearestActiveCell(origin.Position);
            var runtime = cell != null ? cell.RuntimeData : null;
            if (runtime != null && runtime.TryGetCrystalById(origin.CrystalId, out var indexed) && indexed)
                return indexed;

            var live = Crystal.Active;
            for (int i = 0; i < live.Count; i++)
            {
                var c = live[i];
                if (c && !c.IsEmbedded && c.Id == origin.CrystalId) return c;
            }
            return null;
        }

        /// <summary>
        /// Copies the crystal's model renderers onto this object — sharing the crystal's meshes,
        /// shared materials and property block, so frame 0 IS the crystal, tint and all. Nothing
        /// is cloned and nothing is re-authored: any reconstruction would be a second authority
        /// for the crystal's look, and the copy takes the property BLOCK because that is where a
        /// domain tint lands over the shared material.
        ///
        /// Models drawing slot 0's cage are BODY (they fold); any other mesh is an OVERLAY (it
        /// fades). Returns false when the crystal exposes no drawable body.
        /// </summary>
        protected bool Adopt(Crystal crystal)
        {
            Config = CrystalMorphConfigSO.Instance;

            var models = crystal ? crystal.CrystalModels : null;
            if (models == null) return false;

            Mesh cage = null;
            for (int i = 0; i < models.Count; i++)
            {
                var model = models[i]?.model;
                if (model == null) continue;
                if (!model.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null) continue;
                if (!model.TryGetComponent<MeshRenderer>(out var source)) continue;

                cage ??= filter.sharedMesh;
                bool isBody = filter.sharedMesh == cage;

                var copy = new GameObject(isBody ? $"Body{i}" : $"Overlay{i}");
                copy.transform.SetParent(transform, false);
                copy.transform.SetLocalPositionAndRotation(model.transform.localPosition,
                                                           model.transform.localRotation);
                copy.transform.localScale = model.transform.localScale;
                copy.layer = model.layer;

                copy.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var renderer = copy.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = source.sharedMaterials;
                renderer.shadowCastingMode = source.shadowCastingMode;
                renderer.receiveShadows = source.receiveShadows;

                var block = new MaterialPropertyBlock();
                source.GetPropertyBlock(block);
                renderer.SetPropertyBlock(block);

                if (!isBody)
                {
                    CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                        $"[CrystalMorph] {Owner}: '{crystal.name}' model {i} draws " +
                        $"'{filter.sharedMesh.name}', not the cage '{cage.name}' — an overlay; it " +
                        "fades rather than folds.");
                    _overlays.Add(renderer);
                    _overlayBlocks.Add(block);
                    continue;
                }

                // The colour this body STARTS at — the block's value where a tint wrote one, else
                // the shared material's — so the convergence lerps from what is actually drawn.
                var mat = source.sharedMaterial;
                _startDark.Add(ReadColour(block, mat, DarkColorId, Color.black));
                _startBright.Add(ReadColour(block, mat, BrightColorId, Color.white));
                _body.Add(renderer);
                _bodyBlocks.Add(block);
            }
            return _body.Count > 0;
        }

        static Color ReadColour(MaterialPropertyBlock block, Material mat, int id, Color fallback)
        {
            if (block != null && block.HasColor(id)) return block.GetColor(id);
            return mat != null && mat.HasProperty(id) ? mat.GetColor(id) : fallback;
        }

        /// <summary>Holds the crystal still, as it was, for up to the config's grace while the
        /// target is not known yet. Past it the crystal fades out, loudly.</summary>
        protected void BeginWaiting() =>
            _giveUpAt = Time.time + Mathf.Max(0.05f, Config.targetGraceSeconds);

        /// <summary>
        /// Puts <paramref name="morphMesh"/> on every body renderer and writes the ONE stamp that
        /// runs the geometry. This object owns the mesh from here and destroys it.
        /// </summary>
        protected void Stamp(Mesh morphMesh)
        {
            _mesh = morphMesh;
            for (int i = 0; i < _body.Count; i++)
                _body[i].GetComponent<MeshFilter>().sharedMesh = morphMesh;

            _haveTargetColour = TryReadTargetColours(out _targetDark, out _targetBright);
            if (!_haveTargetColour)
                CSDebug.LogWarning($"[{Owner}CrystalMorph] the target exposed no _DarkColor/_BrightColor " +
                                   "pair, so the morph lands in the CRYSTAL's colours and the hand-off " +
                                   "will show a colour change.");

            _startTime = PrismClock.Now;
            _morphSeconds = Config.duration * Mathf.Clamp01(Config.morphFraction);
            _stamped = true;
            _fading = false;

            // The shader's window is the GEOMETRY half only, so the LAST staggered face has landed
            // by the time the dissolve starts — a stagger is only free if it finishes first.
            var morph = new Vector3(_startTime, _morphSeconds, Mathf.Clamp01(Config.stagger));
            for (int i = 0; i < _body.Count; i++)
            {
                _body[i].GetPropertyBlock(_bodyBlocks[i]);
                _bodyBlocks[i].SetVector(MorphId, morph);
                _bodyBlocks[i].SetFloat(OpacityId, 1f);   // a late target cancels a fade already begun
                _body[i].SetPropertyBlock(_bodyBlocks[i]);
            }
            SetOverlayOpacity(1f);
        }

        protected virtual void LateUpdate()
        {
            if (!_stamped) { TickWaiting(); return; }

            float elapsed = PrismClock.Now - _startTime;
            float g = Mathf.Clamp01(elapsed / Mathf.Max(1e-4f, _morphSeconds));

            // Overlays leave early: they are not the cage and have nowhere to land.
            float o = Mathf.Clamp01(g / Mathf.Max(1e-4f, Config.overlayFadeFraction));
            SetOverlayOpacity(1f - o * o * (3f - 2f * o));

            // Colour pair, converged BEFORE the hand-off so the two surfaces already agree when
            // they overlap. Re-read every frame: a target may animate its own pair.
            if (!_handedOff)
            {
                if (TryReadTargetColours(out var dark, out var bright))
                {
                    _targetDark = dark;
                    _targetBright = bright;
                    _haveTargetColour = true;
                }

                if (_haveTargetColour)
                {
                    float c = Mathf.Clamp01(g / Mathf.Max(0.1f, Mathf.Clamp01(Config.colourBlendFraction)));
                    c = c * c * (3f - 2f * c);
                    for (int i = 0; i < _body.Count; i++)
                    {
                        if (!_body[i]) continue;
                        _body[i].GetPropertyBlock(_bodyBlocks[i]);
                        _bodyBlocks[i].SetColor(DarkColorId, Color.Lerp(_startDark[i], _targetDark, c));
                        _bodyBlocks[i].SetColor(BrightColorId, Color.Lerp(_startBright[i], _targetBright, c));
                        _body[i].SetPropertyBlock(_bodyBlocks[i]);
                    }
                }
            }

            // The hand-off happens where the two states are EQUIVALENT: geometry on the target's
            // faces, normals on its normals, pair and formula on its shading.
            if (!_handedOff && elapsed >= _morphSeconds)
            {
                _handedOff = true;
                HandOff();
                CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                    $"[CrystalMorph] {Owner}: geometry landed at {elapsed:F2}s — the target draws " +
                    "itself now; dissolving the crystal's body off it.");
            }

            if (_handedOff)
            {
                float tail = Mathf.Max(1e-4f, Config.duration - _morphSeconds);
                float d = Mathf.Clamp01((elapsed - _morphSeconds) / tail);
                SetBodyOpacity(1f - d * d * (3f - 2f * d));
                if (d >= 1f) Destroy(gameObject);
            }
        }

        /// <summary>
        /// Still waiting for the target. Holds the crystal EXACTLY as it was — a morph that starts
        /// guessing is worse than one that waits — and past the grace fades it out, loudly. A
        /// target that arrives during the fade still wins.
        /// </summary>
        void TickWaiting()
        {
            if (Time.time < _giveUpAt) return;

            if (!_fading)
            {
                _fading = true;
                OnGaveUp();
            }

            float fade = Mathf.InverseLerp(_giveUpAt, _giveUpAt + Config.duration, Time.time);
            SetBodyOpacity(1f - fade);
            SetOverlayOpacity(1f - fade);
            if (fade >= 1f) Destroy(gameObject);
        }

        void SetBodyOpacity(float opacity) => SetOpacity(_body, _bodyBlocks, opacity);

        void SetOverlayOpacity(float opacity) => SetOpacity(_overlays, _overlayBlocks, opacity);

        static void SetOpacity(List<Renderer> renderers, List<MaterialPropertyBlock> blocks, float opacity)
        {
            for (int i = 0; i < renderers.Count; i++)
            {
                if (!renderers[i]) continue;
                renderers[i].GetPropertyBlock(blocks[i]);
                blocks[i].SetFloat(OpacityId, Mathf.Clamp01(opacity));
                renderers[i].SetPropertyBlock(blocks[i]);
            }
        }

        protected virtual void OnDestroy()
        {
            ReleaseHold();
            if (_mesh) Destroy(_mesh);
        }
    }
}
