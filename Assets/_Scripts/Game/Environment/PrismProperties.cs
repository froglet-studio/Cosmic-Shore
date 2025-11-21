using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Core
{
    [System.Serializable]
    public class PrismProperties
    {
        public Vector3 position;
        public float volume;
        public float speedDebuffAmount; // don't use more than two sig figs, see vessel.DebuffSpeed
        [FormerlySerializedAs("trailBlock")] public Prism prism;
        public ushort Index;
        public Trail Trail;
        public bool IsShielded;
        public bool IsSuperShielded;
        public bool IsDangerous; // TODO: change to enum with mutually exclusive values with shielding
        public bool IsTransparent;
        public float TimeCreated;
        public string DefaultLayerName = "TrailBlocks";
    }
}