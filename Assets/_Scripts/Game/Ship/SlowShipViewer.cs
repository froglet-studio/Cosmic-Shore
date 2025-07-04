using CosmicShore.Core;
using CosmicShore.Game;
using UnityEngine;

namespace CosmicShore
{
    [RequireComponent(typeof(IShipStatus))]
    public class SlowShipViewer : MonoBehaviour
    {
        [SerializeField] Material trailViewerMaterial;

        LineRenderer lineRenderer;
        IShipStatus shipStatus;
        Transform target;
        
        void Start()
        {
            shipStatus = GetComponent<IShipStatus>();  
            lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.material = trailViewerMaterial;
            lineRenderer.startWidth = lineRenderer.endWidth = 0.1f;
        }

        void Update()
        {
            target = null;
            lineRenderer.SetPosition(0, transform.position);
            lineRenderer.enabled = false;
            if (Hangar.Instance.SlowedShipTransforms.Count > 0 && Player.ActivePlayer && Player.ActivePlayer.Ship.ShipStatus == shipStatus)
            {
                var distance = float.PositiveInfinity;
                foreach (var shipTransform in Hangar.Instance.SlowedShipTransforms)
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