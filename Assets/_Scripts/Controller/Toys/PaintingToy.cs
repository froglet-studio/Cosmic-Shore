using CosmicShore.ScriptableObjects;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One "fly by numbers" painting station. Its label shows the painting's name and live progress;
    /// flying through it starts (or resumes) the painting's <see cref="PaintingRunner"/> at a fixed
    /// world anchor — a monument-in-progress you can leave and come back to (across vessel swaps,
    /// other paintings, other game modes, and sessions — the saved drawing state regrows). Re-flying
    /// the toy while a run is active benches/resumes it ("put the brush down"); once a masterpiece
    /// is finished it offers two fly-through choice gates: SHARE (export the web reconstruction to
    /// the platform share sheet) or REPAINT (clear the canvas and start fresh).
    ///
    /// Toy-faithful: no score, no timer, no fail state — progress is the only readout, and the
    /// painted trail is conserved mass like any other.
    /// </summary>
    public class PaintingToy : Toy
    {
        const float ChoiceGateRadius = 24f;
        const float ChoiceDespawnSeconds = 0.45f;
        static readonly Color ShareColor = new(0.30f, 0.85f, 1.00f);

        PaintingDefinitionSO _painting;
        Vector3 _anchorPosition;
        Quaternion _anchorRotation;
        TMP_Text _label;

        PaintingRunner _runner;
        GameObject _shareGate;
        GameObject _repaintGate;

        public void Configure(PaintingDefinitionSO painting, Vector3 anchorPosition, Quaternion anchorRotation,
            TMP_Text label)
        {
            _painting = painting;
            _anchorPosition = anchorPosition;
            _anchorRotation = anchorRotation;
            _label = label;
        }

        protected override void OnInitialized() => RefreshLabel();

        protected override void OnActivated(IVesselStatus localVessel)
        {
            if (_painting == null)
            {
                CosmicShore.Utility.CSDebug.LogWarning("[PaintingToy] No painting assigned — nothing to paint.");
                return;
            }

            if (_runner)
            {
                if (!_runner.IsCelebrating)
                    _runner.ToggleBench();
                RefreshLabel();
                return;
            }

            _painting.EnsureStrokes();
            int total = _painting.Strokes.Count;
            if (total == 0) return;

            int resume = PaintingProgressStore.GetStrokesCompleted(_painting.PaintingId, total);
            if (resume >= total)
            {
                // Finished masterpiece — offer the choice gates instead of acting immediately.
                if (!_shareGate && !_repaintGate)
                    SpawnCompletionChoices();
                RefreshLabel();
                return;
            }

            StartRun(resume);
        }

        void StartRun(int resumeFromStroke)
        {
            DespawnChoices();

            var go = new GameObject($"PaintingRunner_{_painting.PaintingId}");
            go.transform.SetParent(transform.parent, false);
            _runner = go.AddComponent<PaintingRunner>();
            _runner.ProgressChanged += RefreshLabel;
            _runner.Finished += HandleRunnerFinished;
            _runner.Begin(_painting, Definition, Context, _anchorPosition, _anchorRotation, resumeFromStroke);
            RefreshLabel();
        }

        void HandleRunnerFinished()
        {
            // The runner destroys itself right after this — drop it and re-label from the store.
            _runner = null;
            RefreshLabel();
        }

        void Update()
        {
            // The choice gates are a freestyle offer — fold them away when the player returns to
            // the menu, so the lava lamp never drifts through a stale SHARE/REPAINT pair.
            if ((_shareGate || _repaintGate)
                && Context?.IsFreestyleActive != null && !Context.IsFreestyleActive())
                DespawnChoices();
        }

        // ── Completion choices (share / repaint) ─────────────────────────────

        void SpawnCompletionChoices()
        {
            Vector3 forward = transform.forward;
            Vector3 tangent = Vector3.Cross(Vector3.up, forward);
            if (tangent.sqrMagnitude < 1e-4f) tangent = Vector3.right;
            tangent.Normalize();

            float spacing = ChoiceGateRadius * 2.6f;
            _shareGate = SpawnChoiceGate("SHARE", transform.position + tangent * spacing, ShareColor, HandleShareChosen);
            _repaintGate = SpawnChoiceGate("REPAINT", transform.position - tangent * spacing,
                Definition ? Definition.AccentColor : Color.white, HandleRepaintChosen);
        }

        GameObject SpawnChoiceGate(string text, Vector3 position, Color color, System.Action onChosen)
        {
            // Choice gates keep a neutral sphere hub — crossing commits a choice, not a trail state,
            // so they must not wear the trail-changer cone.
            return ToyFactory.CreateGate($"Choice_{text}", transform.parent, position, transform.forward,
                ChoiceGateRadius, color, text, hubIsCone: false, null, Definition, Context, _ => onChosen());
        }

        void HandleShareChosen()
        {
            // Gates stay up — the player can share again, or go on to repaint.
            if (!PaintingShareExporter.TryExport(_painting, Context, out string path)) return;
            PaintingShareExporter.Share(path, _painting.DisplayName);

            // In-world acknowledgment: the gate scale-pops so the pass visibly registered.
            if (_shareGate && _shareGate.TryGetComponent(out SwapToy gateToy))
                gateToy.Rebloom();
        }

        void HandleRepaintChosen()
        {
            PaintingProgressStore.ResetProgress(_painting.PaintingId);
            PaintingPrismStore.Clear(_painting.PaintingId);
            StartRun(0);
        }

        void DespawnChoices()
        {
            if (_shareGate) ToyFactory.ScaleOutAndDestroy(_shareGate, ChoiceDespawnSeconds).Forget();
            if (_repaintGate) ToyFactory.ScaleOutAndDestroy(_repaintGate, ChoiceDespawnSeconds).Forget();
            _shareGate = null;
            _repaintGate = null;
        }

        // ── Label ────────────────────────────────────────────────────────────

        void RefreshLabel()
        {
            if (!_label || _painting == null) return;

            _painting.EnsureStrokes();
            int total = Mathf.Max(1, _painting.Strokes.Count);

            if (_runner)
            {
                int pct = Mathf.RoundToInt(100f * _runner.StrokesCompleted / Mathf.Max(1, _runner.StrokeCount));
                _label.text = _runner.IsCelebrating
                    ? $"{_painting.DisplayName}\nMASTERPIECE"
                    : _runner.IsBenched
                        ? $"{_painting.DisplayName}\n{pct}% — PAUSED"
                        : $"{_painting.DisplayName}\n{pct}%";
                return;
            }

            int done = PaintingProgressStore.GetStrokesCompleted(_painting.PaintingId, total);
            int times = PaintingProgressStore.GetTimesCompleted(_painting.PaintingId);
            if (done >= total)
                _label.text = _shareGate || _repaintGate
                    ? $"{_painting.DisplayName}\nSHARE it or REPAINT?"
                    : $"{_painting.DisplayName}\nCOMPLETE — fly through for options";
            else if (done > 0)
                _label.text = $"{_painting.DisplayName}\nresume {Mathf.RoundToInt(100f * done / total)}%";
            else
                _label.text = times > 0
                    ? $"{_painting.DisplayName}\npainted ×{times}"
                    : _painting.DisplayName;
        }
    }
}
