using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using DG.Tweening;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Puppeteers the Shrike's hull (design: <c>R_VesselActions/SHRIKE.md</c> §2.2): the engine flame — an ember while
    /// docked, long while slinging — a gentle bank into the turn, the talon clamps gripping while docked, and the element
    /// morphs.
    ///
    /// <para><b>The hull is a MODEL</b> (Shrike.fbx, built by <c>Tools/Build/vessel_model/specs/shrike.py</c>, nested
    /// under the prefab's <c>ShrikeHull</c>). Its parts are found BY NAME, because an FBX's sub-object fileIDs exist only
    /// after Unity imports it: the <c>flame</c> bone (the placeholder's <c>ShrikeFlame</c> child still resolves), the
    /// <c>leg.L</c> / <c>leg.R</c> clamp bones, and the SkinnedMeshRenderer, whose Charge / Mass / Space / Time blend
    /// shapes the base class drives. The procedural <see cref="ShrikeHullBuilder"/> is kept until the model is accepted:
    /// on a prefab that still carries it, its vertex morph and its flame are driven exactly as before.</para>
    ///
    /// <para><b>Channel discipline.</b> This component writes TRANSFORMS only (the flame's scale, the drawn hull's lean,
    /// the clamps' swing); the blend shapes (or the builder) write VERTICES. No channel has two writers.</para>
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

        [Header("Talon clamps (the model's leg.L / leg.R bones)")]
        [Tooltip("Degrees the folded talon clamps swing down to grip while the hull is docked.")]
        [SerializeField, Range(0f, 120f)] float talonGripDegrees = 80f;
        [Tooltip("How fast the clamps open and fold (1/s, exponential).")]
        [SerializeField, Min(0.1f)] float talonRate = 8f;

        // Part names (the model's bones first, the placeholder's children after): names, not tuning.
        const string HullName = "ShrikeHull";
        const string FlameBone = "flame";
        const string PlaceholderFlame = "ShrikeFlame";
        const string TalonLeftBone = "leg.L";
        const string TalonRightBone = "leg.R";

        ShrikeHullBuilder _hull;          // the retired placeholder, while a prefab still carries it
        Transform _hullTransform;
        Quaternion _hullRestRot = Quaternion.identity;
        Transform _flame;
        Vector3 _flameRestScale = Vector3.one;
        int _flameLengthAxis = 2;         // the flame's local axis that runs aft (the placeholder's z; the bone's, measured)
        Transform _talonL, _talonR;
        float _grip;
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
            // The model's renderer is found, not wired (its fileID exists only after import). The base drives its element
            // shapes by name and flares its slots; the bank and the Space morph reach past the bind-pose bounds, so it
            // must not be culled on them.
            if (!SkinnedMeshRenderer) SkinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (SkinnedMeshRenderer) SkinnedMeshRenderer.updateWhenOffscreen = true;
            if (!_hull && !SkinnedMeshRenderer)
                CSDebug.LogWarning($"[ShrikeAnimation] '{name}' has neither the Shrike model nor a ShrikeHullBuilder in its " +
                                   "children; nothing to animate.");
            base.Initialize(vesselStatus);
            InitializeProceduralMorphs(vesselStatus);
        }

        protected override void ResolveParts()
        {
            // The Hull is what the bank leans: the model's parent (or the placeholder builder's own object).
            _hullTransform = _hull ? _hull.transform : ResolvePart(null, HullName);
            if (_hullTransform) _hullRestRot = _hullTransform.localRotation;
            _flame = ResolvePart(null, FlameBone, PlaceholderFlame);
            if (SkinnedMeshRenderer)
            {
                _talonL = ResolvePart(null, TalonLeftBone);
                _talonR = ResolvePart(null, TalonRightBone);
            }
            ReportUnresolvedParts();
            CaptureRestRotations(_talonL, _talonR);

            if (_flame)
            {
                _flameRestScale = _flame.localScale;
                _flameLengthAxis = AxisAlong(_flame, -transform.forward);
            }
        }

        /// <summary>The local axis of <paramref name="part"/> (0 x, 1 y, 2 z) that lies most nearly along
        /// <paramref name="worldDirection"/>, measured at rest: a bone's import frame is the exporter's, not ours.</summary>
        static int AxisAlong(Transform part, Vector3 worldDirection)
        {
            float x = Mathf.Abs(Vector3.Dot(part.right, worldDirection));
            float y = Mathf.Abs(Vector3.Dot(part.up, worldDirection));
            float z = Mathf.Abs(Vector3.Dot(part.forward, worldDirection));
            return x > y ? (x > z ? 0 : 2) : (y > z ? 1 : 2);
        }

        // Every part is resolved in ResolveParts.
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

            bool docked = executor && executor.Docked;
            _grip = Mathf.Lerp(_grip, docked ? 1f : 0f, 1f - Mathf.Exp(-talonRate * dt));
            ApplyTalon(_talonL);
            ApplyTalon(_talonR);

            if (!_flame) return;
            float target = executor && executor.Slinging ? slingFlame : docked ? dockedFlame : cruiseFlame;
            _flameLength = Mathf.Lerp(_flameLength, target, 1f - Mathf.Exp(-flameRate * dt));
            float wobble = 1f + flicker * Mathf.Sin(_clock * flickerRate) * Mathf.Sin(_clock * flickerRate * 0.61f + 1.3f);
            float girth = Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(_flameLength));
            var scale = new Vector3(girth, girth, girth);
            scale[_flameLengthAxis] = Mathf.Max(0.001f, _flameLength * wobble);
            _flame.localScale = Vector3.Scale(_flameRestScale, scale);
        }

        /// <summary>
        /// Swings a folded talon clamp down about the hull's right axis by the grip blend. Composed in the HULL's frame
        /// onto the rest captured in the vessel's frame, so the clamp leans with the bank and holds on any bone frame
        /// the importer gives it. <b>Sign:</b> the clamps rest folded aft (the vessel's -Z); a NEGATIVE angle about +X
        /// carries -Z toward -Y, so the claws swing DOWN to the block, as the model's <c>dock</c> take shows.
        /// </summary>
        void ApplyTalon(Transform talon)
        {
            if (!talon) return;
            Quaternion grip = Quaternion.AngleAxis(-_grip * talonGripDegrees, Vector3.right);
            Quaternion world = _hullTransform.rotation * grip * Quaternion.Inverse(_hullRestRot) * RestOrientationInVessel(talon);
            talon.localRotation = talon.parent ? Quaternion.Inverse(talon.parent.rotation) * world : world;
        }

        void ResetPose()
        {
            if (!_hullTransform) return;
            _hullTransform.localRotation = _hullRestRot;
            if (_flame) _flame.localScale = _flameRestScale;
            if (_talonL) _talonL.localRotation = RestRotationOf(_talonL);
            if (_talonR) _talonR.localRotation = RestRotationOf(_talonR);
            _flameLength = 1f;
            _grip = 0f;
            _yaw = 0f;
        }

        // ---- element morph plumbing for the retired placeholder (BrittleStarAnimation's, verbatim in shape) ----
        // The model's morphs are blend shapes, driven by the base class; this runs only while a prefab carries the builder.

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

        /// <summary>Single writer for the placeholder's morph push, after the base's shape-key write.</summary>
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
