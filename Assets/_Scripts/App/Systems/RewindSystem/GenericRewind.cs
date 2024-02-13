using UnityEngine;

namespace CosmicShore.App.Systems.RewindSystem
{
    public class GenericRewind : RewindBase
    {
        [Tooltip("Tracking active state of the object that this script is attached to")]
        [SerializeField] private bool trackObjectActiveState;
        [Tooltip("Tracking Position,Rotation and Scale")]
        [SerializeField] private bool trackTransform;

        [Tooltip("Enable checkbox on right side to track particles")]
        // [SerializeField] OptionalParticleSettings trackParticles;

        public override void Rewind(float seconds)
        {
            if (trackObjectActiveState)
                RestoreObjectActiveState(seconds);
            if (trackTransform)
                RestoreTransform(seconds);
        }

        public override void Track()
        {
            if (trackObjectActiveState)
                TrackObjectActiveState();
            if (trackTransform)
                TrackTransform();
  
        }
    }
}