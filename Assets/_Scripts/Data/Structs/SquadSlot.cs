using System;

namespace CosmicShore.Data
{
    /// <summary>
    /// One squad mate's four pre-match choices: vessel, element, pilot and artifact
    /// (Docs/PILOTS_AND_ARTIFACTS.md §1). Persisted inside <see cref="SavedSquad"/>.
    ///
    /// <para>Pilot and artifact are stored by their authored id, not by asset reference, because the
    /// record is JSON in cloud save. An empty <see cref="PilotId"/> means the vessel's default pilot;
    /// an empty <see cref="ArtifactId"/> means no artifact is carried. Both are resolved, and checked
    /// against the vessel again, when the squad is read, so a stale id falls back rather than
    /// failing.</para>
    ///
    /// <para>Computed answers are methods, not properties, so Newtonsoft does not write them into
    /// the cloud record.</para>
    /// </summary>
    [Serializable]
    public struct SquadSlot
    {
        public VesselClassType Vessel;
        public Element Element;
        public string PilotId;
        public string ArtifactId;

        public SquadSlot(VesselClassType vessel, Element element, string pilotId = null, string artifactId = null)
        {
            Vessel = vessel;
            Element = element;
            PilotId = pilotId;
            ArtifactId = artifactId;
        }

        /// <summary>True when no pilot was picked, so the vessel's default pilot flies.</summary>
        public bool UsesDefaultPilot() => string.IsNullOrEmpty(PilotId);

        /// <summary>True when an artifact is carried into the match.</summary>
        public bool CarriesArtifact() => !string.IsNullOrEmpty(ArtifactId);

        /// <summary>
        /// This slot on another hull. Pilot and artifact are cleared: a pilot is chosen per vessel,
        /// and the picks made for the old hull say nothing about what the new one can take.
        /// </summary>
        public SquadSlot WithVessel(VesselClassType vessel) => new SquadSlot(vessel, Element);
    }
}
