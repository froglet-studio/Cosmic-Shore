using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Every pilot and artifact the game offers, and the one place a squad slot asks "what can this
    /// vessel take?" (Docs/PILOTS_AND_ARTIFACTS.md §4, §6).
    ///
    /// <para>The pilot and artifact pickers and the match-start resolve all go through this asset,
    /// so they agree by construction: a slot is only ever offered what <see cref="ResolvePilot"/>
    /// and <see cref="ResolveArtifact"/> will later accept. Lookups are quiet probes that report a
    /// miss by returning false; nothing here logs, because a stale id in a saved squad is an expected
    /// state that falls back to the default pilot or to no artifact, not a fault.</para>
    ///
    /// <para>Small, menu-time lists: linear scans are deliberate. Do not call these per frame.</para>
    ///
    /// <para><b>2026-10-27 (Garrett):</b> no default "base kit" pilot (a pilot is a pair of eyes, not part of the kit:
    /// an empty seat has no sight), and one artifact slot per vessel (a squad of three risks up to three artifacts,
    /// Docs/Pilots/ARTIFACTS.md §5).</para>
    /// </summary>
    [CreateAssetMenu(fileName = "PilotArtifactCatalog", menuName = "ScriptableObjects/Pilots/Pilot and Artifact Catalog")]
    public class PilotArtifactCatalogSO : ScriptableObject
    {
        [Tooltip("Every pilot. Pickers list them in this order.")]
        [SerializeField] List<PilotSO> pilots = new();

        [Tooltip("Every artifact. Pickers list them in this order.")]
        [SerializeField] List<ArtifactSO> artifacts = new();

        [Tooltip("Artifact slots per vessel: one, the squad slot's artifact card (Garrett, 2026-10-27). The Hangar draws up to four.")]
        [SerializeField, Min(0)] int slotsPerVessel = 1;

        public IReadOnlyList<PilotSO> Pilots => pilots;
        public IReadOnlyList<ArtifactSO> Artifacts => artifacts;
        public int SlotsPerVessel => slotsPerVessel;

        /// <summary>Where the one shipped catalog lives under a Resources folder (author_pilot_assets.py writes it),
        /// so a vessel finds it with no prefab wiring - the ElementalAbilityMapSO.LoadFor precedent.</summary>
        public const string ResourcePath = "Pilots/PilotArtifactCatalog";

        static PilotArtifactCatalogSO _loaded;

        /// <summary>The shipped catalog, loaded once; null if it is missing from the build.</summary>
        public static PilotArtifactCatalogSO Load()
        {
            if (!_loaded) _loaded = Resources.Load<PilotArtifactCatalogSO>(ResourcePath);
            return _loaded;
        }

        /// <summary>The pilot saved under <paramref name="id"/>. False for an empty or unknown id.</summary>
        public bool TryGetPilot(string id, out PilotSO pilot)
        {
            pilot = null;
            if (string.IsNullOrEmpty(id) || pilots == null) return false;

            foreach (var candidate in pilots)
            {
                if (!candidate || candidate.Id != id) continue;
                pilot = candidate;
                return true;
            }
            return false;
        }

        /// <summary>The artifact saved under <paramref name="id"/>. False for an empty or unknown id.</summary>
        public bool TryGetArtifact(string id, out ArtifactSO artifact)
        {
            artifact = null;
            if (string.IsNullOrEmpty(id) || artifacts == null) return false;

            foreach (var candidate in artifacts)
            {
                if (!candidate || candidate.Id != id) continue;
                artifact = candidate;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Fills <paramref name="into"/> (cleared first) with every pilot a <paramref name="vesselClass"/>
        /// hull declaring <paramref name="keywords"/> can fly, in authored order. Empty for a hull that is not
        /// concrete (Random).
        /// </summary>
        public void GetPilotsFor(VesselClassType vesselClass, IReadOnlyList<VesselKeyword> keywords, List<PilotSO> into)
        {
            into.Clear();
            if (pilots == null) return;
            foreach (var pilot in pilots)
                if (pilot && IsOffered(pilot, vesselClass, keywords)) into.Add(pilot);
        }

        /// <inheritdoc cref="GetPilotsFor(VesselClassType, IReadOnlyList{VesselKeyword}, List{PilotSO})"/>
        public void GetPilotsFor(SO_Vessel vessel, List<PilotSO> into)
        {
            if (!vessel) { into.Clear(); return; }
            GetPilotsFor(vessel.Class, vessel.Keywords, into);
        }

        /// <summary>
        /// Fills <paramref name="into"/> (cleared first) with every artifact a <paramref name="vesselClass"/>
        /// hull declaring <paramref name="keywords"/> can carry, in authored order.
        /// </summary>
        public void GetArtifactsFor(VesselClassType vesselClass, IReadOnlyList<VesselKeyword> keywords, List<ArtifactSO> into)
        {
            into.Clear();
            if (artifacts == null) return;

            foreach (var artifact in artifacts)
            {
                if (!artifact || artifact.Requirement == null) continue;
                if (artifact.Requirement.IsMetBy(vesselClass, keywords)) into.Add(artifact);
            }
        }

        /// <inheritdoc cref="GetArtifactsFor(VesselClassType, IReadOnlyList{VesselKeyword}, List{ArtifactSO})"/>
        public void GetArtifactsFor(SO_Vessel vessel, List<ArtifactSO> into)
        {
            if (!vessel) { into.Clear(); return; }
            GetArtifactsFor(vessel.Class, vessel.Keywords, into);
        }

        /// <summary>
        /// The pilot <paramref name="slot"/> flies: its chosen pilot when that pilot still exists and can still fly
        /// the slot's vessel, otherwise the first pilot the vessel is offered (every vessel needs a pilot, Garrett
        /// 2026-10-27). Null only when the catalog offers the vessel none.
        /// </summary>
        public PilotSO ResolvePilot(SquadSlot slot, IReadOnlyList<VesselKeyword> keywords) =>
            !slot.UsesDefaultPilot() && TryGetPilot(slot.PilotId, out var chosen) && IsOffered(chosen, slot.Vessel, keywords)
                ? chosen : FirstPilotFor(slot.Vessel, keywords);

        /// <summary>The first pilot, in authored order, a <paramref name="vesselClass"/> hull can fly; null when none.</summary>
        public PilotSO FirstPilotFor(VesselClassType vesselClass, IReadOnlyList<VesselKeyword> keywords)
        {
            if (pilots == null) return null;
            foreach (var pilot in pilots)
                if (pilot && IsOffered(pilot, vesselClass, keywords)) return pilot;
            return null;
        }

        /// <summary>
        /// The artifact <paramref name="slot"/> carries: its chosen artifact when that artifact still
        /// exists and the slot's vessel can still carry it, otherwise none.
        /// </summary>
        public ArtifactSO ResolveArtifact(SquadSlot slot, IReadOnlyList<VesselKeyword> keywords)
        {
            if (!slot.CarriesArtifact() || !TryGetArtifact(slot.ArtifactId, out var artifact)) return null;
            return artifact.Requirement != null && artifact.Requirement.IsMetBy(slot.Vessel, keywords) ? artifact : null;
        }

        /// <summary>
        /// Appends one line to <paramref name="problems"/> per authoring error: empty or missing entries, empty or
        /// duplicate ids, requirements naming Random or None, display names the UI font cannot draw, a pilot with no
        /// sight, an artifact bound to a vessel class (none may be, ARTIFACTS.md §3), an artifact with no payload
        /// transformation, and track sets that are not one each of Mass, Space, Time and Charge. An empty list means
        /// the catalog is consistent.
        /// </summary>
        public void Validate(List<string> problems)
        {
            var pilotIds = new HashSet<string>();
            for (int i = 0; i < (pilots?.Count ?? 0); i++)
            {
                var pilot = pilots[i];
                string where = $"pilots[{i}]";
                if (!pilot) { problems.Add($"{where} is empty."); continue; }
                where = $"Pilot '{pilot.name}'";

                CheckId(pilot.Id, where, pilotIds, problems);
                CheckDisplayName(pilot.DisplayName, where, problems);
                CheckRequirement(pilot.Requirement, where, problems);
                if (pilot.Vision == PilotVision.None) problems.Add($"{where} carries no sight; every pilot is a pair of eyes.");
            }

            var artifactIds = new HashSet<string>();
            for (int i = 0; i < (artifacts?.Count ?? 0); i++)
            {
                var artifact = artifacts[i];
                string where = $"artifacts[{i}]";
                if (!artifact) { problems.Add($"{where} is empty."); continue; }
                where = $"Artifact '{artifact.name}'";

                CheckId(artifact.Id, where, artifactIds, problems);
                CheckDisplayName(artifact.DisplayName, where, problems);
                CheckRequirement(artifact.Requirement, where, problems);
                if (artifact.Requirement != null && artifact.Requirement.IsBoundToClass())
                    problems.Add($"{where} is bound to {artifact.Requirement.Vessel}; no vessel class appears in an artifact.");
                if (!artifact.Transform) problems.Add($"{where} has no payload transformation.");

                var seen = new HashSet<Element>();
                foreach (var track in artifact.Tracks)
                {
                    if (track == null) { problems.Add($"{where} has an empty track."); continue; }
                    if (track.element is not (Element.Mass or Element.Space or Element.Time or Element.Charge))
                        problems.Add($"{where} has a {track.element} track; tracks are Mass, Space, Time and Charge.");
                    else if (!seen.Add(track.element)) problems.Add($"{where} has two {track.element} tracks.");
                    CheckDisplayName(track.label, $"{where} {track.element} track label", problems);
                    CheckDisplayName(track.twistLine, $"{where} {track.element} twist", problems);
                }
                if (seen.Count != 4) problems.Add($"{where} has {seen.Count} element tracks, not 4.");
            }
        }

        static bool IsOffered(PilotSO pilot, VesselClassType vesselClass, IReadOnlyList<VesselKeyword> keywords) =>
            pilot.Requirement != null && pilot.Requirement.IsMetBy(vesselClass, keywords);

        static void CheckId(string id, string where, HashSet<string> seen, List<string> problems)
        {
            if (string.IsNullOrWhiteSpace(id)) problems.Add($"{where} has no id.");
            else if (!seen.Add(id)) problems.Add($"{where} reuses the id '{id}'.");
        }

        static void CheckDisplayName(string displayName, string where, List<string> problems)
        {
            if (string.IsNullOrWhiteSpace(displayName)) { problems.Add($"{where} has no display name."); return; }

            foreach (char c in displayName)
            {
                if (c >= ' ' && c <= '~') continue;
                problems.Add($"{where} display name contains '{c}' (U+{(int)c:X4}), which the UI font cannot draw.");
                return;
            }
        }

        static void CheckRequirement(VesselRequirement requirement, string where, List<string> problems)
        {
            if (requirement == null) { problems.Add($"{where} has no requirement."); return; }

            if (requirement.Vessel == VesselClassType.Random)
                problems.Add($"{where} is bound to Random, which no hull is; use Any for every hull.");

            if (requirement.Keywords == null) return;
            foreach (var keyword in requirement.Keywords)
            {
                if (keyword != VesselKeyword.None) continue;
                problems.Add($"{where} lists the keyword None; remove it.");
                return;
            }
        }
    }
}
