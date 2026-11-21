// ─────────────────────────────────────────────────────────────────────────────
// ModerationSdk.cs — engine placeholder surface for the UGS Moderation SDK
// (original contract: Unity.Services.Moderation — Moderation.Instance.NewReport /
// ReportPlayer, Models.Report, Models.ReportReason). Added so the game's player
// reports (Assets/_Scripts/System/Moderation/PlayerReports.cs, fair play T8)
// compile in Amoebius. There is no moderation service in a single process, so
// sending a report fails the way an unreachable service does; the game shows
// its "try again" word. Tests swap a fake into the settable Instance.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CosmicShore.Engine.Services.Moderation.Models;

namespace CosmicShore.Engine.Services.Moderation.Models
{
    public class ReportReason
    {
        public const string Hacking = "hacking";
        public const string Sabotage = "sabotage";
        public const string InappropriatePlayerName = "player name";
        public const string VerbalAbuse = "verbal abuse";
        public const string HateSpeech = "hate speech";
        public const string Threat = "threat";
        public ReportReason(string reason) { this.reason = reason; }
        public string reason { get; }
    }

    public class Report
    {
        public Report(string playerId, ReportReason reason) { PlayerID = playerId; Reason = reason; Timestamp = DateTime.Now; }
        public string PlayerID { get; set; }
        public ReportReason Reason { get; set; }
        public DateTime Timestamp { get; set; }
        public List<string> Notes { get; } = new();
    }
}

namespace CosmicShore.Engine.Services.Moderation
{
    public interface IModerationService
    {
        Report NewReport(string playerID, ReportReason reason);
        Task ReportPlayer(Report report);
    }

    /// <summary>The default: no moderation service is reachable from a single process.</summary>
    public sealed class UnreachableModerationService : IModerationService
    {
        public Report NewReport(string playerID, ReportReason reason) => new(playerID, reason);
        public Task ReportPlayer(Report report) =>
            Task.FromException(new InvalidOperationException("Player reports are not available in Amoebius."));
    }

    public static class Moderation
    {
        public static IModerationService Instance { get; set; } = new UnreachableModerationService();
    }
}
