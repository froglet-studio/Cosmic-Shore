using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>One faction message as the Port draws it (Docs/MENU_REDESIGN_B.md section 90).</summary>
    public readonly struct FactionMessage
    {
        public readonly string Id;
        public readonly string SenderId;
        public readonly string SenderName;
        public readonly int AvatarId;
        public readonly string Text;
        public readonly DateTime SentUtc;
        public readonly bool FromSelf;

        public FactionMessage(string id, string senderId, string senderName, int avatarId, string text, DateTime sentUtc, bool fromSelf)
        {
            Id = id ?? string.Empty;
            SenderId = senderId ?? string.Empty;
            SenderName = senderName ?? string.Empty;
            AvatarId = avatarId;
            Text = text ?? string.Empty;
            SentUtc = sentUtc;
            FromSelf = fromSelf;
        }
    }

    /// <summary>Why a message was not sent. Each one has its own authored line in the Port.</summary>
    public enum FactionSendRefusal
    {
        None = 0,
        Empty = 1,
        TooLong = 2,
        TooSoon = 3,
        ReadOnly = 4,
        Offline = 5,
    }

    /// <summary>
    /// The faction chat's rules, as pure functions: what a message may hold, how often a player may send, who may send,
    /// and how the sender's avatar and name ride with a message. Vivox carries the messages
    /// (<see cref="FactionChatService"/>); these rules are the game's own, held on the client.
    /// </summary>
    public static class FactionChatRules
    {
        /// <summary>The Vivox channel a faction's messages travel on. Today every player is in faction "all".</summary>
        public static string ChannelFor(string factionId) => "faction_" + (string.IsNullOrEmpty(factionId) ? "all" : factionId);

        /// <summary>Control characters and line breaks become spaces; runs of white space become one; the ends are trimmed.</summary>
        public static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var sb = new StringBuilder(text.Length);
            bool space = false;
            foreach (char ch in text)
            {
                bool white = char.IsWhiteSpace(ch) || char.IsControl(ch) || ch == '\u2028' || ch == '\u2029';
                if (white)
                {
                    space = sb.Length > 0;
                    continue;
                }
                if (space) sb.Append(' ');
                space = false;
                sb.Append(ch);
            }
            return sb.ToString();
        }

        /// <summary>Characters as a player counts them: a surrogate pair (an emoji) is one.</summary>
        public static int Length(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int n = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])) i++;
                n++;
            }
            return n;
        }

        /// <summary>
        /// True unless the birth year says the player is not surely <paramref name="minAge"/> (born in Y: surely that
        /// old from Y + minAge + 1). An unknown year (0) may post. The age screen records the year
        /// (<see cref="FairPlayAge"/>).
        /// </summary>
        public static bool CanPost(int birthYear, int nowYear, int minAge) =>
            birthYear <= 0 || nowYear - birthYear - 1 >= minAge;

        /// <summary>The send rules: (text, last send, now) -> why not, or None.</summary>
        public static FactionSendRefusal Check(string cleanText, int maxLength, DateTime lastSentUtc, DateTime nowUtc,
                                               float minIntervalSeconds, bool canPost)
        {
            if (string.IsNullOrEmpty(cleanText)) return FactionSendRefusal.Empty;
            if (Length(cleanText) > maxLength) return FactionSendRefusal.TooLong;
            if (!canPost) return FactionSendRefusal.ReadOnly;
            if (WaitSeconds(lastSentUtc, nowUtc, minIntervalSeconds) > 0) return FactionSendRefusal.TooSoon;
            return FactionSendRefusal.None;
        }

        /// <summary>Whole seconds until the next message may go; 0 when it may go now.</summary>
        public static int WaitSeconds(DateTime lastSentUtc, DateTime nowUtc, float minIntervalSeconds)
        {
            double left = minIntervalSeconds - (nowUtc - lastSentUtc).TotalSeconds;
            return left > 0 ? (int)Math.Ceiling(left) : 0;
        }

        // The sender's avatar and name ride in the message's metadata ("3|Kestrel"), so every reader draws the face and
        // the name the sender's profile shows, whatever Vivox knows them by.
        public static string EncodeMetadata(int avatarId, string name) =>
            avatarId.ToString(CultureInfo.InvariantCulture) + "|" + (name ?? string.Empty);

        public static void DecodeMetadata(string metadata, out int avatarId, out string name)
        {
            avatarId = -1;
            name = null;
            if (string.IsNullOrEmpty(metadata)) return;
            int bar = metadata.IndexOf('|');
            string id = bar < 0 ? metadata : metadata.Substring(0, bar);
            if (!int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out avatarId)) avatarId = -1;
            if (bar >= 0 && bar + 1 < metadata.Length) name = metadata.Substring(bar + 1);
        }
    }

    /// <summary>
    /// The messages the Port holds for one faction: no duplicates (history and the live feed overlap), oldest first,
    /// at most <see cref="Capacity"/>.
    /// </summary>
    public sealed class FactionChatLog
    {
        readonly List<FactionMessage> _messages = new();
        readonly HashSet<string> _ids = new();

        public FactionChatLog(int capacity) => Capacity = Mathf.Max(1, capacity);

        public int Capacity { get; }
        public IReadOnlyList<FactionMessage> Messages => _messages;

        /// <summary>Adds a message in time order. False when it was already held, or is older than everything kept in a full log.</summary>
        public bool Add(in FactionMessage message)
        {
            if (string.IsNullOrEmpty(message.Id) || _ids.Contains(message.Id)) return false;
            int at = _messages.Count;
            while (at > 0 && _messages[at - 1].SentUtc > message.SentUtc) at--;
            if (at == 0 && _messages.Count >= Capacity) return false;
            _messages.Insert(at, message);
            _ids.Add(message.Id);
            while (_messages.Count > Capacity)
            {
                _ids.Remove(_messages[0].Id);
                _messages.RemoveAt(0);
            }
            return true;
        }

        public void Clear()
        {
            _messages.Clear();
            _ids.Clear();
        }
    }

    /// <summary>
    /// The pilots this device has muted in the Port: their messages are not drawn. Kept on this device only
    /// (PlayerPrefs), never sent anywhere.
    /// </summary>
    public static class FactionMutes
    {
        public const string PrefKey = "FactionChatMutedPlayers";

        static HashSet<string> s_muted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_muted = null;

        static HashSet<string> Muted => s_muted ??= Parse(PlayerPrefs.GetString(PrefKey, string.Empty));

        public static int Count => Muted.Count;

        public static bool IsMuted(string playerId) => !string.IsNullOrEmpty(playerId) && Muted.Contains(playerId);

        public static void Mute(string playerId)
        {
            if (string.IsNullOrEmpty(playerId) || !Muted.Add(playerId)) return;
            Save();
        }

        public static void UnmuteAll()
        {
            if (Muted.Count == 0) return;
            Muted.Clear();
            Save();
        }

        static void Save()
        {
            PlayerPrefs.SetString(PrefKey, Join(Muted));
            PlayerPrefs.Save();
        }

        /// <summary>Player ids are letters and digits, so a comma list is enough.</summary>
        public static HashSet<string> Parse(string stored)
        {
            var set = new HashSet<string>();
            if (string.IsNullOrEmpty(stored)) return set;
            foreach (var id in stored.Split(','))
                if (!string.IsNullOrWhiteSpace(id)) set.Add(id.Trim());
            return set;
        }

        public static string Join(IEnumerable<string> ids) => string.Join(",", ids);
    }
}
