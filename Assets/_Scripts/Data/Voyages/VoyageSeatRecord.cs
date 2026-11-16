using UnityEngine;

namespace CosmicShore.Data
{
    /// <summary>
    /// The seats one voyage sails with (Voyages Milestone 1, Frogma Rev 113 Lobby and Picker; Docs/MENU_REDESIGN_B.md
    /// section 209.1). The Ark page opens a voyage on a copy of the player's active squad; the seat popup writes the player's
    /// picks into THAT copy, never into a saved squad, and Set sail sails exactly it. "You sail as" and the popup's saved
    /// line read the same copy, so what the page says is what spawns.
    /// <para>One voyage is held at a time, keyed by <see cref="JobKey"/> or <see cref="EscortKey"/>: opening another
    /// voyage starts it again from the active squad. Nothing is saved to the cloud; the copy lasts until the app closes or
    /// another voyage is opened.</para>
    /// </summary>
    public static class VoyageSeatRecord
    {
        /// <summary>The Ark escort's key (it has no job).</summary>
        public const string EscortKey = "escort";

        static string s_key;
        static SavedSquad s_squad;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_key = null;
            s_squad = null;
        }

        /// <summary>A job's key: its template, the pilot who brought it and where it goes (a difficulty copy keeps them).</summary>
        public static string JobKey(VoyageJob job) =>
            job == null ? null : job.TemplateId + "|" + job.PilotId + "|" + job.To;

        /// <summary>The voyage the record is for now, or null.</summary>
        public static string Key => s_key;

        /// <summary>
        /// The seats <paramref name="key"/>'s voyage sails with: the record already made for it, else a fresh copy of
        /// <paramref name="from"/> (the active squad). Null for no key or nothing to copy.
        /// </summary>
        public static SavedSquad For(string key, SavedSquad from)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (key == s_key && s_squad != null) return s_squad;
            if (from == null) return null;
            s_key = key;
            s_squad = SquadMembership.Copy(from);
            return s_squad;
        }

        /// <summary>The record for <paramref name="key"/> when one was made, else null (Set sail then sails the active
        /// squad, as before the record).</summary>
        public static SavedSquad Get(string key) =>
            !string.IsNullOrEmpty(key) && key == s_key ? s_squad : null;

        /// <summary>Replace <paramref name="key"/>'s seats with <paramref name="squad"/> (a seat changed).</summary>
        public static void Set(string key, SavedSquad squad)
        {
            if (string.IsNullOrEmpty(key) || squad == null) return;
            s_key = key;
            s_squad = squad;
        }

        /// <summary>Forget the record (a test, or the voyage sailed and came home).</summary>
        public static void Clear() => ResetStatics();

        /// <summary>The player's own seat in <paramref name="squad"/>: a joined squad's held seat, else the leader's.</summary>
        public static int MySeatOf(SavedSquad squad) =>
            squad == null ? -1 : squad.IsJoined() ? Mathf.Clamp(squad.MySeat, 0, SavedSquad.SlotCount - 1) : 0;

        /// <summary>
        /// True when <paramref name="seat"/> is an AI wingmate's on the Ark page: not the player's, and no friend in the
        /// lobby takes it (<see cref="VoyageRules.FriendAt"/>, the order the voyage seats them in).
        /// </summary>
        public static bool IsAiSeat(int seat, int mySeat, int friends) =>
            seat >= 0 && seat < SavedSquad.SlotCount && seat != mySeat && VoyageRules.FriendAt(seat, mySeat, friends) < 0;

        /// <summary>
        /// "Fill the AI seats from a saved squad" (Rev 113 Lobby): a copy of <paramref name="seats"/> whose AI seats take,
        /// in order, the wingmates of <paramref name="preset"/> (its seats that are neither its owner's nor a person's).
        /// The player's seat and the friends' seats are never touched. A wingmate's pilot or artifact that the player's own
        /// seat already carries is cleared, so a pilot never flies two seats (the caller fills empty pilots from the crew).
        /// Null when there is nothing to fill from.
        /// </summary>
        public static SavedSquad FillAiSeats(SavedSquad seats, SavedSquad preset, int friends)
        {
            if (seats == null || preset == null) return null;
            var result = SquadMembership.Copy(seats);
            int mine = MySeatOf(seats);
            int presetMine = MySeatOf(preset);
            var mySlot = mine >= 0 ? result.GetSlot(mine) : default;
            int from = 0;
            for (int seat = 0; seat < SavedSquad.SlotCount; seat++)
            {
                if (!IsAiSeat(seat, mine, friends)) continue;
                // The preset's next wingmate.
                while (from < SavedSquad.SlotCount && (from == presetMine || preset.GetSlot(from).IsPlayer())) from++;
                if (from >= SavedSquad.SlotCount) break;
                var wingmate = preset.GetSlot(from++);
                var slot = result.GetSlot(seat).WithLoadout(wingmate);
                if (!string.IsNullOrEmpty(slot.PilotId) && slot.PilotId == mySlot.PilotId) slot.PilotId = null;
                if (!string.IsNullOrEmpty(slot.ArtifactId) && slot.ArtifactId == mySlot.ArtifactId) slot.ArtifactId = null;
                result.SetSlot(seat, slot);
            }
            // Two wingmates from the preset never share a pilot with each other (the preset kept them apart), but one may
            // share with a seat left as it was; the later seat gives it up.
            for (int i = 0; i < SavedSquad.SlotCount; i++)
            for (int j = i + 1; j < SavedSquad.SlotCount; j++)
            {
                var b = result.GetSlot(j);
                if (string.IsNullOrEmpty(b.PilotId) || b.PilotId != result.GetSlot(i).PilotId || j == mine) continue;
                b.PilotId = null;
                result.SetSlot(j, b);
            }
            return result;
        }
    }
}
