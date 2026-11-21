// ─────────────────────────────────────────────────────────────────────────────
// LavaLampPresence.cs
// The one fact "this player's game is up": the lava-lamp menu has been on screen
// (the boot splash faded over Menu_Main) at least once in this Play.
//
// WHY this exists (Shombith, 2026-10-31, after the four-player test of 30 Oct):
//   A player joins the shared presence lobby the moment they sign in, which is
//   during the boot - seconds to tens of seconds before Menu_Main is up (the
//   boot's menu load has frozen for 10-20 s, B36). Every other player's ONLINE
//   list showed them in that window, and an invite or a join sent to them then
//   went nowhere: the friend list "was not trustable". A player is now listed
//   only once they publish this flag (HostConnectionService's "ready" presence
//   property), which they do the first time the lava lamp is shown.
//
// It stays true through matches (a player in a match is online, and the row
// says which match). It is reset at the start of every Play: the Editor runs
// without a domain reload, so a static would otherwise carry the last Play's
// value into the next boot (the B33 shape).
// ─────────────────────────────────────────────────────────────────────────────

using System;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Whether the lava-lamp menu has been shown in this Play. Written by
    /// <c>SceneLoader</c> when the menu's splash fades; read by
    /// <see cref="HostConnectionService"/>, which publishes it to the presence lobby.
    /// </summary>
    public static class LavaLampPresence
    {
        /// <summary>True once the lava-lamp menu has been on screen in this Play.</summary>
        public static bool IsShown { get; private set; }

        /// <summary>Raised once per Play, the moment <see cref="IsShown"/> turns true.</summary>
        public static event Action Shown;

        /// <summary>Marks the lava lamp as shown. Idempotent: raises <see cref="Shown"/> only the first time.</summary>
        public static void MarkShown()
        {
            if (IsShown) return;
            IsShown = true;
            Shown?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlay()
        {
            IsShown = false;
            Shown   = null;
        }
    }
}
