using UnityEngine;

namespace StarWriter.Core
{
    [System.Serializable]
    public class ShipData : MonoBehaviour
    {
        [SerializeField] float inputSpeed = 1;
                  public float InputSpeed { set { inputSpeed = Mathf.Max(value, 0); speed = inputSpeed * speedMultiplier;}}
        [SerializeField] float speedMultiplier = 1;
                  public float SpeedMultiplier { set { speedMultiplier = Mathf.Max(value, 0); speed = inputSpeed * speedMultiplier; } } 
        [SerializeField] float speed;
                  public float Speed { get { speed = inputSpeed * speedMultiplier; return speed; } }

        public bool Boosting = false;
        public bool BoostCharging = false;
        public bool BoostDecaying = false;
        public bool Drifting = false;

        public bool Attached = false;
        public TrailBlock AttachedTrailBlock;

        public bool GunsActive = true;

        public Vector3 Course;
        public Quaternion blockRotation;
    }
}