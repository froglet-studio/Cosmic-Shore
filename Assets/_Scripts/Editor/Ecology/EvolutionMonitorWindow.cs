using System.Collections.Generic;
using System.IO;
using CosmicShore.Editor.Froglet;
using CosmicShore.Gameplay;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor.Ecology
{
    /// <summary>
    /// The live view of a cell's <see cref="EvolutionLedger"/> (Docs/EVOLUTION.md §6): per species, the trait
    /// distribution of the living population, its drift since the first census, lineages, generations, and the
    /// deaths by cause that are doing the selecting. In Play mode it follows the scene's cells; the Export button
    /// writes the ledger's JSON for the doc tables and the Darwin Lab's "from the game" tab.
    ///
    /// A READER: it draws what the ledger already recorded and writes nothing into the project (the export goes
    /// wherever the save panel points), so it carries no change ledger and no ship panel (Docs/TOOLING.md § "Tool
    /// output is a deliverable" - a report is not a deliverable the branch has to carry).
    /// </summary>
    public class EvolutionMonitorWindow : EditorWindow
    {
        [MenuItem("FrogletTools/Ecology/Evolution Monitor")]
        [FrogletTool(FrogletToolCategory.Ecology, Importance = 4,
            Description = "Watch a cell's lifeforms evolve in Play mode: per-species trait distributions, " +
                          "lineages, generations and deaths by cause from the EvolutionLedger, with a JSON export. " +
                          "Needs CellConfigDataSO.Evolution.Enabled on the biome.",
            DocPath = "Docs/EVOLUTION.md")]
        public static void Open()
        {
            var window = GetWindow<EvolutionMonitorWindow>(false, "Evolution Monitor");
            window.minSize = new Vector2(620f, 420f);
            window.Show();
        }

        static readonly Color[] LocusColors =
        {
            FrogletEditorPalette.Coral,   // tempo
            FrogletEditorPalette.Cyan,    // reach
            FrogletEditorPalette.Gold,    // fecundity
            FrogletEditorPalette.Violet,  // cohesion
        };

        static readonly string[] CauseLabels = { "starved", "eaten", "vessel", "jousted", "teardown", "other" };

        Vector2 _scroll;
        int _cellIndex;
        double _nextRepaint;
        readonly List<EvolutionLedger.Individual> _livingScratch = new();

        void OnEnable() => EditorApplication.update += Tick;
        void OnDisable() => EditorApplication.update -= Tick;

        void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            if (EditorApplication.timeSinceStartup < _nextRepaint) return;
            _nextRepaint = EditorApplication.timeSinceStartup + 0.5;
            Repaint();
        }

        void OnGUI()
        {
            FrogletEditorPalette.Banner("Evolution Monitor",
                "Heritable traits, lineages and the deaths that select them - per cell, per species",
                FrogletEditorPalette.ColorFor(FrogletToolCategory.Ecology));

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.HelpBox(
                    "Enter Play mode in a scene with a Cell whose CellConfigDataSO has Evolution > Enabled on. " +
                    "The ledger fills as lifeforms are seeded, born and die; a census row lands every " +
                    "SnapshotIntervalSeconds (30 s by default). Docs/EVOLUTION.md.",
                    MessageType.Info);
                return;
            }

            var cells = FindObjectsByType<Cell>(FindObjectsSortMode.InstanceID);
            if (cells.Length == 0)
            {
                EditorGUILayout.HelpBox("No Cell in the open scenes.", MessageType.Warning);
                return;
            }

            if (cells.Length > 1)
            {
                var names = new string[cells.Length];
                for (int i = 0; i < cells.Length; i++)
                    names[i] = $"Cell {cells[i].ID} ({(cells[i].Config ? cells[i].Config.CellName : "no config")})";
                _cellIndex = Mathf.Clamp(_cellIndex, 0, cells.Length - 1);
                _cellIndex = EditorGUILayout.Popup("Cell", _cellIndex, names);
            }
            else _cellIndex = 0;

            var cell = cells[_cellIndex];
            var settings = cell.Evolution;
            if (settings == null || !settings.Enabled)
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.HelpBox(
                    $"Cell {cell.ID}'s biome ({(cell.Config ? cell.Config.CellName : "none")}) has Evolution > Enabled OFF: " +
                    "every lifeform carries the founder genome and nothing is recorded. Turn it on in the " +
                    "CellConfigDataSO (it is off in every shipped biome) and re-enter Play mode.",
                    MessageType.Warning);
                return;
            }

            var ledger = cell.EvolutionLedger;
            DrawSummaryRow(cell, ledger, settings);
            FrogletEditorPalette.HorizontalRule();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            var species = ledger.Species;
            if (species.Count == 0)
                EditorGUILayout.LabelField("No lifeform recorded yet - the seeder's first wave fills the ledger.", FrogletEditorPalette.CardBody);
            for (int i = 0; i < species.Count; i++)
                DrawSpecies(ledger, species[i], settings);
            EditorGUILayout.EndScrollView();
        }

        void DrawSummaryRow(Cell cell, EvolutionLedger ledger, EvolutionSettings settings)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                $"Cell {cell.ID} · {(cell.Config ? cell.Config.CellName : "")} · living {ledger.LivingCount} · " +
                $"mutation σ {settings.MutationSigma:0.000} · founder spread {settings.FounderSpread:0.00} · " +
                $"census every {settings.SnapshotIntervalSeconds:0} s",
                FrogletEditorPalette.Subtitle);
            GUILayout.FlexibleSpace();
            if (FrogletEditorPalette.ColorButton("Export JSON", FrogletEditorPalette.Info, 100f,
                    tooltip: "Write the ledger (every species, every census row) as JSON for the doc tables and the Darwin Lab."))
            {
                var path = EditorUtility.SaveFilePanel("Export evolution ledger", "",
                    $"evolution-cell{cell.ID}-{System.DateTime.Now:yyyyMMdd-HHmmss}.json", "json");
                if (!string.IsNullOrEmpty(path))
                    File.WriteAllText(path, ledger.ToJson($"Cell {cell.ID} {(cell.Config ? cell.Config.CellName : "")}"));
            }
            EditorGUILayout.EndHorizontal();
        }

        void DrawSpecies(EvolutionLedger ledger, string species, EvolutionSettings settings)
        {
            var rows = ledger.SnapshotsOf(species);
            int living = ledger.LivingOf(species);
            var rect = EditorGUILayout.BeginVertical();
            GUILayout.Space(6f);
            FrogletEditorPalette.DrawCard(new Rect(rect.x, rect.y + 2f, rect.width, Mathf.Max(0f, rect.height - 4f)),
                FrogletEditorPalette.SurfaceRaised, FrogletEditorPalette.Muted.WithAlpha(0.35f));
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8f);
            EditorGUILayout.LabelField(species, FrogletEditorPalette.CardTitle, GUILayout.Width(260f));
            var pillRect = GUILayoutUtility.GetRect(90f, 18f, GUILayout.Width(90f));
            FrogletEditorPalette.StatusPill(pillRect, $"{living} LIVING", living > 0 ? FrogletEditorPalette.Ok : FrogletEditorPalette.Error);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(
                $"founders {ledger.TotalFoundersOf(species)} · births {ledger.TotalBirthsOf(species)} · " +
                $"max generation {ledger.MaxGenerationOf(species)}",
                FrogletEditorPalette.CardBody);
            GUILayout.Space(8f);
            EditorGUILayout.EndHorizontal();

            // deaths by cause - the selecting forces
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8f);
            var deaths = new System.Text.StringBuilder("deaths: ");
            for (int c = 0; c < EvolutionLedger.CauseCount; c++)
            {
                long n = ledger.TotalDeathsOf(species, (LifeformDeathCause)c);
                if (c > 0) deaths.Append(" · ");
                deaths.Append(CauseLabels[c]).Append(' ').Append(n);
            }
            EditorGUILayout.LabelField(deaths.ToString(), FrogletEditorPalette.CardBody);
            EditorGUILayout.EndHorizontal();

            // the living population's genes, as a histogram per locus, plus the mean's trace over the census rows
            _livingScratch.Clear();
            ledger.CollectLiving(species, _livingScratch);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8f);
            for (int l = 0; l < LifeformGenome.LocusCount; l++)
            {
                EditorGUILayout.BeginVertical(GUILayout.Width(140f));
                DrawLocus(l, _livingScratch, rows, settings);
                EditorGUILayout.EndVertical();
                GUILayout.Space(6f);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(8f);
            EditorGUILayout.EndVertical();
        }

        void DrawLocus(int locus, List<EvolutionLedger.Individual> living, IReadOnlyList<TraitSnapshot> rows, EvolutionSettings settings)
        {
            var accent = FrogletEditorPalette.Adapt(LocusColors[locus]);
            string label = LifeformGenome.LocusLabel((GenomeLocus)locus);

            // header: mean ± sd of the living, and the drift from the first census row
            double sum = 0, sq = 0;
            int n = living.Count;
            for (int i = 0; i < n; i++) { double g = living[i].Genome[locus]; sum += g; sq += g * g; }
            double mean = n > 0 ? sum / n : 0, sd = n > 1 ? Mathf.Sqrt(Mathf.Max(0f, (float)(sq / n - mean * mean))) : 0;
            string drift = rows.Count > 0 ? $"  Δ{mean - rows[0].Mean[locus]:+0.00;-0.00}" : "";
            EditorGUILayout.LabelField($"{label}  {mean:+0.00;-0.00} ± {sd:0.00}{drift}", FrogletEditorPalette.CardBody);

            // histogram over [-1, 1] in 16 bins
            var hist = GUILayoutUtility.GetRect(140f, 44f, GUILayout.Width(140f), GUILayout.Height(44f));
            FrogletEditorPalette.DrawRect(hist, FrogletEditorPalette.Surface);
            const int bins = 16;
            var counts = new int[bins];
            int peak = 1;
            for (int i = 0; i < n; i++)
            {
                int b = Mathf.Clamp((int)((living[i].Genome[locus] + 1f) * 0.5f * bins), 0, bins - 1);
                counts[b]++;
                if (counts[b] > peak) peak = counts[b];
            }
            float bw = hist.width / bins;
            for (int b = 0; b < bins; b++)
            {
                if (counts[b] == 0) continue;
                float h = hist.height * counts[b] / peak;
                FrogletEditorPalette.DrawRect(new Rect(hist.x + b * bw + 1f, hist.yMax - h, bw - 2f, h), accent.WithAlpha(0.85f));
            }
            // the founder line (gene 0)
            FrogletEditorPalette.DrawRect(new Rect(hist.x + hist.width * 0.5f - 0.5f, hist.y, 1f, hist.height), FrogletEditorPalette.Muted.WithAlpha(0.6f));

            // the mean's trace over the census rows, with a ± sd band
            var trace = GUILayoutUtility.GetRect(140f, 30f, GUILayout.Width(140f), GUILayout.Height(30f));
            FrogletEditorPalette.DrawRect(trace, FrogletEditorPalette.Surface);
            FrogletEditorPalette.DrawRect(new Rect(trace.x, trace.y + trace.height * 0.5f - 0.5f, trace.width, 1f), FrogletEditorPalette.Muted.WithAlpha(0.4f));
            int count = rows.Count;
            if (count > 0)
            {
                int from = Mathf.Max(0, count - 140);
                int shown = count - from;
                float step = trace.width / Mathf.Max(1, shown);
                for (int i = from; i < count; i++)
                {
                    var row = rows[i];
                    if (row.Population == 0) continue;
                    float x = trace.x + (i - from) * step;
                    float yMean = trace.y + trace.height * (1f - (row.Mean[locus] + 1f) * 0.5f);
                    float half = trace.height * row.StdDev[locus] * 0.5f;
                    FrogletEditorPalette.DrawRect(new Rect(x, Mathf.Max(trace.y, yMean - half), Mathf.Max(1f, step), Mathf.Min(trace.height, half * 2f)), accent.WithAlpha(0.18f));
                    FrogletEditorPalette.DrawRect(new Rect(x, yMean - 1f, Mathf.Max(1f, step), 2f), accent);
                }
            }
            EditorGUILayout.LabelField(
                locus == (int)GenomeLocus.Tempo ? $"pace x{settings.TempoPaceRange:0.00} at +1, upkeep x{settings.TempoUpkeepRange:0.00}" :
                locus == (int)GenomeLocus.Reach ? $"radius x{settings.ReachRadiusRange:0.00} at +1, upkeep x{settings.ReachUpkeepRange:0.00}" :
                locus == (int)GenomeLocus.Fecundity ? $"births x{settings.FecundityRange:0.00} at +1, children born 1/{settings.FecundityProvisionRange:0.0} full" :
                $"cohesion radius x{settings.CohesionRange:0.00} at +1",
                EditorStyles.miniLabel);
        }
    }
}
