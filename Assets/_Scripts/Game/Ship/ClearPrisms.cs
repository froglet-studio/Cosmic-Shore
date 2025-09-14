using UnityEngine;
using CosmicShore.Core;
using CosmicShore.Game;

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


        private void OnEnable()
        {
            if (Vessel == null)
            {
                Debug.LogError("Vessel instance is not set or does not implement IVessel interface.");
                enabled = false;
                return;
            }

            Vessel.OnShipInitialized += VesselInitialized;
        }

        private void OnDisable()
        {
            Vessel.OnShipInitialized -= VesselInitialized;
        }

        private void VesselInitialized(IVesselStatus _)
        {
            if (Vessel.VesselStatus.AutoPilotEnabled) return;
            cameraManager = CameraManager.Instance;
            mainCamera = cameraManager.GetCloseCamera();
            visibilityCapsuleTransform = new GameObject("Visibility Capsule").transform;
            transform.SetParent(visibilityCapsuleTransform);
            visibilityCapsule = gameObject.AddComponent<CapsuleCollider>();
            visibilityCapsule.isTrigger = true;
            visibilityCapsule.radius = capsuleRadius;
        }

        void Update()
        {
            // NOT GOOD WAY TO DO THIS. STOP RUNNING UPDATE BEFORE CHANGING FROM MAIN_MENU TO MULTIPLAYER FREESTYLE SCENE
            if (!mainCamera || 
                Vessel == null || 
                Vessel.VesselStatus == null ||
                !Vessel.VesselStatus.AIPilot ||
                Vessel.VesselStatus.AutoPilotEnabled) return;

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
            TrailBlock trailBlock = other.GetComponent<TrailBlock>();
            if (trailBlock != null)
            {
                trailBlock.SetTransparency(true);
            }
        }

        private void OnTriggerStay(Collider other)
        {
            Renderer renderer = other.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.SetFloat("_Alpha", scaleCurve.Evaluate(GeometryUtils.DistanceFromPointToLine(other.transform.position, lineData)/ capsuleRadius));
            }
        }

        void OnTriggerExit(Collider other)
        {
            TrailBlock trailBlock = other.GetComponent<TrailBlock>();
            if (trailBlock != null)
            {
                trailBlock.SetTransparency(false);
            }
        }
    }
}
