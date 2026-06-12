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
        private void Update()
        {
            if (vesselStatus.IsDrifting)
            {
                transform.rotation = Quaternion.Lerp(transform.rotation,Quaternion.LookRotation(vesselStatus.Course, transform.parent.forward),.06f);
            }
            else
            {
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(flip ? -transform.parent.right : transform.parent.right, transform.parent.forward), .06f);
            }
        }
    }
}
