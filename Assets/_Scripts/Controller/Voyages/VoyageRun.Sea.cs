using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The shared base of the Milestone 2 voyages (Docs/Voyages/MILESTONE_2.md; Shombith 2026-11-24): a named voyage's
    /// sea is laid from its rung (I1-I4, how dangerous the water is), its cells keep a clock of about 50 seconds each so
    /// a run lands at 3 to 5 minutes, a Salvage cell fills with loose crystals adrift, and the run reports where the Ark
    /// is on its course for the pause screen's map (<see cref="TryGetCourseFix"/>). Unnamed work is unchanged.
    /// </summary>
    public sealed partial class VoyageRun
    {
        /// <summary>The rung of the last named voyage sailed: the way home after it is as rough (0: none yet).</summary>
        int _voyageRungHome;

        /// <summary>The host lays a named voyage's sea onto its job before the leg is sent (the party sails the same).</summary>
        void LaySea(VoyageJob job)
        {
            if (job is not { IsNamed: true } || !_def || Content == null) return;
            var route = VoyageRules.Route(Content, job.From, job.To, _state) ?? VoyageRules.Route(Content, job.From, job.To);
            var danger = VoyageSea.DangerPerCell(VoyageSea.CourseDanger(Content, route), VoyageSea.RunCells(job.Cells));
            VoyageSea.Lay(job, _def.SeaRung(job.Intensity), danger);
            _voyageRungHome = VoyageSea.Rung(job.Intensity);
        }

        /// <summary>A new leg (from <see cref="PrepareLeg"/>): its crossings counted from the first again.</summary>
        void BeginSea()
        {
            _kaijuCrossing = 0;
            var job = _state?.Current;
            if (job is { IsNamed: true }) _voyageRungHome = VoyageSea.Rung(job.Intensity);
            else if (job is { IsReturn: false }) _voyageRungHome = 0; // unnamed work (Crystal Run) sails home as before
        }

        /// <summary>The Ark's crawl speed that crosses one cell, core to core, in the cell clock less its anchor
        /// (<see cref="VoyageToyDefinitionSO.CellSeconds"/>), never above the crawl ceiling. The corridor's own spacing and
        /// slow radius, the Arkway's two-speed profile (<see cref="VoyageSea.CrossingSeconds"/>).</summary>
        float PacedArkSpeed()
        {
            if (!_def || _arkCfg == null) return _baseArkSpeed;
            float scale = Mathf.Max(1f, _arkCfg.CellScale);
            float radius = CellConveyor.MembraneRadiusFloor * scale;
            float spacing = CellConveyor.CellSpacingFor(CellConveyor.MembraneRadiusFloor, scale, _arkCfg.CellGap);
            float travel = Mathf.Max(10f, _def.CellSeconds - _def.NamedAnchorSeconds);
            return VoyageSea.ArkSpeedFor(spacing, radius, Mathf.Max(1f, _arkCfg.ArkCruiseSpeedFactor), travel,
                _def.ArkCrawlCeiling);
        }

        // ── Salvage: loose crystals adrift ───────────────────────────────────

        /// <summary>True while the job under way gathers loose crystals (a named voyage of a type counted underway).</summary>
        bool Salvaging => _state?.Current is { IsNamed: true, CountsUnderway: true, Verb: WorkVerb.Gather };

        /// <summary>
        /// A Salvage cell is stood: scatter its loose elemental crystals through it, part bunched where a hold burst and
        /// the rest alone, each drifting (<see cref="VoyageDriftCrystal"/>). Seeded by the day, the voyage and the cell, so
        /// every machine in a party scatters the same field. Parented to the cell, so they retire with it.
        /// </summary>
        void SeedDrift(Cell cell, int routeIndex)
        {
            if (!cell || !_def || !Salvaging || _def.DriftCrystalsPerCell <= 0) return;
            var set = ElementalCrystalSetSO.Load();
            if (!set)
            {
                CSDebug.LogWarning("[Voyage] No elemental crystal set (Resources/ElementalCrystalSet) - the salvage cell has no loose crystals.");
                return;
            }
            var job = _state.Current;
            var rng = new System.Random(VoyageRules.Hash(_state.Day, job.VoyageId, "drift" + routeIndex));
            float radius = CellConveyor.MembraneRadiusFloor * Mathf.Max(1f, _arkCfg?.CellScale ?? 1f) * 0.8f;
            Vector3 centre = cell.transform.position;
            // Where the hold burst: somewhere between the core and the rim.
            Vector3 burst = centre + RandomDirection(rng) * radius * Mathf.Lerp(0.3f, 0.7f, (float)rng.NextDouble());
            int count = _def.DriftCrystalsPerCell;
            int bunched = Mathf.RoundToInt(count * Mathf.Clamp01(_def.DriftClusterShare));
            for (int i = 0; i < count; i++)
            {
                var element = ElementalCrystalSetSO.RandomElementFrom(rng);
                var prefab = set.GetPrefab(element);
                if (!prefab) continue;
                Vector3 at = i < bunched
                    ? burst + RandomDirection(rng) * radius * 0.15f * Mathf.Pow((float)rng.NextDouble(), 1f / 3f)
                    : centre + RandomDirection(rng) * radius * Mathf.Pow((float)rng.NextDouble(), 1f / 3f);
                var crystal = Instantiate(prefab, at, Quaternion.identity, cell.transform);
                crystal.transform.localScale = Vector3.one * ElementalCrystalSetSO.CrystalWorldScale / Mathf.Max(1e-3f, cell.transform.lossyScale.x);
                crystal.enabled = true;
                crystal.gameObject.SetActive(true);
                var impactor = crystal.gameObject.AddComponent<ElementalCrystalImpactor>();
                impactor.Crystal = crystal;
                if (set.CollectionEffects is { Length: > 0 }) impactor.SetCollectionEffects(set.CollectionEffects);
                crystal.gameObject.AddComponent<ImpactCollider>().SetImpactor(impactor);
                crystal.gameObject.AddComponent<VoyageDriftCrystal>()
                    .Drift(RandomDirection(rng), _def.DriftSpeed * Mathf.Lerp(0.5f, 1.5f, (float)rng.NextDouble()),
                        (float)rng.NextDouble() * 2f - 1f, centre, radius);
            }
        }

        static Vector3 RandomDirection(System.Random rng)
        {
            float y = (float)(rng.NextDouble() * 2.0 - 1.0);
            float theta = (float)(rng.NextDouble() * Mathf.PI * 2f);
            float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            return new Vector3(ring * Mathf.Cos(theta), y, ring * Mathf.Sin(theta));
        }

        // ── Where the Ark is on its course (the pause screen's map) ──────────

        /// <summary>
        /// Where the Ark is on the leg it sails, for the pause screen's map (Shombith 2026-11-24: the pause menu shows the
        /// map; the screen itself is drawn on Frogma first): the places it sails between, the route, and how far along.
        /// False when no voyage is at sea.
        /// </summary>
        public bool TryGetCourseFix(out VoyageCourseFix fix)
        {
            fix = default;
            var job = _state?.Current;
            if (!_active || !_underway || job == null || !_state.AtSea || Content == null) return false;
            int cell = LegCell(out int cells);
            float within = 0f;
            var ark = Ark;
            var current = CurrentCell;
            if (ark && current && _arkCfg != null)
            {
                float spacing = CellConveyor.CellSpacingFor(CellConveyor.MembraneRadiusFloor, Mathf.Max(1f, _arkCfg.CellScale), _arkCfg.CellGap);
                within = 1f - Mathf.Clamp01(Vector3.Distance(ark.Position, current.transform.position) / Mathf.Max(1f, spacing));
            }
            fix = new VoyageCourseFix
            {
                From = job.From,
                To = job.To,
                Route = VoyageRules.Route(Content, job.From, job.To, _state) ?? VoyageRules.Route(Content, job.From, job.To),
                Cell = cell,
                Cells = cells,
                Progress = VoyageCourseFix.ProgressOf(cell, cells, within),
            };
            return true;
        }
    }
}
