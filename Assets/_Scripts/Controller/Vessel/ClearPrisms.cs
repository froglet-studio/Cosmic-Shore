using System.Collections.Generic;
using CosmicShore.Gameplay;
using UnityEngine;
using CosmicShore.Utility;

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

        readonly Dictionary<Collider, Renderer> _renderers = new Dictionary<Collider, Renderer>();
        MaterialPropertyBlock _block;

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

            _block = new MaterialPropertyBlock();
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
            if (!_renderers.ContainsKey(other))
            {
                if (other.TryGetComponent<Renderer>(out var r))
                    _renderers[other] = r;
            }

            if (other.TryGetComponent<Prism>(out var prism))
                prism.SetTransparency(true);
        }

        private void OnTriggerStay(Collider other)
        {
            if (!_renderers.TryGetValue(other, out var renderer))
            {
                if (!other.TryGetComponent<Renderer>(out renderer)) return;
                _renderers[other] = renderer;
            }

            float alpha = scaleCurve.Evaluate(GeometryUtils.DistanceFromPointToLine(other.transform.position, lineData) / capsuleRadius);
            _block.SetFloat(AlphaId, alpha);
            renderer.SetPropertyBlock(_block);
        }

        void OnTriggerExit(Collider other)
        {
            _renderers.Remove(other);

            if (other.TryGetComponent<Prism>(out var prism))
                prism.SetTransparency(false);
        }
    }
}