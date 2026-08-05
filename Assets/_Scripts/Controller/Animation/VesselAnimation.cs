using CosmicShore.Gameplay;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Utility;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using DG.Tweening;
namespace CosmicShore.Gameplay
{
    public abstract class VesselAnimation : MonoBehaviour
    {
        [SerializeField] public SkinnedMeshRenderer SkinnedMeshRenderer;
        [SerializeField] bool SaveNewPositions; // TODO: remove after all models have shape keys support
        [SerializeField] protected float brakeThreshold = .65f;
        [SerializeField] protected float lerpAmount = 2f;
        [SerializeField] protected float smallLerpAmount = .7f;

        protected List<Transform> Transforms = new(); // TODO: use this to populate the vessel geometries on vessel.cs
        protected List<Quaternion> InitialRotations = new(); // TODO: use this to populate the vessel geometries on vessel.cs

        protected IVesselStatus VesselStatus;
        IInputStatus InputStatus => VesselStatus.InputStatus;


        bool _isInitialized;

        protected virtual void Update()
        {
            if (!_isInitialized)
                return;

            if (InputStatus.Idle) Idle();
            else if (VesselStatus.IsSingleStickControls) PerformShipPuppetry(InputStatus.EasedLeftJoystickPosition.y, InputStatus.EasedLeftJoystickPosition.x, 0, 0);
            else PerformShipPuppetry(InputStatus.YSum, InputStatus.XSum, InputStatus.YDiff, InputStatus.XDiff);
        }

        public virtual void Initialize(IVesselStatus vesselStatus)
        {
            VesselStatus = vesselStatus;
            InitializeElementMorphs();

            ResolveParts();
            AssignTransforms();

            _isInitialized = true;
        }

        protected virtual void OnDestroy()
        {
            if (VesselStatus?.ResourceSystem != null)
                VesselStatus.ResourceSystem.OnElementLevelChange -= UpdateShapeKey;
        }

        protected abstract void AssignTransforms();

        // --- Part resolution ---------------------------------------------------------------
        // A vessel's animated parts are found the same way its element morphs are: BY NAME.
        // An authored inspector reference always wins, so every already-wired vessel keeps its
        // exact behaviour; a part left empty is looked up among the model's descendants using
        // the candidate names the subclass declares. That is what lets a vessel's art be swapped
        // for a rigged model (the shape-key rigs whose bones ARE the parts - 'wing.l', 'jetT.r',
        // 'jaw.u') without re-wiring a dozen inspector fields by hand: the stale references come
        // back null and the bones resolve themselves.

        Dictionary<string, Transform> _partsByName;
        readonly List<string> _unresolvedParts = new();

        /// <summary>
        /// Hook for subclasses to resolve their part fields via <see cref="ResolvePart"/> before
        /// <see cref="AssignTransforms"/> runs. Base implementation does nothing, so vessels that
        /// rely purely on authored references are unaffected.
        /// </summary>
        protected virtual void ResolveParts() { }

        /// <summary>
        /// Returns <paramref name="authored"/> when it is wired; otherwise the first descendant
        /// whose name matches one of <paramref name="candidateNames"/> (case-insensitive, in
        /// priority order - put the current rig's bone name first and legacy part names after).
        /// Unresolved parts are collected and reported once, loudly, at the end of resolution.
        /// </summary>
        protected Transform ResolvePart(Transform authored, params string[] candidateNames)
        {
            if (authored) return authored;

            if (_partsByName == null)
            {
                _partsByName = new Dictionary<string, Transform>(System.StringComparer.OrdinalIgnoreCase);
                foreach (var t in GetComponentsInChildren<Transform>(true))
                    if (!_partsByName.ContainsKey(t.name)) // first occurrence wins
                        _partsByName[t.name] = t;
            }

            for (int i = 0; i < candidateNames.Length; i++)
                if (_partsByName.TryGetValue(candidateNames[i], out var found))
                    return found;

            if (candidateNames.Length > 0)
                _unresolvedParts.Add(candidateNames[0]);
            return null;
        }

