using UnityEngine;
using UnityEngine.Serialization;
using CosmicShore.Gameplay;
using System;
namespace CosmicShore.Gameplay
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
        /// <summary>EXPLOSIVE (the Manta's Space skim, MANTA_STING_KABLOOM.md §3a): when this prism is destroyed, the
        /// prisms adjacent to it in its prismscape are destroyed too, a beat later - and an explosive neighbour carries
        /// the chain on (<see cref="PrismExplosiveChain"/>). Runtime only, never a spawner request: cleared on every
        /// (re)initialise and spent by the death that fires it.</summary>
        [NonSerialized] public bool IsExplosive;
        /// <summary>The burn-rule weight of a contact with this prism while it is dangerous (a bite 1; a drain - a
        /// mobber's peck - 0.25; Docs/SUBSTRATE_FAUNA.md §9). Runtime only: reset to 1 whenever the prism is
        /// (re)initialised, set by the fauna that owns the prism.</summary>
        [NonSerialized] public float DangerWeight = 1f;
        public float TimeCreated;
        public string DefaultLayerName = "TrailBlocks";
    }
}