using System;
using System.Collections.Generic;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Vivox;

namespace CosmicShore.Core
{
    /// <summary>
    /// The faction chat's transport: Unity Vivox text channels (Docs/MENU_REDESIGN_B.md section 90). One text-only
    /// group channel per faction (<see cref="FactionChatRules.ChannelFor"/>); the Port joins it while it is shown and
    /// leaves when it closes, so a player holds a channel seat only while reading.
    ///
    /// <para>Vivox signs in with the player's UGS Authentication id, so a message's sender id is the same id the
    /// friends list, the leaderboards and UGS Moderation use; a report on a message carries the shared channel as
    /// context (the Moderation package's Vivox integration).</para>
    ///
    /// <para>Every call answers rather than throws: false or an empty list when Vivox is not set up for the project,
    /// the player is not signed in, or the network is down. The Port shows its Offline line for all three.</para>
    /// </summary>
    public static class FactionChatService
    {
        /// <summary>A message arrived on a faction channel (the channel name, then the message). The sender's own
        /// messages arrive here too, which is how a sent message appears in the feed.</summary>
        public static event Action<string, FactionMessage> MessageReceived;

        static bool s_subscribed;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            MessageReceived = null;
            s_subscribed = false;
        }

        /// <summary>True when the player is signed in to UGS, so Vivox can be reached at all.</summary>
        public static bool CanConnect =>
            UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn;

        /// <summary>Initialises and logs in to Vivox once; later calls reuse the answer while it is still logged in.</summary>
        static async UniTask<bool> EnsureReadyAsync(string displayName)
        {
            if (!CanConnect) return false;
            var vivox = VivoxService.Instance;
            try
            {
                if (vivox.InitializationState == VivoxInitializationState.Uninitialized)
                    await vivox.InitializeAsync().AsMainThread();
                if (!vivox.IsLoggedIn)
                    await vivox.LoginAsync(new LoginOptions { DisplayName = displayName }).AsMainThread();
                if (!s_subscribed)
                {
                    vivox.ChannelMessageReceived += OnChannelMessage;
                    s_subscribed = true;
                }
                return vivox.IsLoggedIn;
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[FactionChat] Vivox could not be reached: {e.Message}");
                return false;
            }
        }

        /// <summary>Joins a faction's channel, text only. True when the player is in it.</summary>
        public static async UniTask<bool> JoinAsync(string channel, string displayName)
        {
            if (!await EnsureReadyAsync(displayName)) return false;
            var vivox = VivoxService.Instance;
            if (vivox.ActiveChannels.ContainsKey(channel)) return true;
            try
            {
                await vivox.JoinGroupChannelAsync(channel, ChatCapability.TextOnly).AsMainThread();
                CSDebug.LogVerbose(CSLogChannel.MenuUI, $"[FactionChat] Joined {channel}.");
                return true;
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[FactionChat] Could not join {channel}: {e.Message}");
                return false;
            }
        }

        /// <summary>Leaves a faction's channel, freeing the seat. Quiet when not in it.</summary>
        public static async UniTask LeaveAsync(string channel)
        {
            if (!CanConnect) return;
            var vivox = VivoxService.Instance;
            if (!vivox.IsLoggedIn || !vivox.ActiveChannels.ContainsKey(channel)) return;
            try
            {
                await vivox.LeaveChannelAsync(channel).AsMainThread();
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[FactionChat] Could not leave {channel}: {e.Message}");
            }
        }

        /// <summary>The channel's recent messages, oldest first; empty when they cannot be read.</summary>
        public static async UniTask<List<FactionMessage>> HistoryAsync(string channel, int count)
        {
            var list = new List<FactionMessage>();
            var vivox = VivoxService.Instance;
            if (!CanConnect || !vivox.IsLoggedIn || !vivox.ActiveChannels.ContainsKey(channel)) return list;
            try
            {
                var history = await vivox.GetChannelTextMessageHistoryAsync(channel, count).AsMainThread();
                foreach (var m in history) list.Add(Convert(m));
                list.Sort((a, b) => a.SentUtc.CompareTo(b.SentUtc));
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[FactionChat] History of {channel} could not be read: {e.Message}");
            }
            return list;
        }

        /// <summary>Sends one message. True when Vivox accepted it; it then arrives through <see cref="MessageReceived"/>.</summary>
        public static async UniTask<bool> SendAsync(string channel, string text, int avatarId, string displayName)
        {
            var vivox = VivoxService.Instance;
            if (!CanConnect || !vivox.IsLoggedIn || !vivox.ActiveChannels.ContainsKey(channel)) return false;
            try
            {
                var options = new MessageOptions { Metadata = FactionChatRules.EncodeMetadata(avatarId, displayName) };
                await vivox.SendChannelTextMessageAsync(channel, text, options).AsMainThread();
                return true;
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[FactionChat] Message to {channel} was not sent: {e.Message}");
                return false;
            }
        }

        static void OnChannelMessage(VivoxMessage message)
        {
            if (message == null || message.IsTranscribedMessage) return;
            MessageReceived?.Invoke(message.ChannelName, Convert(message));
        }

        static FactionMessage Convert(VivoxMessage m)
        {
            FactionChatRules.DecodeMetadata(m.Metadata, out int avatarId, out string name);
            var sent = m.ReceivedTime.Kind == DateTimeKind.Local ? m.ReceivedTime.ToUniversalTime() : m.ReceivedTime;
            return new FactionMessage(m.MessageId, m.SenderPlayerId, string.IsNullOrEmpty(name) ? m.SenderDisplayName : name,
                                      avatarId, FactionChatRules.Clean(m.MessageText), sent, m.FromSelf);
        }
    }
}
