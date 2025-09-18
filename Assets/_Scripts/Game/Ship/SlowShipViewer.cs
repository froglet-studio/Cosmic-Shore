using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.SOAP;
using UnityEngine;

namespace CosmicShore
{
    [RequireComponent(typeof(IVesselStatus))]
    public class SlowShipViewer : MonoBehaviour
    {
        [SerializeField]
        MiniGameDataSO miniGameData;
        
        [SerializeField] Material trailViewerMaterial;

        LineRenderer lineRenderer;
        IVesselStatus vesselStatus;
        Transform target;
        
        void Start()
        {
            vesselStatus = GetComponent<IVesselStatus>();  
            lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.material = trailViewerMaterial;
            lineRenderer.startWidth = lineRenderer.endWidth = 0.1f;
        }

        void Update()
        {
            target = null;
            lineRenderer.SetPosition(0, transform.position);
            lineRenderer.enabled = false;

            // TODO - Can't have ActivePlayer as static
            // if (Hangar.Instance.SlowedShipTransforms.Count > 0 && Player.ActivePlayer && Player.ActivePlayer.Vessel.VesselStatus == vesselStatus)
            {
                var distance = float.PositiveInfinity;
                foreach (var shipTransform in miniGameData.SlowedShipTransforms)
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