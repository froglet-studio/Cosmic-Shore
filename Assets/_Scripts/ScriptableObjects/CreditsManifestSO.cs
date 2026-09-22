using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The credits screen's content, as authored DATA rather than string literals in code.
    ///
    /// <para>There is exactly one asset, at <c>Assets/Resources/CreditsManifest.asset</c>, so the
    /// runtime loads it with no per-scene wiring — the same shape as <c>ControlGlyphSet</c>,
    /// <c>SpeedTunnelConfig</c> and every other fleet-wide config in that folder. Adding the next
    /// attribution is then an asset edit, which is the whole point: the obligations in
    /// <c>Docs/THIRD_PARTY_REGISTER.md</c> §7 are outstanding together because there was nowhere to
    /// put them, and a screen you have to recompile to extend grows the same backlog again.</para>
    ///
    /// <para><b>This asset is load-bearing for licence compliance, not decoration.</b> FMOD's EULA
    /// (<c>Assets/Plugins/FMOD/LICENSE.txt</c> clause 3) requires an in-game credit containing the
    /// words <c>FMOD</c> and <c>Firelight Technologies Pty Ltd.</c> on every tier — free, Indie and
    /// Basic alike. <c>CreditsReleaseGuard</c> (editor assembly) fails any non-development
    /// build whose manifest is missing or has lost those words, and
    /// <c>Tools/Build/check_credits_manifest.py</c> answers the same question offline.</para>
    ///
    /// <para><b>A notice is prose, never a typed licence field.</b> Modelling licences as an enum
    /// forces the UI to carry a formatter per licence kind and makes every unusual one
    /// (QuickScene Pro ships MIT <i>with an Indian jurisdiction clause</i>) unrepresentable — the
    /// same trap <c>Docs/CODEX.md</c> records for stats.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "CreditsManifest", menuName = "ScriptableObjects/Credits Manifest")]
    public class CreditsManifestSO : ScriptableObject
    {
        /// <summary>Where <see cref="Load"/> looks. Also the path both build gates check.</summary>
        public const string ResourcePath = "CreditsManifest";

        /// <summary>The two strings FMOD's EULA clause 3 requires, verbatim including the period.</summary>
        public const string RequiredFmodName = "FMOD";

        /// <summary>The two strings FMOD's EULA clause 3 requires, verbatim including the period.</summary>
        public const string RequiredFmodCompany = "Firelight Technologies Pty Ltd.";

        /// <summary>One credited party: who, what for, and — where a licence demands it — its notice.</summary>
        [Serializable]
        public class Entry
        {
            [Tooltip("Who or what is being credited. Shown as the row's title.")]
            public string Name;

            [Tooltip("One line: the person's role, or what the software does here. Optional.")]
            [TextArea(1, 3)]
            public string Role;

            [Tooltip("The licence notice, verbatim, where one is owed. Prose on purpose - a typed " +
                     "licence field cannot express 'MIT with an added jurisdiction clause'. " +
                     "Leave empty for a person or a studio credit.")]
            [TextArea(3, 12)]
            public string Notice;
        }

        /// <summary>A headed run of entries. Order in the list is order on screen.</summary>
        [Serializable]
        public class Section
        {
            [Tooltip("Section heading, e.g. MIDDLEWARE or THIRD-PARTY NOTICES.")]
            public string Heading;

            [Tooltip("Optional paragraph under the heading, before the entries.")]
            [TextArea(1, 4)]
            public string Blurb;

            public List<Entry> Entries = new();
        }

        [Tooltip("Shown at the very top of the screen, above the first section.")]
        [SerializeField] string title = "CREDITS";

        [Tooltip("Ordered sections. The MIDDLEWARE section is where FMOD's required credit line " +
                 "lives; THIRD-PARTY NOTICES carries the MIT/BSD/Apache/OFL bodies.")]
        [SerializeField] List<Section> sections = new();

        public string Title => title;
        public IReadOnlyList<Section> Sections => sections;

        /// <summary>
        /// Loads the one manifest. Returns null when the asset is absent — callers show nothing
        /// rather than inventing text, and the build guard is what makes absence impossible to ship.
        /// </summary>
        public static CreditsManifestSO Load() => Resources.Load<CreditsManifestSO>(ResourcePath);

        /// <summary>
        /// Every authored string in the manifest, concatenated. This is what both gates search for
        /// FMOD's required words, and it is deliberately the WHOLE text rather than one named
        /// field: the requirement is that the credit appears in game, not that it sits in a
        /// particular slot, so moving the line between sections must not break the build.
        /// </summary>
        public string AllText()
        {
            var sb = new StringBuilder();
            sb.AppendLine(title);
            if (sections == null) return sb.ToString();
            foreach (var section in sections)
            {
                if (section == null) continue;
                sb.AppendLine(section.Heading);
                sb.AppendLine(section.Blurb);
                if (section.Entries == null) continue;
                foreach (var entry in section.Entries)
                {
                    if (entry == null) continue;
                    sb.AppendLine(entry.Name);
                    sb.AppendLine(entry.Role);
                    sb.AppendLine(entry.Notice);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// True when the manifest carries FMOD's required credit. Shared by the runtime, the build
        /// guard and the editor tooling so there is one definition of "satisfied" — the offline
        /// Python gate mirrors this test and its self-test proves it fails without the line.
        /// </summary>
        public bool SatisfiesFmodCredit() => SatisfiesFmodCredit(AllText());

        /// <inheritdoc cref="SatisfiesFmodCredit()"/>
        public static bool SatisfiesFmodCredit(string allText) =>
            !string.IsNullOrEmpty(allText) &&
            allText.Contains(RequiredFmodName, StringComparison.Ordinal) &&
            allText.Contains(RequiredFmodCompany, StringComparison.Ordinal);
    }
}
