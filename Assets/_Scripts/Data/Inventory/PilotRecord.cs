using System;
using System.Collections.Generic;

namespace CosmicShore.Data
{
    /// <summary>
    /// ONE pilot: a unique person in one player's crew, layered on a callsign (Docs/Pilots/INVENTORY.md §3;
    /// Docs/Voyages/VOYAGES.md §6.2.1). Garrett, 2026-11-07: "pilot callsigns are limited to corresponding vision powers.
    /// but each pilot is still a unique person that is a part of one player's crew. they have unique backstories, are
    /// part of a faction, have a fun animal combo (human, species 1, species 2)."
    ///
    /// <para>The CALLSIGN (<see cref="CallsignId"/>, one of the fixed eighteen) is the vision power and is shared with
    /// other crews; everything else here is this person's alone. A crew holds at most one pilot per callsign
    /// (<see cref="AccountInventory.TryAddPilot"/>).</para>
    ///
    /// <para>A snapshot like <see cref="ArtifactRecord"/>: the generator's rolls are stored, and the backstory grows by
    /// appending to <see cref="Chronicle"/> (VOYAGES.md §2: "a backstory that grows with key events").</para>
    /// </summary>
    [Serializable]
    public class PilotRecord
    {
        /// <summary>Most chronicle lines kept; the oldest go first.</summary>
        public const int MaxChronicle = 24;

        /// <summary><c>pilot.callsign.&lt;word&gt;#&lt;seed hex&gt;</c> (<see cref="UniqueId"/>). What squad slots save.</summary>
        public string Id;
        /// <summary>The callsign (vision power) this person carries: <c>pilot.callsign.volt</c>.</summary>
        public string CallsignId;
        public int GeneratorVersion;

        public string GivenName;
        public string FamilyName;

        /// <summary>The two animals this human is part of (never the same one twice).</summary>
        public AnimalSpecies SpeciesOne;
        public AnimalSpecies SpeciesTwo;

        public FactionType FactionType;
        /// <summary><c>faction.pirate.black-tide</c> (<see cref="FactionCatalog"/>); a backend faction id later.</summary>
        public string FactionId;
        public string FactionName;

        public string Homeworld;
        public string Backstory;
        /// <summary>Key events, oldest first, appended as they happen (recruited, promoted, a voyage's end...).</summary>
        public List<string> Chronicle = new();

        /// <summary>Where they came from: starter, recruit, emissary, legacy, dev.</summary>
        public string Origin;
        public long MintedAtMs;

        /// <summary>"Given Family".</summary>
        public string DisplayName() => string.IsNullOrEmpty(FamilyName) ? GivenName ?? string.Empty : GivenName + " " + FamilyName;

        /// <summary>The vision power, from the callsign id (None when the id is not a callsign).</summary>
        public PilotVision Vision() => Callsigns.TryParse(CallsignOrTemplate(), out var v) ? v : PilotVision.None;

        /// <summary>The callsign word players read on a plate: VOLT.</summary>
        public string CallsignWord() => Callsigns.WordOf(Vision());

        /// <summary>"Human / Axolotl / Heron".</summary>
        public string SpeciesCombo() => Species.Combo(SpeciesOne, SpeciesTwo);

        /// <summary>The fun one-word blend of the two animals ("Axoleron").</summary>
        public string SpeciesBlend() => Species.Blend(SpeciesOne, SpeciesTwo);

        /// <summary>The callsign id, robust to a record whose <see cref="CallsignId"/> is blank (read from the id).</summary>
        public string CallsignOrTemplate() => string.IsNullOrEmpty(CallsignId) ? UniqueId.TemplateOf(Id) : CallsignId;

        /// <summary>Appends a key event to the story, dropping the oldest past <see cref="MaxChronicle"/>.</summary>
        public void AddChronicle(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            Chronicle ??= new List<string>();
            Chronicle.Add(PilotGenerator.Printable(line.Trim()));
            if (Chronicle.Count > MaxChronicle) Chronicle.RemoveRange(0, Chronicle.Count - MaxChronicle);
        }

        public PilotRecord Clone()
        {
            var c = (PilotRecord)MemberwiseClone();
            c.Chronicle = Chronicle == null ? new List<string>() : new List<string>(Chronicle);
            return c;
        }
    }
}
