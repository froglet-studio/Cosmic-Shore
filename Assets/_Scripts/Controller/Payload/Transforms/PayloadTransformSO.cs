using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What a payload LOADED with an artifact does when RB releases it (Docs/Pilots/ARTIFACTS.md §3): the artifact's
    /// one transformation of the payload object. A payload with no artifact runs the plain burst instead
    /// (<see cref="PayloadCarrier.Release"/>).
    ///
    /// <para><b>The parcel changes hands.</b> When <see cref="Release"/> returns true it owns the parcel's prisms and
    /// its root from then on, and must spend them (<see cref="PayloadBurst.SpendParcel"/>) when it is done: mass is
    /// conserved, so anything a transform builds comes out of the parcel. Returning false refuses the release (an
    /// Aegis far from its Ark), and the carrier keeps flying with the payload.</para>
    ///
    /// <para>No vessel class appears here, and a transform never reaches a vessel except to move petals through
    /// <c>CombatHitDrain.TryAdmit</c> (PvP is petals only). Every harm to a prism goes through the silence gate
    /// (<see cref="SuperShieldCrack"/>, <see cref="Prism.Damage"/>).</para>
    /// </summary>
    public abstract class PayloadTransformSO : ScriptableObject
    {
        /// <summary>Release the loaded payload. True: the transform now owns the parcel. False: refused.</summary>
        public abstract bool Release(in PayloadLaunch launch);

        /// <summary>The authored line a refused release shows, or empty.</summary>
        public virtual string RefusedLine => string.Empty;
    }

    /// <summary>Everything a transform needs at release.</summary>
    public readonly struct PayloadLaunch
    {
        /// <summary>Where and whose: the release point, the carrier's heading, name and domain.</summary>
        public readonly PayloadRelease Release;
        /// <summary>The parcel's prisms. A transform that takes the release takes these (copy them out).</summary>
        public readonly List<Prism> Parcel;
        /// <summary>The parcel's root, beside the Ark in the hierarchy (never under a vessel).</summary>
        public readonly Transform Root;
        public readonly ArtifactSO Artifact;
        public readonly ArtifactLevels Levels;
        /// <summary>The carrier's squad's hold, or null.</summary>
        public readonly ArkHold Home;

        public PayloadLaunch(in PayloadRelease release, List<Prism> parcel, Transform root, ArtifactSO artifact,
            in ArtifactLevels levels, ArkHold home)
        {
            Release = release;
            Parcel = parcel;
            Root = root;
            Artifact = artifact;
            Levels = levels;
            Home = home;
        }
    }
}
