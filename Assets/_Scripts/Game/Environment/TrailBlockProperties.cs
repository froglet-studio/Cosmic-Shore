using UnityEngine;

namespace CosmicShore.Core
{
    [System.Serializable]
    public class TrailBlockProperties
    {
        public Vector3 position;
        public float volume;
        public float speedDebuffAmount; // don't use more than two sig figs, see vessel.DebuffSpeed
        public TrailBlock trailBlock;
        public ushort Index;
        public Trail Trail;
        public bool IsShielded;
        public bool IsSuperShielded;
        public bool IsDangerous; // TODO: change to enum with mutually exclusive values with shielding
        public bool IsTransparent;
        public float TimeCreated;
    }
}