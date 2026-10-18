using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using DG.Tweening;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Puppeteers the Shrike's hull (built by <see cref="ShrikeHullBuilder"/>; design: <c>R_VesselActions/SHRIKE.md</c>
    /// §2.2): the engine flame — an ember while docked, long while slinging — a gentle bank into the turn, and the
    /// element morphs (the fleet's tweened weights, the Brittle Star's plumbing).
    ///
    /// <para><b>Channel discipline.</b> This component writes TRANSFORMS only (the flame's scale, the drawn hull's lean);
    /// the builder writes VERTICES. No channel has two writers.</para>
    /// </summary>
    public class ShrikeAnimation : VesselAnimation
    {
        [Header("Wiring")]
        [Tooltip("This vessel's executor — docked, slinging. Serialized so a missing wire shows in the inspector; found " +
                 "in the vessel's children if left empty.")]
        [SerializeField] ShrikeExecutor executor;

        [Header("Engine flame (length multiple of the built flame)")]
        [SerializeField, Min(0f)] float cruiseFlame = 1f;
        [SerializeField, Min(0f)] float dockedFlame = 0.15f;
        [SerializeField, Min(0f)] float slingFlame = 4f;
        [Tooltip("How fast the flame eases to its target length (1/s, exponential).")]
        [SerializeField, Min(0.1f)] float flameRate = 10f;
        [Tooltip("Flicker: fraction of the flame's length it wavers by, and how fast.")]
        [SerializeField, Range(0f, 0.5f)] float flicker = 0.12f;
        [SerializeField, Min(0f)] float flickerRate = 22f;

        [Header("Hull")]
        [Tooltip("Degrees the drawn hull leans into a full-stick turn (fleet-scale puppetry: vessel rule 24).")]
        [SerializeField] float bankDegrees = 24f;

        ShrikeHullBuilder _hull;
        Transform _hullTransform;
        Quaternion _hullRestRot;
        float _flameLength = 1f;
        float _yaw;
        float _clock;

        readonly float[] _morphWeights = new float[4];      // charge, mass, space, time
        readonly Tween[] _morphTweens = new Tween[4];
        static readonly Element[] MorphElements = { Element.Charge, Element.Mass, Element.Space, Element.Time };
        VesselElementalMorphConfigSO _procMorphConfig;
        bool _morphDirty;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            _hull = GetComponentInChildren<ShrikeHullBuilder>(true);
            if (!executor) executor = GetComponentInChildren<ShrikeExecutor>(true);
            if (!_hull)
                CSDebug.LogWarning($"[ShrikeAnimation] '{name}' has no ShrikeHullBuilder in its children; nothing to animate.");
            base.Initialize(vesselStatus);
            if (_hull)
            {
                _hullTransform = _hull.transform;
                _hullRestRot = _hullTransform.localRotation;
            }
            InitializeProceduralMorphs(vesselStatus);
        }

        // The parts are the builder's, found through it rather than by name, so nothing to resolve.
        protected override void AssignTransforms() { }

        protected override void PerformShipPuppetry(float pitch, float yaw, float roll, float throttle) => Drive(yaw);

        /// <summary>The base calls Idle() INSTEAD of puppetry when the stick centres; the flame still lives.</summary>
        protected override void Idle() => Drive(0f);

        void Drive(float yaw)
        {
            if (!_hullTransform) return;
            float dt = Time.deltaTime;
            _clock += dt;
            _yaw = Mathf.Lerp(_yaw, Mathf.Clamp(yaw, -1f, 1f), 1f - Mathf.Exp(-10f * dt));
            _hullTransform.localRotation = _hullRestRot * Quaternion.Euler(0f, 0f, -_yaw * bankDegrees);

            var flame = _hull.Flame;
            if (!flame) return;
            float target = executor && executor.Slinging ? slingFlame : executor && executor.Docked ? dockedFlame : cruiseFlame;
            _flameLength = Mathf.Lerp(_flameLength, target, 1f - Mathf.Exp(-flameRate * dt));
            float wobble = 1f + flicker * Mathf.Sin(_clock * flickerRate) * Mathf.Sin(_clock * flickerRate * 0.61f + 1.3f);
            float girth = Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(_flameLength));
            flame.localScale = new Vector3(girth, girth, Mathf.Max(0.001f, _flameLength * wobble));
        }

        void ResetPose()
        {
            if (!_hullTransform) return;
            _hullTransform.localRotation = _hullRestRot;
            if (_hull.Flame) _hull.Flame.localScale = Vector3.one;
            _flameLength = 1f;
            _yaw = 0f;
        }

        // ---- element morph plumbing (BrittleStarAnimation's, verbatim in shape) ----

        void InitializeProceduralMorphs(IVesselStatus vesselStatus)
        {
            if (!_hull) return;
            var resources = vesselStatus?.ResourceSystem;
            if (resources == null) return;
            _procMorphConfig = VesselElementalMorphConfigSO.LoadDefault();
            resources.OnElementLevelChange -= HandleElementLevelForMorph;   // detach-first
            resources.OnElementLevelChange += HandleElementLevelForMorph;
            // Seed the spawn silhouette instantly — the event only covers CHANGES.
            foreach (var element in MorphElements)
                GlideMorph(element, resources.GetLevel(element), instant: true);
        }

        void HandleElementLevelForMorph(Element element, int level) => GlideMorph(element, level, instant: false);

        void GlideMorph(Element element, int level, bool instant)
        {
            int index = System.Array.IndexOf(MorphElements, element);
            if (index < 0) return;   // None/Omni are not morph targets
            float target = VesselElementalMorphConfigSO.NormalizedMorphWeight(level);
            _morphTweens[index]?.Kill();
            if (instant || _procMorphConfig == null || _procMorphConfig.morphDuration <= 0f)
            {
                _morphWeights[index] = target;
                _morphDirty = true;
                return;
            }
            _morphTweens[index] = DOTween
                .To(() => _morphWeights[index],
                    weight => { _morphWeights[index] = weight; _morphDirty = true; },
                    target, _procMorphConfig.morphDuration)
                .SetEase(_procMorphConfig.morphEase)
                .SetLink(gameObject);
        }

        /// <summary>Single writer for the morph push, after the base's shape-key write.</summary>
        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (!_morphDirty || !_hull) return;
            _morphDirty = false;
            _hull.ApplyElementMorphWeights(_morphWeights[0], _morphWeights[1], _morphWeights[2], _morphWeights[3]);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            for (int i = 0; i < _morphTweens.Length; i++) _morphTweens[i]?.Kill();
            if (VesselStatus?.ResourceSystem != null)
                VesselStatus.ResourceSystem.OnElementLevelChange -= HandleElementLevelForMorph;
        }

        // A pooled or swapped-away vessel must not carry a mid-turn pose into its next life.
        void OnDisable() => ResetPose();
    }
}
