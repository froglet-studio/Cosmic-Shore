using UnityEngine;

namespace CosmicShore.Core
{
    [System.Serializable]
    public struct TrailBlockProperties
    {
        public Vector3 position;
        public float volume;
        public float speedDebuffAmount; // don't use more than two sig figs, see ship.DebuffSpeed
        public TrailBlock trailBlock;
        public int Index;
        public Trail Trail;
        public bool IsShielded;
        public bool IsSuperShielded;
        public bool IsDangerous; // TODO: change to enum with mutually exclusive values with shielding
        public float TimeCreated;
    }
}