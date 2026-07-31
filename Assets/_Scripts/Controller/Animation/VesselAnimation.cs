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

            AssignTransforms();

            _isInitialized = true;
        }

        protected virtual void OnDestroy()
        {
            if (VesselStatus?.ResourceSystem != null)
                VesselStatus.ResourceSystem.OnElementLevelChange -= UpdateShapeKey;
        }

        protected abstract void AssignTransforms();

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

        protected virtual void ResetAnimation(Transform part)
        {
            part.localRotation = Quaternion.Lerp(part.localRotation, Quaternion.identity, smallLerpAmount * Time.deltaTime);
        }

        protected virtual void ResetAnimation(Transform part, Quaternion resetQuaternion)
        {
            part.localRotation = Quaternion.Lerp(part.localRotation, resetQuaternion, smallLerpAmount * Time.deltaTime);
        }

        protected virtual void RotatePart(Transform part, float pitch, float yaw, float roll)
        {
            var targetRotation = Quaternion.Euler(pitch, yaw, roll);

            part.localRotation = Quaternion.Lerp(
                                        part.localRotation,
                                        targetRotation,
                                        lerpAmount * Time.deltaTime);
        }

        protected virtual void RotatePart(Transform part, float pitch, float yaw, float roll, Quaternion initialRotation)
        {
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
        // VesselElementalMorphConfigSO; fleet report: Tools > Cosmic Shore > Audit Vessel
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
        VesselElementalMorphConfigSO _morphConfig;

        void InitializeElementMorphs()
        {
            _elementShapes.Clear();
            CollectElementShapes(transform, _elementShapes);
            _elementMorphTweens = _elementShapes.Count > 0
                ? new Tween[_elementShapes.Count]
                : System.Array.Empty<Tween>();
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
                    shape.Renderer.SetBlendShapeWeight(shape.ShapeIndex, target);
                    continue;
                }

                var renderer = shape.Renderer;
                int shapeIndex = shape.ShapeIndex;
                _elementMorphTweens[i] = DOTween
                    .To(() => renderer.GetBlendShapeWeight(shapeIndex),
                        weight => renderer.SetBlendShapeWeight(shapeIndex, weight),
                        target, _morphConfig.morphDuration)
                    .SetEase(_morphConfig.morphEase)
                    .SetLink(renderer.gameObject);
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