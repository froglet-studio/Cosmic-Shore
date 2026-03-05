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

        // Reusable MaterialPropertyBlock to avoid creating material instances
        private static readonly int AlphaID = Shader.PropertyToID("_Alpha");
        private MaterialPropertyBlock _mpb;

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

            // Kinematic Rigidbody so PhysX treats this as a dynamic object.
            // Without it, moving the capsule every frame forces a full static-tree rebuild.
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            visibilityCapsule = gameObject.AddComponent<CapsuleCollider>();
            visibilityCapsule.isTrigger = true;
            visibilityCapsule.radius = capsuleRadius;

            _mpb = new MaterialPropertyBlock();

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
            Prism prism = other.GetComponent<Prism>();
            if (prism != null)
                prism.SetTransparency(true);
        }

        private void OnTriggerStay(Collider other)
        {
            if (!other.TryGetComponent<Renderer>(out var renderer)) return;

            // Use MaterialPropertyBlock to set alpha without creating per-prism material instances.
            // renderer.material creates a copy, breaking static batching and leaking materials.
            renderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(AlphaID, scaleCurve.Evaluate(
                GeometryUtils.DistanceFromPointToLine(other.transform.position, lineData) / capsuleRadius));
            renderer.SetPropertyBlock(_mpb);
        }

        void OnTriggerExit(Collider other)
        {
            Prism prism = other.GetComponent<Prism>();
            if (prism != null)
                prism.SetTransparency(false);
        }
    }
}
