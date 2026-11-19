using System.Collections.Generic;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// A player's avatars are their own crew's faces (Garrett, 2026-11-19: "each persons set of avatars should be
    /// consistent with their crew"). The avatar list (<see cref="SO_ProfileIconList"/>,
    /// Tools/Build/author_pilot_avatar_assets.py) holds every painted pilot's close-up, named by the pilot's key, so
    /// other players' avatars still draw; the picker offers only the account's crew (<see cref="Offered"/>), and an
    /// avatar from outside the crew becomes the crew's pilot on the same callsign (<see cref="Consistent"/>).
    /// </summary>
    public static class CrewAvatars
    {
        /// <summary>The avatars the picker offers: the crew's, in the crew's order. Every avatar while no crew is dealt,
        /// or when none of the crew has one (a build without the roster's avatars).</summary>
        public static List<ProfileIcon> Offered(IReadOnlyList<ProfileIcon> icons, IReadOnlyList<string> crew)
        {
            var all = new List<ProfileIcon>();
            if (icons == null) return all;
            all.AddRange(icons);
            if (crew == null || crew.Count == 0) return all;
            var offered = new List<ProfileIcon>();
            foreach (var key in crew)
                foreach (var icon in icons)
                    if (icon.Name == key)
                    {
                        offered.Add(icon);
                        break;
                    }
            return offered.Count > 0 ? offered : all;
        }

        /// <summary>
        /// <paramref name="avatarId"/> when it is one of the crew's avatars (or no crew is dealt); else the avatar of the
        /// crew's pilot on the same callsign as the pilot it shows; else the crew's first avatar.
        /// </summary>
        public static int Consistent(int avatarId, IReadOnlyList<ProfileIcon> icons, IReadOnlyList<string> crew, PaintedCrewSO painted)
        {
            var offered = Offered(icons, crew);
            if (offered.Count == 0 || crew == null || crew.Count == 0) return avatarId;
            foreach (var icon in offered)
                if (icon.Id == avatarId) return avatarId;

            string shown = null;
            if (icons != null)
                foreach (var icon in icons)
                    if (icon.Id == avatarId) { shown = icon.Name; break; }
            if (painted && shown != null && painted.TryGet(shown, out var was))
                foreach (var icon in offered)
                    if (painted.TryGet(icon.Name, out var p) && p.callsign == was.callsign) return icon.Id;
            return offered[0].Id;
        }
    }
}
