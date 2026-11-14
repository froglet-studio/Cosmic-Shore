// ─────────────────────────────────────────────────────────────────────────────
// RelayQosSdk.cs — engine placeholder surface for the two UGS calls the game's
// Settings > REGION tab makes (original contracts: Unity.Services.Relay —
// RelayService.Instance.ListRegionsAsync, Models.Region; Unity.Services.Qos —
// QosService.Instance.GetSortedRelayQosResultsAsync, IQosResult,
// IQosAnnotatedResult). Added so Assets/_Scripts/Controller/Multiplayer/
// RelayRegionProbe.cs compiles in Amoebius.
//
// The engine's relay (Port/docs/MULTIPLAYER.md §6) does not list Unity Relay's
// regions or measure QoS, so both return empty lists: the REGION tab then
// offers its authored region names without ping, and the session shim's
// WithRelayNetwork(region) records no region. Tests swap fakes into the
// settable Instance properties.
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections.Generic;
using System.Threading.Tasks;
using CosmicShore.Engine.Services.Relay.Models;

namespace CosmicShore.Engine.Services.Relay.Models
{
    /// <summary>A Relay region (original contract: Unity.Services.Relay.Models.Region).</summary>
    public class Region
    {
        public Region(string id, string description) { Id = id; Description = description; }
        public string Id { get; }
        public string Description { get; }
    }
}

namespace CosmicShore.Engine.Services.Relay
{
    /// <summary>The slice of Unity.Services.Relay.IRelayService the game calls.</summary>
    public interface IRelayService
    {
        Task<List<Region>> ListRegionsAsync();
    }

    /// <summary>Static access point (original contract: Unity.Services.Relay.RelayService).</summary>
    public static class RelayService
    {
        public static IRelayService Instance { get; set; } = new NoRelayRegions();

        sealed class NoRelayRegions : IRelayService
        {
            public Task<List<Region>> ListRegionsAsync() => Task.FromResult(new List<Region>());
        }
    }
}

namespace CosmicShore.Engine.Services.Qos
{
    /// <summary>QoS measurements for one region (original contract: Unity.Services.Qos.IQosResult).</summary>
    public interface IQosResult
    {
        string Region { get; }
        int AverageLatencyMs { get; }
        float PacketLossPercent { get; }
    }

    /// <summary>A QoS result with its server annotations (original contract: Unity.Services.Qos.IQosAnnotatedResult).</summary>
    public interface IQosAnnotatedResult : IQosResult
    {
        Dictionary<string, List<string>> Annotations { get; }
    }

    /// <summary>The slice of Unity.Services.Qos.IQosService the game calls.</summary>
    public interface IQosService
    {
        Task<IList<IQosAnnotatedResult>> GetSortedRelayQosResultsAsync(IList<string> regions);
    }

    /// <summary>Static access point (original contract: Unity.Services.Qos.QosService).</summary>
    public static class QosService
    {
        public static IQosService Instance { get; set; } = new NoQos();

        sealed class NoQos : IQosService
        {
            public Task<IList<IQosAnnotatedResult>> GetSortedRelayQosResultsAsync(IList<string> regions) =>
                Task.FromResult<IList<IQosAnnotatedResult>>(new List<IQosAnnotatedResult>());
        }
    }
}
