using CosmicShore.Gameplay;
using UnityEngine;
using CosmicShore.UI;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
namespace CosmicShore.Gameplay
{
    public class DriftJet : MonoBehaviour
    {
        [SerializeField] bool flip = false;

        [SerializeField] VesselStatus vesselStatus;

        /// <summary>The way this jet points when not drifting: its mount bone's right axis (left when
        /// flipped). DriftEngineSwivel turns the mount onto Course along this axis, so pod and plume
        /// line up while drifting.</summary>
        public Vector3 FlightAxis => flip ? -transform.parent.right : transform.parent.right;

        private void Update()
        {
            if (vesselStatus.IsDrifting)
            {
                transform.rotation = Quaternion.Lerp(transform.rotation,Quaternion.LookRotation(vesselStatus.Course, transform.parent.forward),.06f);
            }
            else
            {
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(FlightAxis, transform.parent.forward), .06f);
            }
        }
    }
}
