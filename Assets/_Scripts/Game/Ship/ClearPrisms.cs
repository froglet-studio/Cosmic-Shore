using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Game;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore
{
    public class ClearPrisms : MonoBehaviour
    {
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

        bool isInitialized;

        // Cached to avoid per-frame allocations in OnTriggerStay
        private static readonly int AlphaPropertyID = Shader.PropertyToID("_Alpha");
        private readonly MaterialPropertyBlock _mpb = new();

        // Cache Renderer lookups — OnTriggerStay fires hundreds of times per frame
        private readonly Dictionary<Collider, Renderer> _rendererCache = new(128);


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

        private void OnBeforeVesselDestroyed() => isInitialized = false; // Destroy(gameObject);


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
            if (other.TryGetComponent<Prism>(out var prism))
                prism.SetTransparency(true);
        }

        private void OnTriggerStay(Collider other)
        {
            if (!_rendererCache.TryGetValue(other, out var renderer))
            {
                if (!other.TryGetComponent(out renderer)) return;
                _rendererCache[other] = renderer;
            }
            float alpha = scaleCurve.Evaluate(GeometryUtils.DistanceFromPointToLine(other.transform.position, lineData) / capsuleRadius);
            renderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(AlphaPropertyID, alpha);
            renderer.SetPropertyBlock(_mpb);
        }

        void OnTriggerExit(Collider other)
        {
            _rendererCache.Remove(other);
            if (other.TryGetComponent<Prism>(out var prism))
                prism.SetTransparency(false);
        }
    }
}