using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.SOAP;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore
{
    [RequireComponent(typeof(IVesselStatus))]
    public class SlowShipViewer : MonoBehaviour
    {
        [FormerlySerializedAs("miniGameData")] [SerializeField]
        GameDataSO gameData;
        
        [SerializeField] Material trailViewerMaterial;

        LineRenderer lineRenderer;
        Transform target;
        
        void Start()
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.material = trailViewerMaterial;
            lineRenderer.startWidth = lineRenderer.endWidth = 0.1f;
        }

        void Update()
        {
            target = null;
            lineRenderer.SetPosition(0, transform.position);
            lineRenderer.enabled = false;

            // TODO - Can't have LocalPlayer as static
            // if (Hangar.Instance.SlowedShipTransforms.Count > 0 && Player.LocalPlayer && Player.LocalPlayer.Vessel.VesselStatus == vesselStatus)
            {
                var distance = float.PositiveInfinity;
                foreach (var shipTransform in gameData.SlowedShipTransforms)
                {
                    if (shipTransform == transform) continue;
                    float tempDistance;     
                    tempDistance = (shipTransform.position - transform.position).magnitude;
                    if (tempDistance < distance)
                    {
                        distance = tempDistance;
                        target = shipTransform;
                    }
                }
                if (target != null)
                {
                    lineRenderer.SetPosition(1, target.position);
                    lineRenderer.enabled = true;
                }
            }
        }
    }
}