using UnityEngine;

namespace CosmicShore.Core
{
    [System.Serializable]
    public class ShipStatus : MonoBehaviour
    {
        public float Speed;

        public bool Boosting = false;
        public bool ChargedBoostDischarging = false;
        public bool Drifting = false;
        public bool Turret = false;
        public bool Portrait = false;
        public bool SingleStickControls = false;
        public bool CommandStickControls = false;
        public bool LiveProjectiles = false;
        public bool Stationary = false;
        public bool ElevatedResourceGain = false;
        public float ChargedBoostCharge = 1f; // TODO: move to resource system
        public bool AutoPilotEnabled = false;
        public bool AlignmentEnabled = false;
        public bool Slowed = false;
        public bool Overheating = false;

        public bool Attached = false;
        public TrailBlock AttachedTrailBlock;

        public bool GunsActive = false;

        public Vector3 Course;
        public Quaternion blockRotation;

        public void Reset()
        {
            Boosting = false;
            ChargedBoostDischarging = false;
            Drifting = false;
            Attached = false;
            AttachedTrailBlock = null;
            GunsActive = false;
            Course = transform.forward;
            ChargedBoostCharge = 1f;
            Slowed = false;
            Overheating = false;
        }
    }
}