        /// <summary>
        /// Call at the end of a subclass's <see cref="ResolveParts"/>: reports every part that
        /// neither an authored reference nor the model could supply. A silently unbound part is
        /// a limb that stops animating, so this must not fail quietly.
        /// </summary>
        protected void ReportUnresolvedParts()
        {
            if (_unresolvedParts.Count == 0) return;
            CSDebug.LogWarning($"[{GetType().Name}] '{name}' could not resolve animated part(s): " +
                               $"{string.Join(", ", _unresolvedParts)}. They will not animate - wire them " +
                               "in the inspector, or check that the model's bone names match.");
            _unresolvedParts.Clear();
        }

        /// <summary>Local rotation of a part, or identity when it is unbound (keeps index alignment).</summary>
        protected static Quaternion LocalRotationOf(Transform part) =>
            part ? part.localRotation : Quaternion.identity;

        // --- Rest poses --------------------------------------------------------------------
        // Puppetry drives a part TOWARD an absolute local rotation, which silently assumes the
        // part rests at identity. That holds for a part-per-mesh model whose pieces are placed by
        // translation alone, but NOT for a rigged model: a bone's rest pose is what fans the
        // engines out and sweeps the wings back (the rhino rig's 'wing1.l' rests at ~42 degrees,
        // 'jet.l' at ~115). Driving those toward a bare Euler tears the ship out of its rest pose
        // the moment it animates. Parts registered here are driven RELATIVE to the pose they were
        // authored in, so identity-rest art behaves exactly as before and rigged art holds shape.

        readonly Dictionary<Transform, Quaternion> _restRotations = new();

        /// <summary>Records each part's authored local rotation as its rest pose. Call from ResolveParts.</summary>
        protected void CaptureRestRotations(params Transform[] parts)
        {
            foreach (var part in parts)
                if (part) _restRotations[part] = part.localRotation;
        }

        /// <summary>The captured rest pose of a part, or identity when it has none.</summary>
        protected Quaternion RestRotationOf(Transform part) =>
            part && _restRotations.TryGetValue(part, out var rest) ? rest : Quaternion.identity;

        /// <summary>
        /// Rest-relative <see cref="RotatePart"/>: drives the part toward its captured rest pose
        /// composed with the requested rotation. Identical to <see cref="RotatePart"/> for parts
        /// resting at identity, so legacy art is unaffected.
        /// </summary>
        protected void RotatePartFromRest(Transform part, float pitch, float yaw, float roll)
        {
            if (!part) return;
            var targetRotation = Quaternion.Euler(pitch, yaw, roll) * RestRotationOf(part);

            part.localRotation = Quaternion.Lerp(
                                        part.localRotation,
                                        targetRotation,
                                        lerpAmount * Time.deltaTime);
        }

        // Vessel animations TODO: figure out how to leverage a single definition for pitch, etc. that captures the gyro in the animations.
        protected abstract void PerformShipPuppetry(float Pitch, float Yaw, float Roll, float Throttle);
        protected virtual void Idle()
        {
            if (SaveNewPositions)
            {
                for (var i = 0; i < Transforms.Count; i++)
                {
                    // CSDebug.LogWarningFormat("{0} - {1} - index: {2}", "VesselAnimation" , nameof(Idle), i.ToString());
                    // CSDebug.LogWarningFormat("{0} - {1} - transform value: {2}", "VesselAnimation" , nameof(Idle), Transforms[i]);
                    // CSDebug.LogWarningFormat("{0} - {1} - transform value: {2}", "VesselAnimation" , nameof(Idle), InitialRotations[i].ToString());
                    // CSDebug.LogWarningFormat("{0} - {1} - initial rotations max index: {2}", "VesselAnimation" , nameof(Idle), InitialRotations.Count.ToString());
                    if (i < InitialRotations.Count)
                    {
                        ResetAnimation(Transforms[i], InitialRotations[i]);
                    }
                    else
                    {
                        ResetAnimation(Transforms[i]);
                    }
                }

            }
            else
            {
                foreach (Transform transform in Transforms)
                    ResetAnimation(transform);
            }
        }

        protected virtual float Brake(float throttle)
        {
            return (throttle < brakeThreshold) ? throttle - brakeThreshold : 0;
        }

