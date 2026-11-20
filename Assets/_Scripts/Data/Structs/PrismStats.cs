using System;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    // Lives in the CosmicShore.Data ASSEMBLY (a dependency-free payload) but keeps its
    // CosmicShore.Gameplay namespace, so no consumer changes. Docs/ASSEMBLY_SPLIT.md, Phase 2 step 1.
    [Serializable]
    public struct PrismStats
    {
        public string OwnName;
        public float Volume;
        public string AttackerName;

        /// <summary>
        /// The DOMAIN the destroyed prism was wearing. Needed because ENVIRONMENT mass (flora,
        /// fauna bodies, laid cell structure) has no roster entry, so the owner-name comparison
        /// that classifies a trail says nothing about it - without this, every prism in the world
        /// counted as hostile to every player including the one whose own colour it wore.
        /// <see cref="Domains.Blue"/> is the neutral sentinel and stays hostile to everyone.
        /// </summary>
        public Domains OwnDomain;

        /// <summary>
        /// True when DIRECT GUNFIRE destroyed this prism - a round that struck the prism itself.
        /// False for a blast, a ram, a creature, a sword or an arena teardown.
        ///
        /// <para>It rides the channel rather than being derived at the listener because the
        /// listener cannot see WHAT killed a prism from anywhere else: <c>Prism</c> raises this
        /// event from ONE place on every route, and the weapon identity is only known at the
        /// call that damaged it. <c>VesselRearmOnPrismDestruction</c> is the reader - the
        /// Sparrow's tank fills from its guns and from nothing its rockets do.</para>
        /// </summary>
        public bool DestroyedByGunfire;

        /// <summary>
        /// The shield's share of the prism's volume (Garrett, 2026-11-18: "the extra volume of shielding gets added to
        /// creation"). On a destroyed or stolen prism it is the shell volume the prism carried on top of its box; on a
        /// volume-modified event it is the change in that shell volume, with <see cref="Volume"/> 0. Read only by
        /// <c>VolumeLedger</c>; every legacy stat keeps reading <see cref="Volume"/>, so no mode's scoring moves.
        /// </summary>
        public float ShieldVolume;

        /// <summary>True when the destroyed prism was super-shielded: a specialist's breach (VolumeLedger).</summary>
        public bool WasSuperShielded;
    }
}
