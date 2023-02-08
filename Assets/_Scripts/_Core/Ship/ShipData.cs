using UnityEngine;

namespace StarWriter.Core
{
    [System.Serializable]
    public class ShipData : MonoBehaviour
    {
        public float InputSpeed = 1;
        public float SpeedMultiplier = 1;
        public float Speed;

        public bool Boosting = false;
        public bool BoostDecaying = false;
        public bool Drifting = false;

        public Vector3 VelocityDirection;
        public Quaternion blockRotation;

        void Update()
        {
            if (SpeedMultiplier < 0) SpeedMultiplier = 0;
            Speed = InputSpeed * SpeedMultiplier;
        }
    }
}