        // The part guards below keep an unbound limb from taking the whole vessel's animation
        // down with a NullReferenceException every frame: an art swap that renames one bone
        // costs that limb's motion (reported by ReportUnresolvedParts), not the ship.
        // Settles toward the part's REST pose, which is identity unless CaptureRestRotations
        // recorded otherwise - so idling a rigged vessel relaxes its bones to the pose the rig
        // was authored in instead of flattening them.
        protected virtual void ResetAnimation(Transform part)
        {
            if (!part) return;
            part.localRotation = Quaternion.Lerp(part.localRotation, RestRotationOf(part), smallLerpAmount * Time.deltaTime);
        }

        protected virtual void ResetAnimation(Transform part, Quaternion resetQuaternion)
        {
            if (!part) return;
            part.localRotation = Quaternion.Lerp(part.localRotation, resetQuaternion, smallLerpAmount * Time.deltaTime);
        }

        protected virtual void RotatePart(Transform part, float pitch, float yaw, float roll)
        {
            if (!part) return;
            var targetRotation = Quaternion.Euler(pitch, yaw, roll);

            part.localRotation = Quaternion.Lerp(
                                        part.localRotation,
                                        targetRotation,
                                        lerpAmount * Time.deltaTime);
        }

        protected virtual void RotatePart(Transform part, float pitch, float yaw, float roll, Quaternion initialRotation)
        {
            if (!part) return;
            var targetRotation = Quaternion.Euler(pitch, roll, yaw) * initialRotation;

            part.localRotation = Quaternion.Lerp(
                                        part.localRotation,
                                        targetRotation,
                                        lerpAmount * Time.deltaTime);
        }

        // --- Elemental hull morphs ---------------------------------------------------------
        // The vessel MODEL is an element display: its skinned meshes carry blend shapes labeled
        // by element name (charge / mass / space / time - authored into the FBX), and each one
        // glides between its extremes as the vessel's effective element level moves through the
        // [0,10] progression band (the deficit band [-5,0) holds the level-0 silhouette, the
        // overcharge band (10,15] holds the level-10 extreme). Discovery is by NAME, so a vessel
        // opts in simply by shipping labeled shape keys - no per-prefab wiring - and models
        // without them (or with unrelated art shapes) are untouched. Feel lives in the shared
        // VesselElementalMorphConfigSO; fleet report: FrogletTools > Vessels > Audit Vessel
        // Elemental Morphs.

        /// <summary>One element-labeled blend shape on one of the vessel model's skinned meshes.</summary>
        public struct ElementShapeTarget
        {
            public Element Element;
            public SkinnedMeshRenderer Renderer;
            public int ShapeIndex;
            public string ShapeName;
            public float FullWeight; // the shape's authored extreme (its last frame weight)
        }

        readonly List<ElementShapeTarget> _elementShapes = new();
        Tween[] _elementMorphTweens = System.Array.Empty<Tween>();
        float[] _elementShapeWeights = System.Array.Empty<float>();
        VesselElementalMorphConfigSO _morphConfig;

        void InitializeElementMorphs()
        {
            _elementShapes.Clear();
            CollectElementShapes(transform, _elementShapes);
            _elementMorphTweens = _elementShapes.Count > 0
                ? new Tween[_elementShapes.Count]
                : System.Array.Empty<Tween>();
            _elementShapeWeights = _elementShapes.Count > 0
                ? new float[_elementShapes.Count]
                : System.Array.Empty<float>();
            for (int i = 0; i < _elementShapes.Count; i++)
                _elementShapeWeights[i] = _elementShapes[i].Renderer.GetBlendShapeWeight(_elementShapes[i].ShapeIndex);
            if (_elementShapes.Count == 0) return; // model ships no element shapes - nothing to drive

            _morphConfig = VesselElementalMorphConfigSO.LoadDefault();

            var resources = VesselStatus.ResourceSystem;
            resources.OnElementLevelChange -= UpdateShapeKey; // re-init safe: never double-subscribe
            resources.OnElementLevelChange += UpdateShapeKey;

            // Seed the spawn silhouette from the live levels - the event only covers CHANGES,
            // and a vessel can spawn (or swap in) mid-session with levels already earned.
            foreach (var element in VesselElementalMorphConfigSO.MorphElements)
                MorphToLevel(element, resources.GetLevel(element), instant: true);
        }

