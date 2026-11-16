using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The course genome's placements laid around ANY gate race's own course, for training only: the AIs train in each
    /// hull's own Time game (Docs/AI_SYSTEM/AI_PLAN.md), and a search widens what those games show them by laying the
    /// same placements a Regatta genome carries (every segment spawner and cell world, sized, posed about the spine
    /// and carved by the same rules) round that game's gates. <see cref="Override"/> is null in shipped play; only the
    /// race judge and the trainers set it (Tools/Compute/evaluators/regatta_judge.py, a spec's "genome" with a mode
    /// other than Regatta).
    ///
    /// <para>What a Time game takes from the genome: its placements and its line clearance. Rail features, flora and
    /// circuit genes describe Regatta's own rails and are ignored. The spine is the gates' own polyline, the frame is
    /// taken about the gates' centroid (a placement's bearing is about the course, wherever the cell stands), legs are
    /// rescaled from Regatta's eight to the mode's gate count, and the shell is the mode's own cell.</para>
    /// </summary>
    public static class TrainingCourseOverlay
    {
        /// <summary>The genome to lay round the next gate course raised; null (shipped play) lays nothing.</summary>
        public static RegattaCourseGenome Override;

        /// <summary>The prism the placements are laid with (the judge sets the shipped SpawnablePrism).</summary>
        public static Prism PrismPrefab;

        /// <summary>What the last <see cref="Lay"/> laid, one report per placement, for the judge's notes.</summary>
        public static readonly List<RegattaPlacementReport> LastReports = new();

        /// <summary>Regatta's gate count: a genome's legs are numbered over eight gates.</summary>
        const int RegattaLegs = 8;

        /// <summary>
        /// Lay <see cref="Override"/>'s placements round <paramref name="gates"/> (world positions) under
        /// <paramref name="parent"/>. <paramref name="nucleus"/> / <paramref name="membrane"/> are the cell's shell
        /// about <paramref name="cellCentre"/>. Returns the prisms laid (0 when nothing is set).
        /// </summary>
        public static int Lay(IReadOnlyList<RaceGate> gates, Transform parent, Vector3 cellCentre, float nucleus,
                              float membrane, string ownerName)
        {
            LastReports.Clear();
            var genome = Override;
            if (genome == null || genome.Placements.Count == 0 || gates == null || gates.Count < 2) return 0;

            var output = new List<PrismLay>();
            int laid = Plan(genome, gates, cellCentre, nucleus, membrane, output, LastReports);
            if (laid == 0) return 0;
            if (!PrismPrefab)
            {
                CSDebug.LogWarning($"[{ownerName}] A training course genome is set but no prism prefab is: " +
                                   "its placements are not laid (set TrainingCourseOverlay.PrismPrefab).");
                return 0;
            }
            var trail = new Trail { Dimension = PrismscapeDimension.Volume };
            PrismTrailBuilder.LaySync(PrismPrefab, output, parent, trail, $"{ownerName}::TRAINING", true);
            return laid;
        }

        /// <summary>The pure half of <see cref="Lay"/>: every placement prism, carved, in world space.</summary>
        public static int Plan(RegattaCourseGenome genome, IReadOnlyList<RaceGate> gates, Vector3 cellCentre,
                               float nucleus, float membrane, List<PrismLay> output, List<RegattaPlacementReport> reports)
        {
            // The frame about the gates' centroid: SpineFrame measures "outward" from the origin.
            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < gates.Count; i++) centroid += gates[i].Position;
            centroid /= gates.Count;
            var local = new List<RaceGate>(gates.Count);
            var spine = new List<Vector3>(gates.Count);
            for (int i = 0; i < gates.Count; i++)
            {
                local.Add(new RaceGate(gates[i].Position - centroid, gates[i].Axis, gates[i].Radius));
                spine.Add(gates[i].Position - centroid);
            }

            var scaled = new RegattaCourseGenome { LineClearance = genome.LineClearance };
            float legScale = gates.Count / (float)RegattaLegs;
            foreach (var p in genome.Placements)
            {
                var c = p.Clone();
                c.Leg = p.Leg * legScale;
                scaled.Placements.Add(c);
            }

            var carve = new RegattaPlacementCarve(spine, local, genome.LineClearance, cellCentre - centroid, nucleus,
                                                  membrane);
            var placed = new List<PrismLay>();
            reports.AddRange(RegattaPlacementBuilder.LayAll(scaled, RegattaGeneCatalogue.Shipped, local, carve, placed));
            foreach (var lay in placed)
                output.Add(new PrismLay(new SpawnPoint(lay.Point.Position + centroid, lay.Point.Rotation, lay.Point.Scale),
                                        lay.Domain, lay.Kind));
            return placed.Count;
        }
    }
}
