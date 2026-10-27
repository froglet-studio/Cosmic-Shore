using System;
using System.Collections.Generic;
using System.Text;

namespace CosmicShore.Data
{
    /// <summary>
    /// Every squad a player has saved, and which one is active (Docs/PILOTS_AND_ARTIFACTS.md §1).
    /// Persisted as <c>SquadCloudData.Book</c> under the <c>SQUAD_DATA</c> cloud key.
    ///
    /// <para>Pure data with no engine dependency, so its rules run in an edit-mode test or headless.
    /// The book keeps <see cref="ActiveIndex"/> pointing at the same squad through every add and
    /// remove, and <see cref="Normalize"/> repairs anything a hand-edited or partial cloud record can
    /// carry (a null list, null entries, an index out of range, a name the UI font cannot draw).</para>
    /// </summary>
    [Serializable]
    public class SquadBook
    {
        /// <summary>Longest squad name kept. Longer names are cut, not refused.</summary>
        public const int MaxNameLength = 24;

        public List<SavedSquad> Squads = new();
        public int ActiveIndex;

        /// <summary>The squad the pre-game flow plays, or null while the book is empty.</summary>
        public SavedSquad GetActive() =>
            Squads != null && ActiveIndex >= 0 && ActiveIndex < Squads.Count ? Squads[ActiveIndex] : null;

        /// <summary>Makes squad <paramref name="index"/> the active one. False when there is no such squad.</summary>
        public bool TrySetActive(int index)
        {
            if (Squads == null || index < 0 || index >= Squads.Count) return false;
            ActiveIndex = index;
            return true;
        }

        /// <summary>
        /// Appends <paramref name="squad"/> with its name made drawable (blank names become
        /// "Squad N"). The first squad added becomes the active one; later ones do not.
        /// </summary>
        public SavedSquad Add(SavedSquad squad)
        {
            if (squad == null) throw new ArgumentNullException(nameof(squad));
            Squads ??= new List<SavedSquad>();

            squad.Name = SanitizeName(squad.Name, Squads.Count);
            Squads.Add(squad);
            if (Squads.Count == 1) ActiveIndex = 0;
            return squad;
        }

        /// <summary>
        /// Removes squad <paramref name="index"/>. The active squad stays active when another one is
        /// removed; removing the active squad makes its successor (or the new last squad) active.
        /// </summary>
        public bool RemoveAt(int index)
        {
            if (Squads == null || index < 0 || index >= Squads.Count) return false;

            Squads.RemoveAt(index);
            if (index < ActiveIndex) ActiveIndex--;
            if (ActiveIndex >= Squads.Count) ActiveIndex = Math.Max(0, Squads.Count - 1);
            return true;
        }

        /// <summary>Renames squad <paramref name="index"/>; see <see cref="SanitizeName"/>.</summary>
        public bool Rename(int index, string name)
        {
            if (Squads == null || index < 0 || index >= Squads.Count || Squads[index] == null) return false;
            Squads[index].Name = SanitizeName(name, index);
            return true;
        }

        /// <summary>
        /// Repairs the book after a load: a missing list, null entries, unusable names and an active
        /// index that points at nothing. Idempotent.
        /// </summary>
        public void Normalize()
        {
            Squads ??= new List<SavedSquad>();
            Squads.RemoveAll(s => s == null);

            for (int i = 0; i < Squads.Count; i++)
                Squads[i].Name = SanitizeName(Squads[i].Name, i);

            if (ActiveIndex < 0 || ActiveIndex >= Squads.Count) ActiveIndex = 0;
        }

        /// <summary>The name a squad at <paramref name="index"/> gets when it has none.</summary>
        public static string DefaultName(int index) => "Squad " + (index + 1);

        /// <summary>
        /// A name the UI can draw. The UI font carries printable ASCII only (Docs/claude/ANTI_PATTERNS.md:
        /// anything else renders as an empty box), so other characters are dropped, runs of spaces
        /// collapse to one, the result is trimmed and cut to <see cref="MaxNameLength"/>, and a name
        /// left empty becomes <see cref="DefaultName"/>.
        /// </summary>
        public static string SanitizeName(string raw, int index)
        {
            if (string.IsNullOrEmpty(raw)) return DefaultName(index);

            var sb = new StringBuilder(Math.Min(raw.Length, MaxNameLength));
            bool lastWasSpace = true;   // also drops leading spaces
            foreach (char c in raw)
            {
                bool isSpace = c == ' ' || c == '\t';
                if (!isSpace && (c < '!' || c > '~')) continue;

                if (isSpace)
                {
                    if (lastWasSpace) continue;
                    sb.Append(' ');
                }
                else
                {
                    sb.Append(c);
                }

                lastWasSpace = isSpace;
                if (sb.Length >= MaxNameLength) break;
            }

            string name = sb.ToString().TrimEnd();
            return name.Length == 0 ? DefaultName(index) : name;
        }
    }
}
