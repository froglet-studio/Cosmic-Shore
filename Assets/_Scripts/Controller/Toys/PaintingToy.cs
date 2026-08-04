using CosmicShore.ScriptableObjects;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One "connect the dots" painting station. Its label shows the painting's name and live progress;
    /// flying through it starts (or resumes) the painting's <see cref="PaintingRunner"/> at a fixed
    /// world anchor - a monument-in-progress you can leave and come back to (across vessel swaps,
    /// other paintings, other game modes, and sessions - the saved drawing state regrows). Re-flying
    /// the toy while a run is active benches/resumes it ("put the brush down"); once a masterpiece
    /// is finished it offers two fly-through choice gates: SHARE (export the web reconstruction to
    /// the platform share sheet) or REPAINT (clear the canvas and start fresh).
    ///
    /// Toy-faithful: no score, no timer, no fail state - progress is the only readout, and the
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
        Transform _runParent;

        PaintingRunner _runner;
        GameObject _shareGate;
        GameObject _repaintGate;

        /// <summary>
        /// Live runs, keyed by painting id. A run OUTLIVES its station: the gallery matrix folds
        /// away when you fly the gallery toy again, and a canvas in progress must neither be
        /// abandoned nor duplicated when it unfolds. A fresh station re-adopts the run for its
        /// painting instead of starting a second one on the same canvas.
        /// </summary>
        static readonly System.Collections.Generic.Dictionary<string, PaintingRunner> ActiveRuns = new();

        public void Configure(PaintingDefinitionSO painting, Vector3 anchorPosition, Quaternion anchorRotation,
            TMP_Text label, Transform runParent = null)
        {
            _painting = painting;
            _anchorPosition = anchorPosition;
            _anchorRotation = anchorRotation;
            _label = label;
            _runParent = runParent;
        }

        protected override void OnInitialized()
        {
            AdoptLiveRun();
            RefreshLabel();
        }

        /// <summary>Re-attach to this painting's run if one survived a matrix fold.</summary>
        void AdoptLiveRun()
        {
            if (_painting == null) return;
            if (!ActiveRuns.TryGetValue(_painting.PaintingId, out var live) || !live)
            {
                ActiveRuns.Remove(_painting.PaintingId);
                return;
            }
            _runner = live;
            _runner.ProgressChanged += RefreshLabel;
            _runner.Finished += HandleRunnerFinished;
        }

        void OnDestroy()
        {
            // The run keeps going without us (it is parented outside the matrix) - just stop
            // driving a label that is about to be destroyed.
            if (!_runner) return;
            _runner.ProgressChanged -= RefreshLabel;
            _runner.Finished -= HandleRunnerFinished;
        }

        protected override void OnActivated(IVesselStatus localVessel)
        {
            if (_painting == null)
            {
                CosmicShore.Utility.CSDebug.LogWarning("[PaintingToy] No painting assigned - nothing to paint.");
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
                // Finished masterpiece - offer the choice gates instead of acting immediately.
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
            // Parented OUTSIDE the gallery matrix (the toybox root) so folding the matrix away
            // mid-painting leaves the canvas untouched. Falls back to this station's parent when
            // no run parent was supplied (a stand-alone painting station).
            go.transform.SetParent(_runParent ? _runParent : transform.parent, false);
            _runner = go.AddComponent<PaintingRunner>();
            ActiveRuns[_painting.PaintingId] = _runner;
            _runner.ProgressChanged += RefreshLabel;
            _runner.Finished += HandleRunnerFinished;
            _runner.Begin(_painting, Definition, Context, _anchorPosition, _anchorRotation, resumeFromStroke);
            RefreshLabel();
        }

        void HandleRunnerFinished()
        {
            // The runner destroys itself right after this - drop it and re-label from the store.
            if (_painting != null) ActiveRuns.Remove(_painting.PaintingId);
            _runner = null;
            RefreshLabel();
        }

        protected override void Update()
        {
            base.Update(); // the base's exit-gated re-arm - shadowing it would deaden the station

            // The choice gates are a freestyle offer - fold them away when the player returns to
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
            // Choice gates keep a neutral sphere hub - crossing commits a choice, not a trail state,
            // so they must not wear the trail-changer cone.
            return ToyFactory.CreateGate($"Choice_{text}", transform.parent, position, transform.forward,
                ChoiceGateRadius, color, text, hubIsCone: false, null, Definition, Context, _ => onChosen());
        }

        void HandleShareChosen()
        {
            // Gates stay up - the player can share again, or go on to repaint.
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
                        ? $"{_painting.DisplayName}\n{pct}% - PAUSED"
                        : $"{_painting.DisplayName}\n{pct}%";
                return;
            }

            int done = PaintingProgressStore.GetStrokesCompleted(_painting.PaintingId, total);
            int times = PaintingProgressStore.GetTimesCompleted(_painting.PaintingId);
            if (done >= total)
                _label.text = _shareGate || _repaintGate
                    ? $"{_painting.DisplayName}\nSHARE it or REPAINT?"
                    : $"{_painting.DisplayName}\nCOMPLETE - fly through for options";
            else if (done > 0)
                _label.text = $"{_painting.DisplayName}\nresume {Mathf.RoundToInt(100f * done / total)}%";
            else
                _label.text = times > 0
                    ? $"{_painting.DisplayName}\npainted ×{times}"
                    : _painting.DisplayName;
        }
    }
}
