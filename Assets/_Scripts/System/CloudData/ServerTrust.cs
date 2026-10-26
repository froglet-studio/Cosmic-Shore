using System;
using System.Collections.Generic;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Unity.Services.CloudCode;

namespace CosmicShore.Core
{
    /// <summary>
    /// Whether the game asks the server which pool it plays in (fair play, phase B; Docs/AntiCheat/README.md §4.5-4.6).
    /// Off until the <c>trust</c> Cloud Code script is deployed (Tools/UGS/README.md). While off, every account joins
    /// the one presence lobby it always did, posts to the public boards and never sees a notice.
    /// </summary>
    public static class FairPlayTrustGate
    {
        public static readonly bool Enabled = false;

        /// <summary>
        /// Whether a party host removes a guest whose pool pass is missing or does not verify. Off at first: a guest on
        /// a build from before the pass (or one whose sign-in could not reach the server) has none, and removing them
        /// would punish the honest. A guest whose pass verifies as another pool is removed either way.
        /// </summary>
        public static readonly bool RemoveUnverifiedGuests = false;
    }

    /// <summary>
    /// The client half of the trust record: which presence lobby to join, which twin leaderboard holds this account's
    /// times, and the notice once one is owed. The server keeps the record where no player can read it and tells the
    /// client only these three things, never the stage, score or reasons (<c>Tools/UGS/CloudCode/trust.js</c>).
    ///
    /// <para>What it does not do: stop a modified client from joining the shore lobby anyway. A lobby is chosen by
    /// the client; the server-side checks (results, scores, rewards) do not depend on it, and a host can ask
    /// <c>samePool</c> about a guest. Routing makes an honest flagged account meet its own kind, not a cheater.</para>
    /// </summary>
    public static class FairPlayTrust
    {
        public const string Endpoint = "trust";

        [Serializable]
        public class Notice
        {
            public string kind;        // "cheating" | "automated" | "quitting"
            public int rule;           // the Code of Conduct rule the notice names
            public bool restricted;    // a person restricted the account (else an automated measure)
            public string until;       // ISO date the restriction ends (restricted only)
            public bool automated;
            public string appeal;      // "none" | "open" | "upheld" | "refused"
        }

        [Serializable]
        class StatusResult
        {
            public bool ok;
            public string lobby;
            public string board;
            public Notice notice;
            public string pass;
        }

        [Serializable]
        class VerifyResult
        {
            public bool ok;
            public bool verified;
            public bool same;
        }

        /// <summary>What a host learns about a guest from the guest's pass.</summary>
        public enum GuestCheck
        {
            Unknown = 0,       // the server could not be asked (gate off, offline, rate limited)
            SamePool = 1,
            OtherPool = 2,
            Unverified = 3,    // no pass, or one the guest does not hold
        }

        /// <summary>The party-session player property that carries this account's pool pass to its party.</summary>
        public const string PassPropertyKey = "fp_pass";

        [Serializable]
        class AppealResult
        {
            public bool ok;
            public string reason;
            public string appeal;
        }

        /// <summary>Appended to the presence lobby's game mode: empty on the shore.</summary>
        public static string LobbySuffix { get; private set; } = string.Empty;

        /// <summary>Appended to a weekly board's id for this account's own times: empty on the shore.</summary>
        public static string BoardSuffix { get; private set; } = string.Empty;

        /// <summary>
        /// This sign-in's pool pass: shown only to a party this account joins, so its host can ask the server whether
        /// the account belongs in its pool. Empty while the gate is off.
        /// </summary>
        public static string Pass { get; private set; } = string.Empty;

        /// <summary>The notice the server says is owed, or null.</summary>
        public static Notice Current { get; private set; }

        /// <summary>Raised on the main thread when <see cref="Current"/> changes.</summary>
        public static event Action<Notice> OnNoticeChanged;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            LobbySuffix = string.Empty;
            BoardSuffix = string.Empty;
            Pass = string.Empty;
            Current = null;
            OnNoticeChanged = null;
        }

