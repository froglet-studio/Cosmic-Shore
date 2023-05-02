using UnityEngine;

namespace StarWriter.Core
{
    [System.Serializable]
    public class ShipData : MonoBehaviour
    {
        public float Speed;

        public bool Boosting = false;
        public bool BoostCharging = false;
        public bool BoostDecaying = false;
        public bool Drifting = false;
        public bool LayingBulletTrail = false;
        public bool Portrait = false;

        public bool Attached = false;
        public TrailBlock AttachedTrailBlock;

        public bool GunsActive = false;

        public Vector3 Course;
        public Quaternion blockRotation;

        public void Reset()
        {
            Boosting = false;
            BoostCharging = false;
            BoostDecaying = false;
            Drifting = false;
            LayingBulletTrail = false;
            Attached = false;
            AttachedTrailBlock = null;
            GunsActive = false;
            //InputSpeed = 1;
            //SpeedMultiplier = 1;
            Course = transform.forward;
        }
    }
}