using CosmicShore.Gameplay;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Utility;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
namespace CosmicShore.Gameplay
{
    public abstract class VesselAnimation : MonoBehaviour
    {
        [SerializeField] public SkinnedMeshRenderer SkinnedMeshRenderer;
        [SerializeField] bool SaveNewPositions; // TODO: remove after all models have shape keys support
        [SerializeField] bool UseShapeKeys; // TODO: remove after all models have shape keys support
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
            VesselStatus.ResourceSystem.OnElementLevelChange += UpdateShapeKey;

            AssignTransforms();

            _isInitialized = true;
        }
        protected abstract void AssignTransforms();

        // --- Part resolution ---------------------------------------------------------------
        // A vessel's animated parts can be found BY NAME. An authored inspector reference
        // always wins, so every already-wired vessel keeps its exact behaviour; a part left
        // empty is looked up among the model's descendants using the candidate names the
        // subclass declares. That is what lets a vessel's art be swapped for a rigged model
        // (the shape-key rigs whose bones ARE the parts - 'wing.l', 'jetT.r', 'jaw.u') without
        // re-wiring a dozen inspector fields by hand: the stale references come back null and
        // the bones resolve themselves.

        Dictionary<string, Transform> _partsByName;
        readonly List<string> _unresolvedParts = new();

        /// <summary>
        /// Returns <paramref name="authored"/> when it is wired; otherwise the first descendant
        /// whose name matches one of <paramref name="candidateNames"/> (case-insensitive, in
        /// priority order - put the current rig's bone name first and legacy part names after).
        /// Unresolved parts are collected and reported once, loudly, via
        /// <see cref="ReportUnresolvedParts"/>.
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
        /// Call after a subclass has resolved its part fields: reports every part that neither
        /// an authored reference nor the model could supply. A silently unbound part is a limb
        /// that stops animating, so this must not fail quietly.
        /// </summary>
        protected void ReportUnresolvedParts()
        {
            if (_unresolvedParts.Count == 0) return;
            CSDebug.LogWarning($"[{GetType().Name}] '{name}' could not resolve animated part(s): " +
                               $"{string.Join(", ", _unresolvedParts)}. They will not animate - wire them " +
                               "in the inspector, or check that the model's bone names match.");
            _unresolvedParts.Clear();
        }

        // --- Rest poses --------------------------------------------------------------------
        // Puppetry drives a part TOWARD an absolute local rotation, which silently assumes the
        // part rests at identity. That holds for a part-per-mesh model whose pieces are placed by
        // translation alone, but NOT for a rigged model: a bone's rest pose is what fans the
        // engines out and sweeps the wings back. Driving those toward a bare Euler tears the ship
        // out of its rest pose the moment it animates. Parts registered here are driven RELATIVE
        // to the pose they were authored in, so identity-rest art behaves exactly as before and
        // rigged art holds shape.

        readonly Dictionary<Transform, Quaternion> _restRotations = new();

        /// <summary>Records each part's authored local rotation as its rest pose.</summary>
        protected void CaptureRestRotations(params Transform[] parts)
        {
            foreach (var part in parts)
                if (part) _restRotations[part] = part.localRotation;
        }

        /// <summary>The captured rest pose of a part, or identity when it has none.</summary>
        protected Quaternion RestRotationOf(Transform part) =>
            part && _restRotations.TryGetValue(part, out var rest) ? rest : Quaternion.identity;

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

        public virtual void UpdateShapeKey(Element element, int level)
        {
            if (!UseShapeKeys) return;

            var index = 0;
            switch (element)
            {
                case Element.Mass: index = 0; break;
                case Element.Charge: index = 1; break;
                case Element.Space: index = 2; break;
                case Element.Time: index = 3; break;
            }
            SkinnedMeshRenderer.SetBlendShapeWeight(index, level / 10f);
        }

        // --- Elemental hull morph discovery -------------------------------------------------
        // The vessel MODEL can be an element display: a skinned mesh may carry blend shapes
        // labeled by element name (charge / mass / space / time - authored into the FBX,
        // resolved via VesselElementalMorphConfigSO.TryResolveElement). Discovery is by NAME, so
        // a vessel opts in simply by shipping labeled shape keys. This is pure discovery only -
        // shared with the editor auditor (FrogletTools > Vessels > Audit Vessel Elemental
        // Morphs) so the report and any future runtime driver can never disagree about what a
        // model actually ships.

        /// <summary>One element-labeled blend shape on one of the vessel model's skinned meshes.</summary>
        public struct ElementShapeTarget
        {
            public Element Element;
            public SkinnedMeshRenderer Renderer;
            public int ShapeIndex;
            public string ShapeName;
            public float FullWeight; // the shape's authored extreme (its last frame weight)
        }

        /// <summary>
        /// Finds every element-labeled blend shape on the skinned meshes under <paramref name="root"/>
        /// (labeling contract: <see cref="VesselElementalMorphConfigSO.TryResolveElement"/>).
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