        /// <summary>The presence lobby's game mode for this account: <paramref name="shore"/> plus the suffix.</summary>
        public static string PresenceGameMode(string shore) => shore + LobbySuffix;

        /// <summary>
        /// Asks the server once, at sign-in, before the presence lobby is joined. A failure keeps the last answer
        /// (on a first sign-in: the shore), so a server that cannot be reached never stops a player from playing.
        /// </summary>
        public static async UniTask RefreshAsync()
        {
            if (!FairPlayTrustGate.Enabled) return;
            try
            {
                var result = await CloudCodeService.Instance
                    .CallEndpointAsync<StatusResult>(Endpoint, new Dictionary<string, object> { { "action", "status" } })
                    .AsMainThread();
                if (result is not { ok: true }) return;
                LobbySuffix = Sanitize(result.lobby);
                BoardSuffix = Sanitize(result.board);
                Pass = IsPass(result.pass) ? result.pass : string.Empty;
                var notice = result.notice != null && !string.IsNullOrEmpty(result.notice.kind) ? result.notice : null;
                Current = notice;
                OnNoticeChanged?.Invoke(notice);
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[FairPlay] Trust status could not reach the server ({e.Message}); keeping the last answer.");
            }
        }

        /// <summary>
        /// A party host asks about a guest who joined, with the pass the guest put on its party-session properties.
        /// The answer covers only that guest, and only if the pass is really theirs.
        /// </summary>
        public static async UniTask<GuestCheck> VerifyGuestAsync(string guestId, string guestPass)
        {
            if (!FairPlayTrustGate.Enabled || string.IsNullOrEmpty(guestId)) return GuestCheck.Unknown;
            if (!IsPass(guestPass)) return GuestCheck.Unverified;
            try
            {
                var result = await CloudCodeService.Instance
                    .CallEndpointAsync<VerifyResult>(Endpoint, new Dictionary<string, object>
                    {
                        { "action", "verifyGuest" }, { "guestId", guestId }, { "pass", guestPass },
                    })
                    .AsMainThread();
                if (result is not { ok: true }) return GuestCheck.Unknown;
                if (!result.verified) return GuestCheck.Unverified;
                return result.same ? GuestCheck.SamePool : GuestCheck.OtherPool;
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[FairPlay] Could not check a guest's pool ({e.Message}); leaving them in the party.");
                return GuestCheck.Unknown;
            }
        }

        /// <summary>A pass is 32 lowercase hex digits; anything else is no pass.</summary>
        public static bool IsPass(string pass)
        {
            if (string.IsNullOrEmpty(pass) || pass.Length != 32) return false;
            foreach (char c in pass)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            return true;
        }

        /// <summary>Opens an appeal with the player's own words (trimmed by the server). True when it is open.</summary>
        public static async UniTask<bool> AppealAsync(string text)
        {
            if (!FairPlayTrustGate.Enabled) return false;
            try
            {
                var result = await CloudCodeService.Instance
                    .CallEndpointAsync<AppealResult>(Endpoint,
                        new Dictionary<string, object> { { "action", "appeal" }, { "text", text ?? string.Empty } })
                    .AsMainThread();
                bool open = result != null && (result.ok || result.appeal == "open");
                if (open && Current != null)
                {
                    Current.appeal = "open";
                    OnNoticeChanged?.Invoke(Current);
                }
                return open;
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[FairPlay] The appeal could not reach the server ({e.Message}).");
                return false;
            }
        }

        /// <summary>A suffix is a short token the server chose; anything else is treated as the shore.</summary>
        public static string Sanitize(string suffix)
        {
            if (string.IsNullOrEmpty(suffix) || suffix.Length > 16) return string.Empty;
            foreach (char c in suffix)
                if (!(char.IsLetterOrDigit(c) || c == '_')) return string.Empty;
            return suffix;
        }
    }
}
