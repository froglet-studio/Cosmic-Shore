using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// Scans GameDataSO.Players for hostile vessels and writes them into the
    /// DecisionContext as ThreatInfo entries. Severity is a simple proximity heuristic;
    /// fitness components and policies that care about closer/faster threats can read
    /// it without recomputing.
    /// </summary>
    public class ThreatSensor : ITrainingSensor
    {
        public float MaxRange = 200f;

        readonly GameDataSO _gameData;
        IVessel _vessel;

        public ThreatSensor(GameDataSO gameData) { _gameData = gameData; }

        public void Bind(IVessel vessel) => _vessel = vessel;
        public void OnEpisodeStart() { }

        public void Sample(DecisionContext ctx)
        {
            ctx.Threats.Clear();
            if (_gameData == null) return;

            for (int i = 0; i < _gameData.Players.Count; i++)
            {
                var p = _gameData.Players[i];
                if (p == null || p.Vessel == null) continue;
                if (p.Vessel == _vessel) continue;
                if (p.Domain == ctx.MyDomain && ctx.MyDomain != Domains.Unassigned) continue;

                Vector3 pos = p.Vessel.Transform.position;
                float range = (pos - ctx.Position).magnitude;
                if (range > MaxRange) continue;

                Vector3 vel = p.Vessel.Transform.forward
                            * (p.Vessel.VesselStatus != null ? p.Vessel.VesselStatus.Speed : 0f);

                // Severity rises sharply near contact, falls off with range.
                float severity = Mathf.Clamp01(1f - range / MaxRange);
                if (severity < 0.05f) continue;

                ctx.Threats.Add(new ThreatInfo
                {
                    Position = pos,
                    Velocity = vel,
                    Range = range,
                    Severity = severity,
                    Domain = p.Domain
                });
            }
        }
    }
}