        public virtual void UpdateShapeKey(Element element, int level) =>
            MorphToLevel(element, level, instant: false);

        // Tweens drive the CACHED weight; LateUpdate is the single writer to the renderers.
        // Unity's Animator writes bound curves every frame during the animation update - after
        // Update, where tweens run - so an export carrying even constant-zero blend-shape curves
        // (a common Blender residue) would stomp script-set weights every frame. Writing in
        // LateUpdate makes the element level authoritative over any such stray animation curve,
        // on every vessel. The current fleet's takes are clean; the defense is deliberate.
        void MorphToLevel(Element element, int level, bool instant)
        {
            float normalized = VesselElementalMorphConfigSO.NormalizedMorphWeight(level);
            for (int i = 0; i < _elementShapes.Count; i++)
            {
                var shape = _elementShapes[i];
                if (shape.Element != element) continue;

                float target = normalized * shape.FullWeight;
                _elementMorphTweens[i]?.Kill();

                if (instant || _morphConfig.morphDuration <= 0f)
                {
                    _elementShapeWeights[i] = target;
                    shape.Renderer.SetBlendShapeWeight(shape.ShapeIndex, target);
                    continue;
                }

                int index = i;
                _elementMorphTweens[i] = DOTween
                    .To(() => _elementShapeWeights[index],
                        weight => _elementShapeWeights[index] = weight,
                        target, _morphConfig.morphDuration)
                    .SetEase(_morphConfig.morphEase)
                    .SetLink(shape.Renderer.gameObject);
            }
        }

        protected virtual void LateUpdate()
        {
            for (int i = 0; i < _elementShapes.Count; i++)
            {
                var shape = _elementShapes[i];
                if (shape.Renderer)
                    shape.Renderer.SetBlendShapeWeight(shape.ShapeIndex, _elementShapeWeights[i]);
            }
        }

        /// <summary>
        /// Finds every element-labeled blend shape on the skinned meshes under <paramref name="root"/>
        /// (labeling contract: <see cref="VesselElementalMorphConfigSO.TryResolveElement"/>). Shared
        /// with the editor auditor so the report and the runtime discover identically.
        /// </summary>
        public static void CollectElementShapes(Transform root, List<ElementShapeTarget> results)
        {
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = renderer.sharedMesh;
                if (!mesh) continue;

                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    string shapeName = mesh.GetBlendShapeName(i);
                    if (!VesselElementalMorphConfigSO.TryResolveElement(shapeName, out var element)) continue;

                    int lastFrame = mesh.GetBlendShapeFrameCount(i) - 1;
                    float fullWeight = lastFrame >= 0 ? mesh.GetBlendShapeFrameWeight(i, lastFrame) : 100f;
                    if (fullWeight <= 0f) fullWeight = 100f;

                    results.Add(new ElementShapeTarget
                    {
                        Element = element,
                        Renderer = renderer,
                        ShapeIndex = i,
                        ShapeName = shapeName,
                        FullWeight = fullWeight,
                    });
                }
            }
        }

        public virtual void FlareEngine()
        {
            if (SkinnedMeshRenderer) SkinnedMeshRenderer.materials[3].SetFloat("_ColorMultiplier",5f);
        }

        public virtual void StopFlareEngine()
        {
            if (SkinnedMeshRenderer) SkinnedMeshRenderer.materials[3].SetFloat("_ColorMultiplier", 1f);
        }
        
        public virtual void FlareBody()
        {
            if (SkinnedMeshRenderer) SkinnedMeshRenderer.materials[0].SetFloat("_ColorMultiplier",5f);
        }
        public virtual void FlareBody(float amount)
        {
            if (SkinnedMeshRenderer) SkinnedMeshRenderer.materials[0].SetFloat("_ColorMultiplier",1 + amount*4f);
        }

        public virtual void StopFlareBody()
        {
            if (SkinnedMeshRenderer) SkinnedMeshRenderer.materials[0].SetFloat("_ColorMultiplier", 1f);
        }


    }
}