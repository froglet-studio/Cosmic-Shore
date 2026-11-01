using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Pressure and healing (Garrett, 2026-10-31, after playing d8-d9: "way more fauna relentlessly attacking our Ark";
    /// "the Ark does not regenerate at all until it reaches the nucleus, and only if that cell is under the team's
    /// control"). Every second: the reactor may regrow only while the Ark is inside the nucleus of a world the squad
    /// holds (pacified, or the squad leads its control volume: <see cref="Cell.DominantDomain"/>); and every hostile world
    /// the escort stands lures a tier-set share of its fauna's goal picks onto the Ark (<see cref="Cell.SetFeedingLure"/>,
    /// the escort-only carve-out recorded in Docs/ECOSYSTEM.md §0.z).
    /// </summary>
    public sealed partial class ArkEscortRun
    {
        /// <summary>Without a nucleus, "the nucleus" is this share of the membrane radius around the core.</summary>
        const float CoreShareWithoutNucleus = 0.25f;

        void TickChaos(Ark ark, ArkEscortReport.LegReport r)
        {
            var squad = ark.Domain;
            var tier = _escort.TierFor(_difficulty);
            float lure = tier?.LureShare ?? 0f;
            bool regrow = false;
            for (int leg = 0; leg < _cellOfLeg.Length; leg++)
            {
                var cell = _cellOfLeg[leg];
                if (!cell) continue;
                // "Under the team's control": a hostile world the squad PACIFIED (earned: escort.pacifyShare, min volume and
                // nucleus time), or a world that started friendly and is still the squad's. Leading an empty hostile
                // nucleus with the Ark's own hull is not control.
                var legReport = leg < _report.LegReports.Count ? _report.LegReports[leg] : null;
                bool pacified = legReport is { Pacified: true };
                bool held = pacified || legReport is { Hostile: false } && cell.DominantDomain == squad;

                // The lure: hostile, unpacified worlds only (a pacified world's fauna are the squad's own colour).
                if (held || lure <= 0f) cell.SetFeedingLure(null, squad, 0f);
                else cell.SetFeedingLure(ark.transform, squad, lure);

                if (held && InCore(cell, ark.Position)) regrow = true;
            }
            ark.RegrowAllowed = regrow;
            if (regrow) { r.RegrowSeconds += SampleSeconds; _report.RegrowSeconds += SampleSeconds; }
        }

        static bool InCore(Cell cell, Vector3 at)
        {
            if (cell.HasNucleusControlZone) return cell.IsInsideNucleus(at);
            float core = (cell.MembraneRadius > 1f ? cell.MembraneRadius : 1200f) * CoreShareWithoutNucleus;
            return (at - cell.transform.position).sqrMagnitude <= core * core;
        }

        /// <summary>Each escort world's spawn cadence from the tier (faster waves at the high tiers).</summary>
        void PrepareChaos(Cell cell)
        {
            var tier = _escort.TierFor(_difficulty);
            cell.RuntimeFaunaSpawnPeriodScale = tier != null ? Mathf.Max(0.05f, tier.SpawnPeriodScale) : 1f;
            // The tier's armour bites, on every creature of the escort's worlds (through the run's fauna tuning).
            var f = _paramTarget?.Fauna;
            if (tier == null || f == null) return;
            if (tier.ShieldBiteMin > 0f) f.Set(FaunaRuntimeTuning.AllSpecies, "shieldBiteMin", tier.ShieldBiteMin);
            if (tier.SuperShieldBiteMin > 0f) f.Set(FaunaRuntimeTuning.AllSpecies, "superShieldBiteMin", tier.SuperShieldBiteMin);
            if (!Mathf.Approximately(tier.BiteScale, 1f))
            {
                float now = f.TryGet(FaunaRuntimeTuning.AllSpecies, "biteScale", out float b) ? b : 1f;
                if (!_biteScaleApplied) f.Set(FaunaRuntimeTuning.AllSpecies, "biteScale", now * tier.BiteScale);
                _biteScaleApplied = true;
            }
        }

        bool _biteScaleApplied;

        void ClearChaos()
        {
            _biteScaleApplied = false;
            foreach (var cell in _cellOfLeg)
                if (cell)
                {
                    cell.SetFeedingLure(null, Domains.Blue, 0f);
                    cell.RuntimeFaunaSpawnPeriodScale = 1f;
                }
            if (Ark) Ark.RegrowAllowed = true;
        }
    }
}
