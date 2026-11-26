using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Hangar's words for a vessel's ROLE and ARCHETYPES (Shombith, 2026-11-26; Docs/Vessels/ARCHETYPES.md): the
    /// kicker over the vessel's name reads "DESTROYER · SAPPER / HARRASER / BUILDER · OWNED" where it read "VESSEL ·
    /// OWNED". The facts come from the fleet's one table (<see cref="VesselArchetypesSO"/>); every word is an authored
    /// template or the table's authored names, so a person changes or blanks any of it in the Inspector.
    /// </summary>
    public static class HangarArchetypeWords
    {
        /// <summary>
        /// The kicker, upper-cased. <paramref name="template"/> takes {role}, {archetypes} and {state};
        /// <paramref name="roleLessTemplate"/> takes {state} and is used when the table gives the vessel no role (or the
        /// table is missing).
        /// </summary>
        public static string Kicker(VesselArchetypesSO table, VesselClassType vessel, string template, string roleLessTemplate,
            string separator, string state)
        {
            var role = table ? table.RoleOf(vessel) : VesselRole.None;
            string roleName = role != VesselRole.None ? table.RoleName(role) : string.Empty;
            if (string.IsNullOrEmpty(roleName))
                return TemplateText.Fill(roleLessTemplate, "state", state).ToUpperInvariant();
            return TemplateText.Fill(template, "role", roleName, "archetypes", Archetypes(table, vessel, separator),
                "state", state).ToUpperInvariant();
        }

        /// <summary>The vessel's archetype names, primary first, joined by <paramref name="separator"/>; an archetype whose
        /// name is blank is left out.</summary>
        public static string Archetypes(VesselArchetypesSO table, VesselClassType vessel, string separator)
        {
            if (!table) return string.Empty;
            var names = new List<string>(3);
            foreach (var a in table.ArchetypesOf(vessel))
            {
                string name = table.ArchetypeName(a);
                if (!string.IsNullOrEmpty(name)) names.Add(name);
            }
            return string.Join(separator ?? string.Empty, names);
        }
    }
}
