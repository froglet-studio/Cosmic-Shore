#if UNITY_EDITOR

using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Edit-mode + play-mode Scene-view overlay for the network-synced
    /// <see cref="DensityPartitionSystem"/>. Toggled from the
    /// <c>FrogletTools/Toolbox</c> "Density" tab via EditorPrefs so the
    /// overlay survives domain reloads, scene swaps, and toolbox close.
    /// </summary>
    [InitializeOnLoad]
    internal static class DensityPartitionDiagnostics
    {
        // ── EditorPrefs keys (shared with the toolbox tab) ──────────────────
        public const string PrefShowGrid       = "Froglet_DensityShowGrid";
        public const string PrefShowAntiJade   = "Froglet_DensityShowAntiJade";
        public const string PrefShowAntiRuby   = "Froglet_DensityShowAntiRuby";
        public const string PrefShowAntiGold   = "Froglet_DensityShowAntiGold";
        public const string PrefShowOnlyDensest = "Froglet_DensityShowOnlyDensest";
        public const string PrefShowLabels     = "Froglet_DensityShowLabels";

        // ── Domain palette (matches in-game branding) ───────────────────────
        // Anti-X solution colors: complementary to the friendly's brand color
        // so they read as "what the X team is fighting against".
        public static readonly Color JadeColor = new(0.30f, 0.85f, 0.55f, 1f);
        public static readonly Color RubyColor = new(0.95f, 0.30f, 0.40f, 1f);
        public static readonly Color GoldColor = new(0.95f, 0.78f, 0.20f, 1f);

        public static readonly Color GridLineColor    = new(0.45f, 0.45f, 0.55f, 0.18f);
        public static readonly Color GridDensityColor = new(0.85f, 0.85f, 0.95f, 0.55f);

        static DensityPartitionDiagnostics()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        static bool ShowGrid       => EditorPrefs.GetBool(PrefShowGrid, false);
        static bool ShowAntiJade   => EditorPrefs.GetBool(PrefShowAntiJade, true);
        static bool ShowAntiRuby   => EditorPrefs.GetBool(PrefShowAntiRuby, true);
        static bool ShowAntiGold   => EditorPrefs.GetBool(PrefShowAntiGold, true);
        static bool ShowOnlyDensest => EditorPrefs.GetBool(PrefShowOnlyDensest, true);
        static bool ShowLabels     => EditorPrefs.GetBool(PrefShowLabels, true);

        static bool AnythingVisible =>
            ShowGrid || ShowAntiJade || ShowAntiRuby || ShowAntiGold;

        static void OnSceneGUI(SceneView view)
        {
            if (!AnythingVisible) return;
            if (!Application.isPlaying) return;

            DrawGrids();
            DrawSolutions();
        }

        // ────────────────────────────────────────────────────────────────────
        //  Grid wireframe
        // ────────────────────────────────────────────────────────────────────

        static void DrawGrids()
        {
            if (!ShowGrid) return;

            foreach (var cell in Cell.ActiveCells)
            {
                if (cell == null) continue;
                if (cell.countGrids == null) continue;

                // Use the Blue (all-domain wildcard) grid for the wireframe and
                // density heatmap — it's the union of every prism in the cell
                // so it gives the most informative picture.
                if (!cell.countGrids.TryGetValue(Domains.Blue, out var grid) || grid == null)
                    continue;
                if (grid.values == null) continue;

                DrawGridForCell(cell, grid);
            }
        }

        static void DrawGridForCell(Cell cell, BlockCountDensityGrid grid)
        {
            int n = grid.values.GetLength(0);
            float stride = grid.Stride;

            // Outer box.
            var min = grid.origin;
            var size = new Vector3((n - 1) * stride, (n - 1) * stride, (n - 1) * stride);
            Handles.color = GridLineColor;
            Handles.DrawWireCube(min + size * 0.5f, size);

            // Density-occupied cells: scale a small wirecube by occupancy so
            // the eye instantly picks out clusters without over-drawing.
            for (int x = 0; x < n; x++)
            {
                for (int y = 0; y < n; y++)
                {
                    for (int z = 0; z < n; z++)
                    {
                        byte d = grid.values[x, y, z];
                        if (d == 0) continue;

                        var center = grid.MapGridIndicesToCoordinates(new Vector3Int(x, y, z));
                        // Cube size scales with sqrt(density) so a single
                        // saturated cell doesn't blow out the picture.
                        float scale = Mathf.Min(1f, Mathf.Sqrt(d) / 8f);
                        float sz = stride * 0.55f * scale;
                        Handles.color = new Color(GridDensityColor.r, GridDensityColor.g,
                                                  GridDensityColor.b, GridDensityColor.a * scale);
                        Handles.DrawWireCube(center, new Vector3(sz, sz, sz));
                    }
                }
            }
        }

        // ────────────────────────────────────────────────────────────────────
        //  The three anti-domain solutions
        // ────────────────────────────────────────────────────────────────────

        static void DrawSolutions()
        {
            var system = DensityPartitionSystem.Active;
            if (system == null) return;

            if (ShowOnlyDensest)
            {
                // Highlight only the strongest anti-domain solution this tick —
                // useful for at-a-glance "where's the action" debugging.
                var jade = system.GetAntiDomainSolution(Domains.Jade);
                var ruby = system.GetAntiDomainSolution(Domains.Ruby);
                var gold = system.GetAntiDomainSolution(Domains.Gold);
                var winner = jade;
                Color winnerColor = JadeColor;
                Domains friendly = Domains.Jade;

                if (ruby.Density > winner.Density)
                {
                    winner = ruby; winnerColor = RubyColor; friendly = Domains.Ruby;
                }
                if (gold.Density > winner.Density)
                {
                    winner = gold; winnerColor = GoldColor; friendly = Domains.Gold;
                }

                if (winner.HasResult)
                    DrawSolution(friendly, winner, winnerColor, primary: true);

                // Still draw the others faintly so you can see what's losing.
                if (ShowAntiJade && friendly != Domains.Jade && jade.HasResult)
                    DrawSolution(Domains.Jade, jade, JadeColor, primary: false);
                if (ShowAntiRuby && friendly != Domains.Ruby && ruby.HasResult)
                    DrawSolution(Domains.Ruby, ruby, RubyColor, primary: false);
                if (ShowAntiGold && friendly != Domains.Gold && gold.HasResult)
                    DrawSolution(Domains.Gold, gold, GoldColor, primary: false);
                return;
            }

            if (ShowAntiJade)
            {
                var s = system.GetAntiDomainSolution(Domains.Jade);
                if (s.HasResult) DrawSolution(Domains.Jade, s, JadeColor, primary: true);
            }
            if (ShowAntiRuby)
            {
                var s = system.GetAntiDomainSolution(Domains.Ruby);
                if (s.HasResult) DrawSolution(Domains.Ruby, s, RubyColor, primary: true);
            }
            if (ShowAntiGold)
            {
                var s = system.GetAntiDomainSolution(Domains.Gold);
                if (s.HasResult) DrawSolution(Domains.Gold, s, GoldColor, primary: true);
            }
        }

        static void DrawSolution(Domains friendlyDomain, PartitionSolution solution,
                                 Color color, bool primary)
        {
            // Sphere radius scales with density (sqrt to soften extremes) and
            // with the grid stride that produced this answer (so a 60-stride
            // hit looks roughly the same size in world space as a 30-stride hit
            // weighted by density).
            float baseRadius = Mathf.Max(solution.Stride * 0.5f, 4f);
            float densityScale = 1f + Mathf.Sqrt(solution.Density) * 0.25f;
            float radius = baseRadius * densityScale * (primary ? 1f : 0.5f);

            var fill = new Color(color.r, color.g, color.b, primary ? 0.18f : 0.07f);
            var rim = new Color(color.r, color.g, color.b, primary ? 0.95f : 0.45f);

            Handles.color = fill;
            Handles.SphereHandleCap(0, solution.Position, Quaternion.identity,
                                    radius * 2f, EventType.Repaint);

            Handles.color = rim;
            Handles.DrawWireDisc(solution.Position, Vector3.up, radius);
            Handles.DrawWireDisc(solution.Position, Vector3.right, radius);
            Handles.DrawWireDisc(solution.Position, Vector3.forward, radius);

            if (ShowLabels)
            {
                var style = new GUIStyle(EditorStyles.boldLabel)
                {
                    normal = { textColor = color },
                    fontSize = primary ? 12 : 10,
                };
                Handles.Label(solution.Position + Vector3.up * (radius + 2f),
                              $"Anti-{friendlyDomain}\n" +
                              $"density {solution.Density:F0}  cell #{solution.CellId}  v{solution.Version}",
                              style);
            }
        }
    }
}

#endif
