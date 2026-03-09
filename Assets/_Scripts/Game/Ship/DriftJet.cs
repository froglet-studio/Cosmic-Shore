using CosmicShore.Game;
using UnityEngine;

namespace CosmicShore
{
    public class DriftJet : MonoBehaviour
    {
        [SerializeField] bool flip = false;

        [SerializeField] VesselStatus vesselStatus;

        private Transform _parentTransform;

        void Awake()
        {
            _parentTransform = transform.parent;
        }

        private void Update()
        {
            var parentFwd = _parentTransform.forward;
            if (vesselStatus.IsDrifting)
            {
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(vesselStatus.Course, parentFwd), .06f);
            }
            else
            {
                var parentRight = _parentTransform.right;
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(flip ? -parentRight : parentRight, parentFwd), .06f);
            }
        }
    }
}
