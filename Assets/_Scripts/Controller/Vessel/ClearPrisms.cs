using CosmicShore.Gameplay;
using UnityEngine;
using CosmicShore.Utility;
using System.Collections.Generic;

namespace CosmicShore.Gameplay
{
    public class ClearPrisms : MonoBehaviour
    {
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");

        Transform mainCamera;

        [SerializeField, RequireInterface(typeof(IVessel))]
        Object _shipMono;
        IVessel Vessel => _shipMono as IVessel;

        [SerializeField] AnimationCurve scaleCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [SerializeField] float capsuleRadius = 5f;

        Transform visibilityCapsuleTransform;

        private CapsuleCollider visibilityCapsule;

        Vector3 capsuleDirection;

        CameraManager cameraManager;
        GeometryUtils.LineData lineData;

        // Prisms currently inside the visibility capsule, with their cached renderers so
        // OnTriggerStay does no GetComponent. A reusable property block applies the view
        // alpha without cloning the (shared, GPU-instanced) prism material every frame.
        readonly Dictionary<Collider, Renderer> _trackedRenderers = new();
        MaterialPropertyBlock _propertyBlock;

        bool isInitialized;


        private void OnEnable()
        {
            if (Vessel == null)
            {
                CSDebug.LogError("Vessel instance is not set or does not implement IVessel interface.");
                enabled = false;
                return;
            }

            Vessel.OnInitialized += VesselInitialized;
            Vessel.OnBeforeDestroyed += OnBeforeVesselDestroyed;
        }

        private void OnDisable()
        {
            Vessel.OnInitialized -= VesselInitialized;
            Vessel.OnBeforeDestroyed -= OnBeforeVesselDestroyed;
        }

        private void OnBeforeVesselDestroyed()
        {
            isInitialized = false; // Destroy(gameObject);
            _trackedRenderers.Clear();
        }


        private void VesselInitialized()
        {
            cameraManager = CameraManager.Instance;
            mainCamera = cameraManager.GetCloseCamera();
            if (mainCamera == null)
            {
                CSDebug.LogError("Close main camera not found! This should not happen!");
                return;
            }
            
            visibilityCapsuleTransform = new GameObject("Visibility Capsule").transform;
            transform.SetParent(visibilityCapsuleTransform);
            visibilityCapsule = gameObject.AddComponent<CapsuleCollider>();
            visibilityCapsule.isTrigger = true;
            visibilityCapsule.radius = capsuleRadius;

            isInitialized = true;
        }

        void Update()
        {
            if (!isInitialized)
                return;

            Vector3 cameraPosition = mainCamera.position;
            Vector3 shipPosition = Vessel.Transform.position;

            // Position the capsule between the camera and the vessel
            transform.position = (cameraPosition + shipPosition) / 2f;
            transform.LookAt(shipPosition);

            // Scale the capsule to fit between the camera and vessel
            float distance = Vector3.Distance(cameraPosition, shipPosition);
            visibilityCapsule.height = distance;

            // Update the capsule's end positions
            capsuleDirection = (shipPosition - cameraPosition).normalized;
            visibilityCapsule.center = Vector3.zero;
            transform.up = capsuleDirection;
            lineData = GeometryUtils.PrecomputeLineData(cameraPosition, shipPosition);
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent(out Prism prism))
                return;

            prism.SetTransparency(true);
            if (other.TryGetComponent(out Renderer renderer))
                _trackedRenderers[other] = renderer;
        }

        private void OnTriggerStay(Collider other)
        {
            if (!_trackedRenderers.TryGetValue(other, out Renderer renderer) || renderer == null)
                return;

            _propertyBlock ??= new MaterialPropertyBlock();
            float alpha = scaleCurve.Evaluate(GeometryUtils.DistanceFromPointToLine(other.transform.position, lineData) / capsuleRadius);
            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(AlphaId, alpha);
            renderer.SetPropertyBlock(_propertyBlock);
        }

        void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent(out Prism prism))
                prism.SetTransparency(false);
            _trackedRenderers.Remove(other);
        }
    }
}