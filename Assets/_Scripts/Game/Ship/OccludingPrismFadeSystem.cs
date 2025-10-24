using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Core;
using CosmicShore.Game;
using UnityEngine.Serialization;

namespace CosmicShore
{
    
    /// <summary>
    /// This component gradually fades prisms when are located right between the player camera and the ship.
    /// It bases its fading factor on the line that goes between the camera and the ship. The closer a
    /// prism is to that line, the more it will be faded.
    /// </summary>
    public class ClearPrisms : MonoBehaviour
    {
        private static readonly int Alpha = Shader.PropertyToID("_Alpha");
        private Transform mainCamera;

        public VesselController shipPrefab;

        private IVessel Vessel => shipPrefab as IVessel;

        [SerializeField] AnimationCurve scaleCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [SerializeField] private float capsuleRadius = 5f;

        private CapsuleCollider visibilityCapsule;

        private Vector3 capsuleDirection;

        private CameraManager cameraManager;
        private GeometryUtils.LineData lineData;
        
        private HashSet<Prism> currentlyFadingPrisms = new();

        private bool isInitialized;

        private void OnEnable()
        {
            if (Vessel == null)
            {
                Debug.LogError("Vessel instance is not set or does not implement IVessel interface.");
                enabled = false;
                return;
            }

            Vessel.OnInitialized += AttachCapsuleCollider;
            Vessel.OnInitialized += TestingInit;
            Vessel.OnBeforeDestroyed += OnBeforeVesselDestroyed;
        }

        void TestingInit()
        {
            Debug.Log("<color=\"magenta\">Testing initialization");
        }
        
        private void OnDisable()
        {
            Vessel.OnInitialized -= AttachCapsuleCollider;
            Vessel.OnBeforeDestroyed -= OnBeforeVesselDestroyed;
        }

        private void OnBeforeVesselDestroyed() => isInitialized = false; // Destroy(gameObject);


        /// <summary>
        /// Attaches the capsule dynamically, so that this component can be added to a prefab without the need to
        /// add the collider as a mandatory additional step.
        /// </summary>
        private void AttachCapsuleCollider()
        {
            // TODO: Shouldn't that be an or rather than an and?
            if (!Vessel.IsOwnerClient && Vessel.VesselStatus.IsInitializedAsAI) 
                return;
            
            cameraManager = CameraManager.Instance;
            mainCamera = cameraManager.GetCloseCamera();
            if (mainCamera == null)
            {
                Debug.LogError("Close main camera not found! This should not happen!");
                return;
            }

            StartCoroutine(CreateCapsule());
            
            isInitialized = true;
        }

        IEnumerator CreateCapsule()
        {
            yield return new WaitForSeconds(2f);
            var cameraPosition = mainCamera.position;
            var shipPosition = Vessel.Transform.position;

            // Scale the capsule to fit between the camera and vessel
            var distance = Mathf.Abs(Vector3.Distance(cameraPosition, shipPosition));
            visibilityCapsule = gameObject.AddComponent<CapsuleCollider>();
            // if (playerCamera == null) return;
            visibilityCapsule.direction = 2; // Z-axis
            visibilityCapsule.height = distance;
            visibilityCapsule.isTrigger = true;
            visibilityCapsule.radius = capsuleRadius;
            visibilityCapsule.center = Vector3.back * distance / 2;
            Debug.Log($"Distance: {distance}");
            Debug.Log($"Capsule height: {visibilityCapsule.height}");
            Debug.Log($"Visibility capsule center:  {visibilityCapsule.center}");
        }

        void Update()
        {
            if (!isInitialized)
                return;
            
            // Update the capsule's end positions
            // lineData = GeometryUtils.PrecomputeLineData(cameraPosition, shipPosition);
        }

        private void OnTriggerEnter(Collider other)
        {
            Debug.Log($"OnTriggerEnter:  {other.gameObject.name} :: {other.gameObject.GetInstanceID()}");
            var prism = other.GetComponent<Prism>();
            if (prism == null) return;
            // currentlyFadingPrisms.Add(prism);
        }

        private void OnTriggerStay(Collider other)
        {
            return;
            var renderer = other.GetComponent<Renderer>();
            if (renderer != null)
            {
                var magnitude =
                    GeometryUtils.DistanceFromPointToLine(other.transform.position, lineData) / capsuleRadius;
                renderer.material.SetFloat(Alpha, scaleCurve.Evaluate(magnitude));
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var prism = other.GetComponent<Prism>();
            if (prism == null) return;
            currentlyFadingPrisms.Remove(prism);
            prism.SetTransparency(false);
        }
    }
}
