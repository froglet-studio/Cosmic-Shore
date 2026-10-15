using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using DG.Tweening;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Puppeteers the Brittle Star's hull (built by <see cref="BrittleStarHullBuilder"/>). Design record:
    /// <c>R_VesselActions/BRITTLE_STAR.md</c> §2.2. Everything it draws is the class document's:
    /// <list type="bullet">
    /// <item><b>Thrusters.</b> "When speed mode is off, only [the] thruster on the cockpit should be engaged. The outer
    /// thrusters should only engage as the vessel banks. When speed mode is turned on, all of the thrusters should engage
    /// at once." The pod flames on the OUTSIDE of a turn light with the stick; Speed Mode (and Overdrive) light all five.</item>
    /// <item><b>The firing rotation.</b> The support ring turns about the nose axis to bring the aimed turret round
    /// (<see cref="BrittleStarExecutor.RingPhase"/>), and Creation Mode's five corner trails turn with it.</item>
    /// <item><b>The ring's life.</b> Thrown, it is gone from the hull; rebuilt, it blooms back out from the cockpit
    /// (<see cref="BrittleStarExecutor.RingPresence01"/>) — continuity of existence, never a pop.</item>
    /// </list>
    ///
    /// <para><b>Channel discipline.</b> This component writes TRANSFORMS only: the ring's localRotation and localScale,
    /// the flames' localScale, and a gentle bank of the drawn hull. The builder writes VERTICES (the element morph) and
    /// the muzzles' positions. No channel has two writers.</para>
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
        [Tooltip("Overdrive's extra flame length, as a multiple.")]
        [SerializeField, Min(1f)] float overdriveFlame = 1.35f;
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

        BrittleStarHullBuilder _hull;
        Transform _hullTransform;
        Quaternion _hullRestRot;
        Vector3 _ringRestScale = Vector3.one;
        readonly float[] _flameLength = new float[BrittleStarHullForm.Points + 1];
        float _yaw;
        float _clock;

        // ---- procedural element morphs (the StoatAnimation plumbing, verbatim in shape) ----
        readonly float[] _morphWeights = new float[4];      // BrittleStarHullForm.MorphElements order
        readonly Tween[] _morphTweens = new Tween[4];
        VesselElementalMorphConfigSO _procMorphConfig;
        bool _morphDirty;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            _hull = GetComponentInChildren<BrittleStarHullBuilder>(true);
            if (!executor) executor = GetComponentInChildren<BrittleStarExecutor>(true);
            if (!_hull)
                CSDebug.LogWarning($"[BrittleStarAnimation] '{name}' has no BrittleStarHullBuilder in its children; nothing to animate.");
            base.Initialize(vesselStatus);
            CaptureRest();
            InitializeProceduralMorphs(vesselStatus);
        }

        void CaptureRest()
        {
            if (!_hull) return;
            _hullTransform = _hull.transform;
            _hullRestRot = _hullTransform.localRotation;
            if (_hull.Ring) _ringRestScale = _hull.Ring.localScale;
            for (int i = 0; i < _flameLength.Length; i++) _flameLength[i] = 0f;
        }

        // The parts are the builder's, found through it rather than by name, so nothing to resolve.
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
            bool overdrive = executor && executor.Overdrive;
            var ring = _hull.Ring;
            if (ring)
            {
                float phase = executor ? executor.RingPhase : 0f;
                if (executor && executor.Creation01 > 0f && executor.AimedPoint < 0 && creationIdleSpin != 0f)
                    phase += _clock * creationIdleSpin * Mathf.Deg2Rad;
                ring.localRotation = Quaternion.Euler(0f, 0f, phase * Mathf.Rad2Deg);
                // Thrown: gone. Rebuilt: blooms back out with a little overshoot.
                float presence = executor ? executor.RingPresence01 : 1f;
                float bloom = presence >= 1f ? 1f : DOVirtual.EasedValue(0f, 1f, presence, Ease.OutBack);
                ring.localScale = _ringRestScale * Mathf.Max(0.001f, bloom);
            }

            // The cockpit's own thruster: always lit, longer with Speed Mode.
            SetFlame(-1, Mathf.Lerp(cockpitFlame, litFlame, speed01), overdrive, dt);

            // The pods: an ember at rest, lit on the outside of a turn, all lit in Speed Mode.
            float ringPhase = executor ? executor.RingPhase : 0f;
            for (int i = 0; i < BrittleStarHullForm.Points; i++)
            {
                // The pod's side of the hull right now (+1 right … -1 left); a right turn lights the left pods.
                float side = Mathf.Cos(BrittleStarHullForm.PointAngle(i) + ringPhase);
                float bank = Mathf.Clamp01(-side * _yaw / bankToLight);
                float lit = Mathf.Max(speed01, bank);
                SetFlame(i, Mathf.Lerp(emberFlame, litFlame, lit), overdrive, dt);
            }
        }

        void SetFlame(int index, float target, bool overdrive, float dt)
        {
            var flame = _hull.Flame(index);
            if (!flame) return;
            if (overdrive) target *= overdriveFlame;
            int slot = index + 1;
            _flameLength[slot] = Mathf.Lerp(_flameLength[slot], target, 1f - Mathf.Exp(-flameRate * dt));
            float wobble = 1f + flicker * Mathf.Sin(_clock * flickerRate + slot * 1.7f) * Mathf.Sin(_clock * flickerRate * 0.61f + slot);
            float length = Mathf.Max(0.001f, _flameLength[slot] * wobble);
            float girth = Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(_flameLength[slot] / Mathf.Max(0.01f, litFlame)));
            flame.localScale = new Vector3(girth, girth, length);
        }

        void ResetPose()
        {
            if (!_hullTransform) return;
            _hullTransform.localRotation = _hullRestRot;
            if (_hull.Ring)
            {
                _hull.Ring.localRotation = Quaternion.identity;
                _hull.Ring.localScale = _ringRestScale;
            }
            for (int i = -1; i < BrittleStarHullForm.Points; i++)
            {
                var flame = _hull.Flame(i);
                if (flame) flame.localScale = new Vector3(1f, 1f, emberFlame);
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
