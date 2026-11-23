// ─────────────────────────────────────────────────────────────────────────────
// VivoxSdk.cs — engine placeholder surface for the Unity Vivox SDK
// (original contract: Unity.Services.Vivox — VivoxService.Instance, IVivoxService, VivoxMessage, LoginOptions,
// MessageOptions, ChatCapability, VivoxInitializationState). Added so the Port's faction messages
// (Assets/_Scripts/System/FactionChat/FactionChatService.cs, Docs/MENU_REDESIGN_B.md section 90) compile in
// Amoebius. There is no Vivox service in a single process, so initialising fails the way an unreachable service
// does; the game shows its "Faction messages are offline" line. Tests swap a fake into the settable Instance.
// Only the members the game calls are here.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace CosmicShore.Engine.Services.Vivox
{
    public enum VivoxInitializationState
    {
        Uninitialized = 0,
        Initializing = 1,
        Initialized = 2,
    }

    public enum ChatCapability
    {
        TextOnly = 0,
        AudioOnly = 1,
        TextAndAudio = 2,
    }

    public sealed class LoginOptions
    {
        public string DisplayName { get; set; }
    }

    public sealed class MessageOptions
    {
        public string Language { get; set; }
        public string Metadata { get; set; }
    }

    public sealed class VivoxMessage
    {
        public string ChannelName { get; init; }
        public bool FromSelf { get; init; }
        public bool IsTranscribedMessage { get; init; }
        public string MessageId { get; init; }
        public string MessageText { get; init; }
        public string Metadata { get; init; }
        public DateTime ReceivedTime { get; init; }
        public string SenderDisplayName { get; init; }
        public string SenderPlayerId { get; init; }
    }

    public interface IVivoxService
    {
        event Action<VivoxMessage> ChannelMessageReceived;
        VivoxInitializationState InitializationState { get; }
        bool IsLoggedIn { get; }
        ReadOnlyDictionary<string, ReadOnlyCollection<object>> ActiveChannels { get; }
        Task InitializeAsync();
        Task LoginAsync(LoginOptions options = null);
        Task JoinGroupChannelAsync(string channelName, ChatCapability chatCapability);
        Task LeaveChannelAsync(string channelName);
        Task SendChannelTextMessageAsync(string channelName, string message, MessageOptions options = null);
        Task<ReadOnlyCollection<VivoxMessage>> GetChannelTextMessageHistoryAsync(string channelName, int requestSize = 10);
    }

    /// <summary>The default: no Vivox service is reachable from a single process.</summary>
    public sealed class UnreachableVivoxService : IVivoxService
    {
        static readonly ReadOnlyDictionary<string, ReadOnlyCollection<object>> None = new(new Dictionary<string, ReadOnlyCollection<object>>());
        static Task Unreachable() => Task.FromException(new InvalidOperationException("Vivox is not available in Amoebius."));

        public event Action<VivoxMessage> ChannelMessageReceived { add { } remove { } }
        public VivoxInitializationState InitializationState => VivoxInitializationState.Uninitialized;
        public bool IsLoggedIn => false;
        public ReadOnlyDictionary<string, ReadOnlyCollection<object>> ActiveChannels => None;
        public Task InitializeAsync() => Unreachable();
        public Task LoginAsync(LoginOptions options = null) => Unreachable();
        public Task JoinGroupChannelAsync(string channelName, ChatCapability chatCapability) => Unreachable();
        public Task LeaveChannelAsync(string channelName) => Task.CompletedTask;
        public Task SendChannelTextMessageAsync(string channelName, string message, MessageOptions options = null) => Unreachable();
        public Task<ReadOnlyCollection<VivoxMessage>> GetChannelTextMessageHistoryAsync(string channelName, int requestSize = 10) =>
            Task.FromResult(new ReadOnlyCollection<VivoxMessage>(new List<VivoxMessage>()));
    }

    public static class VivoxService
    {
        public static IVivoxService Instance { get; set; } = new UnreachableVivoxService();
    }
}
