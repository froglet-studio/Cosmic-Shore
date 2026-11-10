using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Puppeteers the Brittle Star's hull (<see cref="BrittleStarHullSource"/>: the model <c>BrittleStar.fbx</c> through
    /// <see cref="BrittleStarHullModel"/> since 2026-10-18, or the procedural <see cref="BrittleStarHullBuilder"/>). Design record:
    /// <c>R_VesselActions/BRITTLE_STAR.md</c> §2.2. Everything it draws is the class document's:
    /// <list type="bullet">
    /// <item><b>Thrusters.</b> "When speed mode is off, only [the] thruster on the cockpit should be engaged. The outer
    /// thrusters should only engage as the vessel banks. When speed mode is turned on, all of the thrusters should engage
    /// at once." The pod flames on the OUTSIDE of a turn light with the stick; Speed Mode lights all five; overheated guns flare every flame.</item>
    /// <item><b>The firing rotation.</b> The support ring turns about the nose axis to bring the aimed turret round
    /// (<see cref="BrittleStarExecutor.RingPhase"/>), and Creation Mode's five corner trails turn with it.</item>
    /// <item><b>The ring's life.</b> Thrown, it is gone from the hull; rebuilt, it blooms back out from the cockpit
    /// (<see cref="BrittleStarExecutor.RingPresence01"/>) — continuity of existence, never a pop.</item>
    /// </list>
    ///
    /// <para><b>Channel discipline.</b> This component decides TRANSFORMS only: the ring's turn and presence and the
    /// flames' lengths (written through the hull, which knows its own parts' rest frames), and a gentle bank of the drawn
    /// hull. The element morph is the model's blend shapes (the base class) or the builder's vertices, and the hull keeps
    /// the muzzles on the barrels. No channel has two writers.</para>
    /// </summary>
    public class BrittleStarAnimation : VesselAnimation
    {
        [Header("Wiring")]
        [Tooltip("This vessel's executor — the ring's rotation, its presence and the modes. Serialized so a missing wire " +
                 "shows in the inspector; found in the vessel's children if left empty.")]
        [SerializeField] BrittleStarExecutor executor;

        [Header("Thrusters (flame length in world units)")]
        [Tooltip("The cockpit flame at cruise.")]
        [SerializeField, Min(0f)] float cockpitFlame = 1.4f;
        [Tooltip("Every flame's length with its thruster fully lit (Speed Mode, or a pod at full bank).")]
        [SerializeField, Min(0f)] float litFlame = 3.2f;
        [Tooltip("A pod flame at rest — a pilot-light ember, so an unlit engine still reads as an engine.")]
        [SerializeField, Min(0f)] float emberFlame = 0.12f;
        [Tooltip("How much stick lights the outside pods fully (0..1 of a full deflection).")]
        [SerializeField, Range(0.05f, 1f)] float bankToLight = 0.6f;
        [Tooltip("Extra flame length while the guns are overheated, as a multiple (the hull venting heat).")]
        [FormerlySerializedAs("overdriveFlame")]
        [SerializeField, Min(1f)] float overheatFlame = 1.35f;
        [Tooltip("Flicker: fraction of a flame's length it wavers by, and how fast.")]
        [SerializeField, Range(0f, 0.5f)] float flicker = 0.12f;
        [SerializeField, Min(0f)] float flickerRate = 22f;
        [Tooltip("How fast a flame eases to its target length (1/s, exponential).")]
        [SerializeField, Min(0.1f)] float flameRate = 12f;

        [Header("Hull")]
        [Tooltip("Degrees the drawn hull leans into a full-stick turn (fleet-scale puppetry: vessel rule 24).")]
        [SerializeField] float bankDegrees = 28f;
        [Tooltip("Degrees per second the ring idles round when nothing aims it (Creation Mode only — the corner trails twist).")]
        [SerializeField] float creationIdleSpin = 0f;

        BrittleStarHullSource _hull;
        Transform _hullTransform;
        Quaternion _hullRestRot;
        readonly float[] _flameLength = new float[BrittleStarHullForm.Points + 1];
        float _yaw;
        float _clock;

        // ---- the ring's motion smear (BrittleStarRingSmearPass) ----
        float _drawnPhase, _lastDrawnPhase, _drawnPresence;
        bool _hasDrawnPhase, _drewThisFrame;
        readonly BrittleStarRingSmearPass.Request _smear = new();
        readonly List<MaterialPropertyBlock> _smearBlocks = new();
        Mesh _smearRing;
        bool _smearing;
        float _smearRetryAt;
        bool _smearWarned;

        // ---- procedural element morphs (the StoatAnimation plumbing, verbatim in shape) ----
        readonly float[] _morphWeights = new float[4];      // BrittleStarHullForm.MorphElements order
        readonly Tween[] _morphTweens = new Tween[4];
        VesselElementalMorphConfigSO _procMorphConfig;
        bool _morphDirty;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            _hull = GetComponentInChildren<BrittleStarHullSource>(true);
            if (!executor) executor = GetComponentInChildren<BrittleStarExecutor>(true);
            if (!_hull)
                CSDebug.LogWarning($"[BrittleStarAnimation] '{name}' has no BrittleStarHullSource in its children; nothing to animate.");
            // The model (BrittleStar.fbx under the hull) is found, not wired: its sub-objects' fileIDs exist only after
            // Unity imports it. The base drives its element shapes by name; the flares need the renderer; the ring's
            // spin and the Space barrels reach past the bind-pose bounds, so it must not be culled on them.
            if (!SkinnedMeshRenderer) SkinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (SkinnedMeshRenderer) SkinnedMeshRenderer.updateWhenOffscreen = true;
            base.Initialize(vesselStatus);
            CaptureRest();
            InitializeProceduralMorphs(vesselStatus);
        }

        void CaptureRest()
        {
            if (!_hull) return;
            _hullTransform = _hull.transform;
            _hullRestRot = _hullTransform.localRotation;
            for (int i = 0; i < _flameLength.Length; i++) _flameLength[i] = 0f;
        }

        // The parts are the hull's, found through it (the model resolves its own bones by name), so nothing to resolve.
        protected override void AssignTransforms() { }

        protected override void PerformShipPuppetry(float pitch, float yaw, float roll, float throttle) => Drive(yaw);

        /// <summary>The base calls Idle() INSTEAD of puppetry when the stick centres; the flames and the ring still live.</summary>
        protected override void Idle() => Drive(0f);

        void Drive(float yaw)
        {
            if (!_hullTransform) return;
            float dt = Time.deltaTime;
            _clock += dt;
            _yaw = Mathf.Lerp(_yaw, Mathf.Clamp(yaw, -1f, 1f), 1f - Mathf.Exp(-10f * dt));

            // The drawn hull leans into the turn; the vessel's own transform and its path never move with it.
            _hullTransform.localRotation = _hullRestRot * Quaternion.Euler(0f, 0f, -_yaw * bankDegrees);

            float speed01 = executor ? executor.Speed01 : 0f;
            bool overheated = executor && executor.Overheated;
            if (_hull.Ring)
            {
                float phase = executor ? executor.RingPhase : 0f;
                if (executor && executor.Creation01 > 0f && !executor.Aiming && creationIdleSpin != 0f)
                    phase += _clock * creationIdleSpin * Mathf.Deg2Rad;
                // Thrown: gone. Rebuilt: blooms back out with a little overshoot.
                float presence = executor ? executor.RingPresence01 : 1f;
                float bloom = presence >= 1f ? 1f : DOVirtual.EasedValue(0f, 1f, presence, Ease.OutBack);
                _hull.SetRingPose(phase, Mathf.Max(0.001f, bloom));
                _drawnPhase = phase;
                _drawnPresence = presence;
                _drewThisFrame = true;
            }

            // The cockpit's own thruster: always lit, longer with Speed Mode.
            SetFlame(-1, Mathf.Lerp(cockpitFlame, litFlame, speed01), overheated, dt);

            // The pods: an ember at rest, lit on the outside of a turn, all lit in Speed Mode.
            float ringPhase = executor ? executor.RingPhase : 0f;
            for (int i = 0; i < BrittleStarHullForm.Points; i++)
            {
                // The pod's side of the hull right now (+1 right … -1 left); a right turn lights the left pods.
                float side = Mathf.Cos(BrittleStarHullForm.PointAngle(i) + ringPhase);
                float bank = Mathf.Clamp01(-side * _yaw / bankToLight);
                float lit = Mathf.Max(speed01, bank);
                SetFlame(i, Mathf.Lerp(emberFlame, litFlame, lit), overheated, dt);
            }
        }

        void SetFlame(int index, float target, bool overheated, float dt)
        {
            if (overheated) target *= overheatFlame;
            int slot = index + 1;
            _flameLength[slot] = Mathf.Lerp(_flameLength[slot], target, 1f - Mathf.Exp(-flameRate * dt));
            float wobble = 1f + flicker * Mathf.Sin(_clock * flickerRate + slot * 1.7f) * Mathf.Sin(_clock * flickerRate * 0.61f + slot);
            float length = Mathf.Max(0.001f, _flameLength[slot] * wobble);
            float girth = Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(_flameLength[slot] / Mathf.Max(0.01f, litFlame)));
            _hull.SetFlame(index, girth, length);
        }

        void ResetPose()
        {
            if (!_hullTransform) return;
            _hullTransform.localRotation = _hullRestRot;
            _hull.ResetRingPose();
            for (int i = -1; i < BrittleStarHullForm.Points; i++)
            {
                _hull.SetFlame(i, 1f, emberFlame);
                _flameLength[i + 1] = 0f;
            }
            _yaw = 0f;
        }

        // ---- element morph plumbing --------------------------------------------------------

        void InitializeProceduralMorphs(IVesselStatus vesselStatus)
        {
            if (!_hull) return;
            var resources = vesselStatus?.ResourceSystem;
            if (resources == null) return;
            _procMorphConfig = VesselElementalMorphConfigSO.LoadDefault();
            resources.OnElementLevelChange -= HandleElementLevelForMorph;   // detach-first
            resources.OnElementLevelChange += HandleElementLevelForMorph;
            // Seed the spawn silhouette instantly — the event only covers CHANGES.
            foreach (var element in BrittleStarHullForm.MorphElements)
                GlideMorph(element, resources.GetLevel(element), instant: true);
        }

        void HandleElementLevelForMorph(Element element, int level) => GlideMorph(element, level, instant: false);

        void GlideMorph(Element element, int level, bool instant)
        {
            int index = System.Array.IndexOf(BrittleStarHullForm.MorphElements, element);
            if (index < 0) return;   // None/Omni are not morph targets
            float target = VesselElementalMorphConfigSO.NormalizedMorphWeight(level);
            _morphTweens[index]?.Kill();
            if (instant || _procMorphConfig == null || _procMorphConfig.MorphDuration <= 0f)
            {
                _morphWeights[index] = target;
                _morphDirty = true;
                return;
            }
            _morphTweens[index] = DOTween
                .To(() => _morphWeights[index],
                    weight => { _morphWeights[index] = weight; _morphDirty = true; },
                    target, _procMorphConfig.MorphDuration)
                .SetEase(_procMorphConfig.MorphEase)
                .SetLink(gameObject);
        }

        /// <summary>Single writer for the morph push, after the base's shape-key write; then the ring's smear, once the
        /// frame's pose and morph are final.</summary>
        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (_morphDirty && _hull)
            {
                _morphDirty = false;
                _hull.ApplyElementMorphWeights(_morphWeights[0], _morphWeights[1], _morphWeights[2], _morphWeights[3]);
            }
            UpdateRingSmear();
        }

        // ---- the ring's motion smear ------------------------------------------------------

        /// <summary>
        /// THE RING'S MOTION SMEAR (Garrett, 2026-11-03; lab <c>Docs/Studios/BrittleStarRingLab.html</c>, "Forward +
        /// motion smear", normalized so it adds no brightness). When the drawn ring turned more than
        /// <c>ringSmearMinDegrees</c> this frame, the hull stops drawing it and <see cref="BrittleStarRingSmearPass"/>
        /// draws the AVERAGE of copies spread back over the angle it turned (<c>ringSmear</c> of it), one copy per
        /// <c>ringSmearStepDegrees</c>. Slower, the hull draws it as it stands. A thrown or regrowing ring never
        /// smears. A smear no camera drew (no URP renderer took the pass) shows the ring again at once and retries
        /// later, so the ring can never be left undrawn.
        /// </summary>
        void UpdateRingSmear()
        {
            float swept = _hasDrawnPhase && _drewThisFrame
                ? Mathf.DeltaAngle(_lastDrawnPhase * Mathf.Rad2Deg, _drawnPhase * Mathf.Rad2Deg) : 0f;
            if (_drewThisFrame)
            {
                _lastDrawnPhase = _drawnPhase;
                _hasDrawnPhase = true;
            }
            _drewThisFrame = false;

            // A smear submitted last frame that no camera drew: show the ring, warn once, retry in a while.
            if (_smearing && _smear.SubmittedFrame == Time.frameCount - 1 && _smear.DrawnFrame < _smear.SubmittedFrame)
            {
                if (!_smearWarned)
                {
                    _smearWarned = true;
                    CSDebug.LogWarning($"[BrittleStarAnimation] '{name}': no camera drew the ring's motion smear " +
                                       "(BrittleStarRingSmearPass: no URP camera took the pass, or none of the hull's " +
                                       "materials has a pass it can draw), so the ring is drawn unsmeared.");
                }
                _smearRetryAt = Time.unscaledTime + 2f;
                StopRingSmear();
                return;
            }

            var config = executor ? executor.Config : null;
            int copies = config && _hull && _drawnPresence >= 1f && Time.unscaledTime >= _smearRetryAt
                ? BrittleStarRules.SmearCopies(swept, config.RingSmear, config.RingSmearMinDegrees,
                                               config.RingSmearStepDegrees,
                                               Mathf.Min(config.RingSmearMaxCopies, BrittleStarRingSmearPass.MaxCopies))
                : 0;
            if (copies < 2 || !_hull.CanSmearRing || !BrittleStarRingSmearPass.CanDraw(config.RingSmearShader))
            {
                StopRingSmear();
                return;
            }

            if (!_smearRing)
            {
                _smearRing = new Mesh { name = "BrittleStarSmearRing" };
                _smearRing.MarkDynamic();
            }
            _hull.SetRingHidden(true);
            if (!_hull.BakeRing(_smearRing, out var toWorld, out var renderer, out var centre, out var axis, out float reach)
                || !renderer)
            {
                StopRingSmear();
                return;
            }

            _smear.Ring = _smearRing;
            _smear.RingMatrix = toWorld;
            _smear.Layer = renderer.gameObject.layer;
            renderer.GetSharedMaterials(_smear.Materials);
            // The renderer's per-slot property blocks (the flares, the vision band) go with each material.
            while (_smearBlocks.Count < _smear.Materials.Count) _smearBlocks.Add(new MaterialPropertyBlock());
            if (_smear.Blocks.Length != _smear.Materials.Count) _smear.Blocks = new MaterialPropertyBlock[_smear.Materials.Count];
            for (int i = 0; i < _smear.Materials.Count; i++)
            {
                _smearBlocks[i].Clear();
                renderer.GetPropertyBlock(_smearBlocks[i], i);
                _smear.Blocks[i] = _smearBlocks[i].isEmpty ? null : _smearBlocks[i];
            }
            // A disc in the ring's plane, a little wider than the ring reaches (the pods stand off the plane).
            Vector3 up = Vector3.ProjectOnPlane(transform.up, axis);
            if (up.sqrMagnitude < 1e-6f) up = Vector3.ProjectOnPlane(Vector3.up, axis);
            if (up.sqrMagnitude < 1e-6f) up = Vector3.ProjectOnPlane(Vector3.right, axis);
            _smear.DiscMatrix = Matrix4x4.TRS(centre, Quaternion.LookRotation(axis, up), Vector3.one * (reach * 1.1f + 0.5f));
            // Copy k sits BACK by `back` degrees about the nose axis; its points turn forward by that much onto the
            // ring as drawn (the hull turns the ring by +phase about the nose, BrittleStarHullModel.SetRingPose).
            float amount = config.RingSmear;
            for (int k = 0; k < copies; k++)
            {
                float back = BrittleStarRules.SmearCopyBack(k, copies, swept, amount);
                _smear.Back[k] = Matrix4x4.Translate(centre) * Matrix4x4.Rotate(Quaternion.AngleAxis(back, axis))
                               * Matrix4x4.Translate(-centre);
            }
            for (int k = copies; k < _smear.Back.Length; k++) _smear.Back[k] = Matrix4x4.identity;
            _smear.Copies = copies;
            BrittleStarRingSmearPass.Submit(_smear, config.RingSmearShader);
            _smearing = true;
        }

        void StopRingSmear()
        {
            if (!_smearing) return;
            _smearing = false;
            BrittleStarRingSmearPass.Withdraw(_smear);
            if (_hull) _hull.SetRingHidden(false);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            StopRingSmear();
            if (_smearRing) Destroy(_smearRing);
            for (int i = 0; i < _morphTweens.Length; i++) _morphTweens[i]?.Kill();
            if (VesselStatus?.ResourceSystem != null)
                VesselStatus.ResourceSystem.OnElementLevelChange -= HandleElementLevelForMorph;
        }

        // A pooled or swapped-away vessel must not carry a mid-turn pose into its next life.
        void OnDisable()
        {
            StopRingSmear();
            _hasDrawnPhase = false;
            ResetPose();
        }
    }
}
