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
    /// </summary>
    [CreateAssetMenu(fileName = "PilotArtifactCatalog", menuName = "ScriptableObjects/Pilots/Pilot and Artifact Catalog")]
    public class PilotArtifactCatalogSO : ScriptableObject
    {
        [Tooltip("Every pilot, including each vessel's default pilot. Pickers list them in this order, " +
                 "default first.")]
        [SerializeField] List<PilotSO> pilots = new();

        [Tooltip("Every artifact. Pickers list them in this order.")]
        [SerializeField] List<ArtifactSO> artifacts = new();

        public IReadOnlyList<PilotSO> Pilots => pilots;
        public IReadOnlyList<ArtifactSO> Artifacts => artifacts;

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

        /// <summary>The base-kit pilot of <paramref name="vesselClass"/>. False when none is authored yet.</summary>
        public bool TryGetDefaultPilot(VesselClassType vesselClass, out PilotSO pilot)
        {
            pilot = null;
            if (pilots == null) return false;

            foreach (var candidate in pilots)
            {
                if (!candidate || !candidate.IsDefaultFor(vesselClass)) continue;
                pilot = candidate;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Fills <paramref name="into"/> (cleared first) with every pilot a <paramref name="vesselClass"/>
        /// hull declaring <paramref name="keywords"/> can fly: its default pilot first, then the rest
        /// in authored order. Empty for a hull that is not concrete (Random).
        /// </summary>
        public void GetPilotsFor(VesselClassType vesselClass, IReadOnlyList<VesselKeyword> keywords, List<PilotSO> into)
        {
            into.Clear();
            if (pilots == null) return;

            if (TryGetDefaultPilot(vesselClass, out var defaultPilot) && IsOffered(defaultPilot, vesselClass, keywords))
                into.Add(defaultPilot);

            foreach (var pilot in pilots)
            {
                if (!pilot || pilot == defaultPilot) continue;
                if (IsOffered(pilot, vesselClass, keywords)) into.Add(pilot);
            }
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
        /// The pilot <paramref name="slot"/> flies: its chosen pilot when that pilot still exists and
        /// can still fly the slot's vessel, otherwise the vessel's default pilot. Null only when the
        /// vessel is not concrete or has no default pilot authored.
        /// </summary>
        public PilotSO ResolvePilot(SquadSlot slot, IReadOnlyList<VesselKeyword> keywords)
        {
            if (!slot.UsesDefaultPilot()
                && TryGetPilot(slot.PilotId, out var chosen)
                && IsOffered(chosen, slot.Vessel, keywords))
                return chosen;

            return TryGetDefaultPilot(slot.Vessel, out var defaultPilot) ? defaultPilot : null;
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
        /// Appends one line to <paramref name="problems"/> per authoring error: empty or missing
        /// entries, empty or duplicate ids, requirements naming Random or None, default pilots that
        /// are unbound, carry keywords or share a class, boosts into no element, and display names the
        /// UI font cannot draw. An empty list means the catalog is consistent.
        /// </summary>
        public void Validate(List<string> problems)
        {
            var pilotIds = new HashSet<string>();
            var defaultClasses = new HashSet<VesselClassType>();

            for (int i = 0; i < (pilots?.Count ?? 0); i++)
            {
                var pilot = pilots[i];
                string where = $"pilots[{i}]";
                if (!pilot) { problems.Add($"{where} is empty."); continue; }
                where = $"Pilot '{pilot.name}'";

                CheckId(pilot.Id, where, pilotIds, problems);
                CheckDisplayName(pilot.DisplayName, where, problems);
                CheckRequirement(pilot.Requirement, where, problems);

                if (pilot.IsDefault)
                {
                    var requirement = pilot.Requirement;
                    if (requirement == null || !requirement.IsBoundToClass())
                        problems.Add($"{where} is a default pilot but is not bound to a vessel class.");
                    else if (!defaultClasses.Add(requirement.Vessel))
                        problems.Add($"{where} is a second default pilot for {requirement.Vessel}.");

                    if (requirement != null && HasRealKeyword(requirement))
                        problems.Add($"{where} is a default pilot but requires keywords; a vessel's own base kit needs none.");
                }

                var boost = pilot.ElementalBoost;
                if (boost.Petals > 0 && boost.IsNone())
                    problems.Add($"{where} boosts {boost.Petals} petals into {boost.Element}, which is not Charge, Mass, Space or Time.");
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

        static bool HasRealKeyword(VesselRequirement requirement)
        {
            if (requirement.Keywords == null) return false;
            foreach (var keyword in requirement.Keywords)
                if (keyword != VesselKeyword.None) return true;
            return false;
        }
    }
}
