using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Hangar's centre: the selected vessel as a live 3D model on a turntable (rev 3: "in the middle
    /// instead of a screenshot it should be a 3D view of the vessel").
    ///
    /// <para><b>A display copy, never the ship.</b> The model is harvested off the PREFAB ASSET by
    /// <see cref="VesselModelBuilder.TryBuildLive"/> - the same builder the Toy Box's vessel stations
    /// use - so no gameplay component ever wakes up. It wears the ship's own materials.</para>
    ///
    /// <para><b>Hull morphs stay live.</b> The builder flattens skinned meshes to static ones (a toy
    /// needs only a silhouette); a mesh that carries element blend shapes is re-skinned here with one
    /// bone per bind pose placed at that pose's inverse, which reproduces the bind pose exactly and lets
    /// <see cref="SetElementLevel"/> drive the charge / mass / space / time shapes through
    /// <see cref="VesselAnimation.CollectElementShapes"/> - the helper the ship's own animation uses.</para>
    ///
    /// <para><b>Rendered on demand</b> into a RenderTexture by a disabled, hand-stepped camera on a
    /// private stage parked far outside every gameplay camera (the <see cref="ToyPreviewCamera"/>
    /// pattern, on its own axis), with the game camera's post-processing adopted so emissive hull
    /// materials tonemap the way they do in play.</para>
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class HangarVesselStage : MonoBehaviour, IDragHandler
    {
        [Header("Data")]
        [Tooltip("Class -> prefab table the model is harvested from (Assets/_SO_Assets/Vessel Prefab Container.asset).")]
        [SerializeField] private VesselPrefabContainer prefabs;

        [Header("Framing")]
        [SerializeField, Min(0.5f)] private float modelRadius = 4f;
        [SerializeField, Min(1f)] private float distanceFactor = 2.6f;
        [SerializeField, Range(10f, 90f)] private float fieldOfView = 34f;
        [SerializeField] private float pitchDegrees = 18f;

        [Header("Turntable")]
        [Tooltip("Degrees per second the model turns on its own while untouched.")]
        [SerializeField] private float idleDegreesPerSecond = 14f;
        [Tooltip("Degrees per pixel of drag.")]
        [SerializeField] private float dragDegreesPerPixel = 0.35f;
        [Tooltip("Degrees per second at full right-stick deflection.")]
        [SerializeField] private float stickDegreesPerSecond = 160f;
        [Tooltip("Seconds of no input before the idle turn resumes.")]
        [SerializeField, Min(0f)] private float idleResumeSeconds = 2.5f;

        [Header("Perform (an ability pressed on the Abilities tab)")]
        [Tooltip("Seconds a perform lasts: a lunge, a roll and a burst of the element's colour.")]
        [SerializeField, Min(0.1f)] private float performSeconds = 0.55f;
        [SerializeField] private float performLunge = 1.4f;
        [SerializeField] private float performRollDegrees = 90f;
        [Tooltip("Peak intensity of the coloured light the burst is drawn with.")]
        [SerializeField] private float performLightIntensity = 6f;

        [Header("Effects")]
        [SerializeField, Tooltip("Bolts, muzzle flash and the upgrade moment, drawn in UI over the stage (optional).")]
        private HangarStageFx fx;
        [SerializeField, Min(0.05f), Tooltip("Seconds the upgrade pulse takes.")]
        private float pulseSeconds = 0.6f;
        [SerializeField, Tooltip("How much the hull swells at the top of an upgrade pulse.")]
        private float pulseScale = 0.12f;

        [Header("Cost")]
        [SerializeField, Min(1)] private int renderRate = 30;
        [SerializeField, Min(64)] private int resolution = 768;

        static readonly Vector3 StageOrigin = new(0f, -110000f, 30000f);

        RawImage _surface;
        KitSightline _sight;
        Camera _camera;
        RenderTexture _target;
        Transform _stage;
        Transform _pivot;
        GameObject _model;
        Light _burst;
        readonly List<VesselAnimation.ElementShapeTarget> _shapes = new();
        float _yaw = 200f;
        float _lastInput = -100f;
        float _nextRender;
        float _performT = -1f;
        Color _performColour = Color.white;
        HangarAbilityKind _performKind = HangarAbilityKind.Shot;
        Vector3 _modelBaseScale = Vector3.one;   // the builder fits the hull to modelRadius through this
        float _pulseT = -1f;
        Color _pulseColour = Color.white;
        float _morphT = -1f, _morphSeconds = 0.5f;
        int _morphFrom, _morphTo;
        Element _morphElement;

        /// <summary>The stage's UI effects (bolts, upgrade ring, banner), or null.</summary>
        public HangarStageFx Fx => fx;
        VesselClassType _shown = VesselClassType.Any;
        SO_Vessel _vessel;

        void Awake()
        {
            _surface = GetComponent<RawImage>();
            _sight = new KitSightline(transform, null, _surface ? _surface.canvasRenderer : null);
        }

        /// <summary>True while something covers the stage (the Abilities tab's TRY): no renders.</summary>
        public bool Covered { get; set; }

        // Released when the screen goes away (nothing holds a surface for a view it is not drawing) and
        // rebuilt for the same vessel when it comes back.
        void OnDisable() => Release();

        void OnEnable()
        {
            if (_vessel) SetVessel(_vessel);
        }

        /// <summary>Shows <paramref name="vessel"/> (no-op when it is already on the stage).</summary>
        public void SetVessel(SO_Vessel vessel)
        {
            if (!vessel || vessel.Class == _shown && _model) return;
            _vessel = vessel;
            _shown = vessel.Class;
            DestroyModel();
            if (!prefabs || !prefabs.TryGetShipPrefab(vessel.Class, out Transform prefab, reportMissing: false))
            {
                if (_surface) _surface.enabled = false;
                return;
            }
            EnsureRig();
            if (!VesselModelBuilder.TryBuildLive(prefab, modelRadius, null, out _model))
            {
                if (_surface) _surface.enabled = false;
                return;
            }
            _model.transform.SetParent(_pivot, false);
            _model.transform.localPosition = Vector3.zero;
            _modelBaseScale = _model.transform.localScale;
            SetLayerRecursive(_model, _stage.gameObject.layer);
            ReskinMorphMeshes(prefab, _model.transform);
            _shapes.Clear();
            VesselAnimation.CollectElementShapes(_model.transform, _shapes);
            if (_surface) _surface.enabled = true;
            _nextRender = 0f;
        }

        /// <summary>True once a model of the selected vessel stands on the stage.</summary>
        public bool HasModel => _model;

        /// <summary>True when the vessel on the stage has element blend shapes to preview.</summary>
        public bool HasMorphs => _shapes.Count > 0;

        /// <summary>Morphs the hull to <paramref name="level"/> (0..10) of <paramref name="element"/>.</summary>
        public void SetElementLevel(Element element, int level)
        {
            float k = VesselElementalMorphConfigSO.NormalizedMorphWeight(Mathf.Clamp(level, 0, 10));
            foreach (var s in _shapes)
                if (s.Element == element && s.Renderer)
                    s.Renderer.SetBlendShapeWeight(s.ShapeIndex, k * s.FullWeight);
            _nextRender = 0f;
        }

        /// <summary>Back to the default form: every element shape at zero.</summary>
        public void ResetMorphs()
        {
            foreach (var s in _shapes)
                if (s.Renderer) s.Renderer.SetBlendShapeWeight(s.ShapeIndex, 0f);
            _nextRender = 0f;
        }

        /// <summary>
        /// Plays a short "this is the ability" move in <paramref name="colour"/>: the hull's motion and the effect
        /// over the render both follow the ability's <paramref name="kind"/> (section 46) - a shot recoils and fires,
        /// a boost surges with exhaust, a stance settles under a closing ring, a strike rolls through a slash.
        /// </summary>
        public void Perform(Color colour, HangarAbilityKind kind)
        {
            _performColour = colour;
            _performKind = kind;
            _performT = 0f;
            if (fx && TryGetNoseViewport(out var from, out var dir)) fx.Play(kind, colour, from, dir);
            if (kind is HangarAbilityKind.Burst or HangarAbilityKind.Passive) Pulse(colour);
            _lastInput = Time.unscaledTime;
        }

        /// <summary>How each kind moves the hull: forward lunge and roll (as multiples of performLunge /
        /// performRollDegrees) and how small the hull blinks at the middle (1 = not at all).</summary>
        static (float lunge, float roll, float blink) MotionOf(HangarAbilityKind kind) => kind switch
        {
            HangarAbilityKind.Shot => (-0.25f, 0f, 1f),     // recoil
            HangarAbilityKind.Boost => (1.8f, 0.35f, 1f),   // surge with a strafing roll
            HangarAbilityKind.Phase => (2.2f, 0f, 0.35f),   // blink ahead
            HangarAbilityKind.Build => (0.6f, 0f, 1f),      // glide while laying
            HangarAbilityKind.Stance => (-0.4f, 0f, 1f),    // settle back
            HangarAbilityKind.Sense => (0f, 0f, 1f),        // hold still
            HangarAbilityKind.Pull => (0.25f, 0f, 1f),
            HangarAbilityKind.Strike => (1.1f, 4f, 1f),     // a full roll through the slash
            HangarAbilityKind.Burst => (0f, 0f, 1f),
            _ => (0f, 0f, 1f),
        };

        /// <summary>The hull swells and lights in <paramref name="colour"/> for a beat (an upgrade landing).</summary>
        public void Pulse(Color colour)
        {
            _pulseColour = colour;
            _pulseT = 0f;
            _lastInput = Time.unscaledTime;
        }

        /// <summary>Morphs <paramref name="element"/> from one level to another over <paramref name="seconds"/>.</summary>
        public void AnimateElementLevel(Element element, int from, int to, float seconds)
        {
            _morphElement = element;
            _morphFrom = from;
            _morphTo = to;
            _morphSeconds = Mathf.Max(0.05f, seconds);
            _morphT = 0f;
        }

        void ApplyMorphWeight(Element element, float level)
        {
            int lo = Mathf.FloorToInt(level), hi = Mathf.Min(10, lo + 1);
            float a = VesselElementalMorphConfigSO.NormalizedMorphWeight(Mathf.Clamp(lo, 0, 10));
            float b = VesselElementalMorphConfigSO.NormalizedMorphWeight(Mathf.Clamp(hi, 0, 10));
            float k = Mathf.Lerp(a, b, level - lo);
            foreach (var sh in _shapes)
                if (sh.Element == element && sh.Renderer)
                    sh.Renderer.SetBlendShapeWeight(sh.ShapeIndex, k * sh.FullWeight);
        }

        /// <summary>The vessel's nose and its facing, in 0..1 across the stage, for the bolts.</summary>
        public bool TryGetNoseViewport(out Vector2 from, out Vector2 dir)
        {
            from = new Vector2(0.5f, 0.5f);
            dir = Vector2.right;
            if (!_camera || !_model) return false;
            var fwd = _pivot ? _pivot.forward : Vector3.forward;
            var nose = _model.transform.position + fwd * modelRadius * 0.8f;
            var a = _camera.WorldToViewportPoint(nose);
            var b = _camera.WorldToViewportPoint(nose + fwd * modelRadius);
            if (a.z <= 0f) return false;
            from = a;
            dir = (Vector2)(b - a);
            if (dir.sqrMagnitude < 1e-6f) dir = Vector2.right;
            return true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            _yaw -= eventData.delta.x * dragDegreesPerPixel;
            _lastInput = Time.unscaledTime;
        }

        void LateUpdate()
        {
            if (!_camera || !_model) return;
            float dt = Time.unscaledDeltaTime;

            var pad = Gamepad.current;
            float stick = pad != null ? pad.rightStick.ReadValue().x : 0f;
            if (Mathf.Abs(stick) > 0.2f)
            {
                _yaw -= stick * stickDegreesPerSecond * dt;
                _lastInput = Time.unscaledTime;
            }
            else if (Time.unscaledTime - _lastInput > idleResumeSeconds)
            {
                _yaw += idleDegreesPerSecond * dt;
            }

            Vector3 lunge = Vector3.zero;
            float roll = 0f;
            float blink = 1f;
            if (_performT >= 0f)
            {
                _performT += dt / performSeconds;
                float k = Mathf.Clamp01(_performT);
                float bell = Mathf.Sin(k * Mathf.PI);
                var motion = MotionOf(_performKind);
                lunge = Vector3.forward * (performLunge * motion.lunge * bell);
                // A strike's roll turns all the way round; the others rock out and back.
                roll = _performKind == HangarAbilityKind.Strike ? 360f * k : performRollDegrees * motion.roll * bell;
                blink = Mathf.Lerp(1f, motion.blink, bell);
                if (_burst)
                {
                    _burst.color = _performColour;
                    _burst.intensity = performLightIntensity * bell;
                }
                if (_performT >= 1f) _performT = -1f;
            }

            float swell = 1f;
            if (_pulseT >= 0f)
            {
                _pulseT += dt / pulseSeconds;
                float bell = Mathf.Sin(Mathf.Clamp01(_pulseT) * Mathf.PI);
                swell = 1f + pulseScale * bell;
                if (_burst && _performT < 0f)
                {
                    _burst.color = _pulseColour;
                    _burst.intensity = performLightIntensity * 1.4f * bell;
                }
                if (_pulseT >= 1f) _pulseT = -1f;
                _nextRender = 0f;
            }
            if (_morphT >= 0f)
            {
                _morphT += dt / _morphSeconds;
                float e = 1f - (1f - Mathf.Clamp01(_morphT)) * (1f - Mathf.Clamp01(_morphT));
                ApplyMorphWeight(_morphElement, Mathf.Lerp(_morphFrom, _morphTo, e));
                if (_morphT >= 1f) _morphT = -1f;
                _nextRender = 0f;
            }

            _pivot.localRotation = Quaternion.Euler(0f, _yaw, 0f);
            _model.transform.localPosition = lunge;
            _model.transform.localRotation = Quaternion.Euler(0f, 0f, roll);
            _model.transform.localScale = _modelBaseScale * (swell * blink);

            if (Time.unscaledTime < _nextRender) return;
            // Screens stay ACTIVE beside each other in the strip (only freestyle switches them off), so
            // without this the stage rendered a 768 px vessel 30 times a second behind Home and every
            // other screen - a full camera render the player never saw (the menu slowdown, 2026-10-17).
            if (Covered || (_sight != null && !_sight.Visible) || !OnScreen()) return;
            _nextRender = Time.unscaledTime + 1f / Mathf.Max(1, renderRate);
            Step();
        }

        static readonly Vector3[] s_corners = new Vector3[4];

        /// <summary>True when any part of the stage lies inside the screen (the strip slides other screens
        /// off it).</summary>
        bool OnScreen()
        {
            if (!(transform is RectTransform rt)) return true;
            var canvas = rt.GetComponentInParent<Canvas>();
            var cam = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            rt.GetWorldCorners(s_corners);
            var min = RectTransformUtility.WorldToScreenPoint(cam, s_corners[0]);
            var max = RectTransformUtility.WorldToScreenPoint(cam, s_corners[2]);
            return max.x > 0f && min.x < Screen.width && max.y > 0f && min.y < Screen.height;
        }

        void Step()
        {
            // The first selection can land before the layout pass, when the surface has no size yet:
            // re-fit the target to the surface once it does, or the model renders into a 64 px stamp.
            var size = TargetSize();
            if (_target && (_target.width != size.x || _target.height != size.y)) EnsureRig();

            float distance = modelRadius * distanceFactor;
            var centre = _stage.position;
            var dir = Quaternion.Euler(pitchDegrees, 0f, 0f) * Vector3.back;
            _camera.transform.SetPositionAndRotation(centre + dir * distance, Quaternion.LookRotation(-dir, Vector3.up));
            _camera.nearClipPlane = Mathf.Max(0.05f, modelRadius * 0.05f);
            _camera.farClipPlane = distance * 3f;
            _camera.fieldOfView = fieldOfView;
            if (_target && _target.height > 0) _camera.aspect = (float)_target.width / _target.height;
            _camera.Render();
        }

        /// <summary>
        /// Gives each harvested static copy of a blend-shape mesh a SkinnedMeshRenderer again, so its
        /// element shapes can move. One bone per bind pose, placed at that pose's inverse under the
        /// copy, reproduces the authored bind pose exactly.
        /// </summary>
        void ReskinMorphMeshes(Transform prefab, Transform model)
        {
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (!mesh || mesh.blendShapeCount == 0) continue;
                var go = filter.gameObject;
                var materials = go.TryGetComponent<MeshRenderer>(out var mr) ? mr.sharedMaterials : null;
                if (mr) DestroyImmediate(mr);
                DestroyImmediate(filter);

                var skinned = go.AddComponent<SkinnedMeshRenderer>();
                skinned.sharedMesh = mesh;
                if (materials != null) skinned.sharedMaterials = materials;
                skinned.updateWhenOffscreen = true;
                skinned.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                skinned.receiveShadows = false;

                var binds = mesh.bindposes;
                if (binds.Length > 0)
                {
                    var bones = new Transform[binds.Length];
                    for (int i = 0; i < binds.Length; i++)
                    {
                        var bone = new GameObject($"Bind{i}").transform;
                        bone.SetParent(go.transform, false);
                        var m = binds[i].inverse;
                        bone.localPosition = m.GetColumn(3);
                        Vector3 f = m.GetColumn(2), u = m.GetColumn(1);
                        bone.localRotation = f.sqrMagnitude > 1e-8f && u.sqrMagnitude > 1e-8f ? Quaternion.LookRotation(f, u) : Quaternion.identity;
                        bone.localScale = new Vector3(((Vector3)m.GetColumn(0)).magnitude, u.magnitude, f.magnitude);
                        bones[i] = bone;
                    }
                    skinned.bones = bones;
                    skinned.rootBone = go.transform;
                }
            }
        }

        void EnsureRig()
        {
            var size = TargetSize();
            if (_target && (_target.width != size.x || _target.height != size.y)) ReleaseTarget();
            if (!_target)
            {
                _target = new RenderTexture(size.x, size.y, 16) { name = "HangarStage", antiAliasing = 1 };
                _target.Create();
                if (_surface) _surface.texture = _target;
            }

            if (!_stage)
            {
                var stage = new GameObject("HangarStage") { hideFlags = HideFlags.DontSave };
                stage.transform.position = StageOrigin;
                _stage = stage.transform;
                _pivot = new GameObject("Pivot").transform;
                _pivot.SetParent(_stage, false);
                var light = new GameObject("Burst").AddComponent<Light>();
                light.transform.SetParent(_stage, false);
                light.transform.localPosition = new Vector3(0f, modelRadius * 0.6f, -modelRadius * 1.2f);
                light.type = LightType.Point;
                light.range = modelRadius * 6f;
                light.intensity = 0f;
                _burst = light;
            }

            if (_camera)
            {
                _camera.targetTexture = _target;
                return;
            }
            var go = new GameObject("HangarStageCamera") { hideFlags = HideFlags.DontSave };
            _camera = go.AddComponent<Camera>();
            _camera.enabled = false;
            _camera.targetTexture = _target;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);   // the hangar backdrop shows through
            _camera.cullingMask = ~LayerMask.GetMask("UI");
            _camera.useOcclusionCulling = false;
            // No post-processing: URP's post pass writes alpha = 1 over the whole target, which turned the
            // transparent clear into the black box the owner saw (2026-10-17). Without it the hangar bay
            // drawn behind the render shows through around the vessel.
            OffscreenCameraSetup.AdoptGameCameraImage(_camera, postProcessing: false, antiAliasing: false, shadows: false);
            _camera.allowMSAA = false;
            var data = _camera.GetUniversalAdditionalCameraData();
            if (data)
            {
                data.requiresColorOption = CameraOverrideOption.Off;
                data.requiresDepthOption = CameraOverrideOption.Off;
            }
        }

        Vector2Int TargetSize()
        {
            float w = 1f, h = 1f;
            if (_surface)
            {
                var r = _surface.rectTransform.rect;
                if (r.width > 1f && r.height > 1f) { w = r.width; h = r.height; }
            }
            float k = resolution / Mathf.Max(w, h);
            return new Vector2Int(Mathf.Max(64, Mathf.RoundToInt(w * k)), Mathf.Max(64, Mathf.RoundToInt(h * k)));
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursive(child.gameObject, layer);
        }

        void DestroyModel()
        {
            _shapes.Clear();
            if (_model) Destroy(_model);
            _model = null;
        }

        void Release()
        {
            DestroyModel();
            _shown = VesselClassType.Any;
            if (_camera) { Destroy(_camera.gameObject); _camera = null; }
            if (_stage) { Destroy(_stage.gameObject); _stage = null; _pivot = null; _burst = null; }
            ReleaseTarget();
        }

        void ReleaseTarget()
        {
            if (!_target) return;
            if (_surface) _surface.texture = null;
            if (_camera) _camera.targetTexture = null;
            _target.Release();
            Destroy(_target);
            _target = null;
        }
    }
}
