using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One "fly by numbers" painting station. Its label shows the painting's name and live progress;
    /// flying through it starts (or resumes) the painting's <see cref="PaintingRunner"/> at a fixed
    /// world anchor — a monument-in-progress you can leave and come back to, in this session or the
    /// next. Re-flying the toy while a run is active benches/resumes it ("put the brush down");
    /// flying it after a masterpiece is finished clears the canvas for a repaint.
    ///
    /// Toy-faithful: no score, no timer, no fail state — progress is the only readout, and the
    /// painted trail is conserved mass like any other.
    /// </summary>
    public class PaintingToy : Toy
    {
        PaintingDefinitionSO _painting;
        Vector3 _anchorPosition;
        Quaternion _anchorRotation;
        TMP_Text _label;

        PaintingRunner _runner;

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
                // Finished masterpiece — this pass clears the canvas for a fresh painting.
                PaintingProgressStore.ResetProgress(_painting.PaintingId);
                resume = 0;
            }

            var go = new GameObject($"PaintingRunner_{_painting.PaintingId}");
            go.transform.SetParent(transform.parent, false);
            _runner = go.AddComponent<PaintingRunner>();
            _runner.ProgressChanged += RefreshLabel;
            _runner.Finished += HandleRunnerFinished;
            _runner.Begin(_painting, Definition, Context, _anchorPosition, _anchorRotation, resume);
            RefreshLabel();
        }

        void HandleRunnerFinished()
        {
            // The runner destroys itself right after this — drop it and re-label from the store.
            _runner = null;
            RefreshLabel();
        }

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
                _label.text = $"{_painting.DisplayName}\nCOMPLETE — fly through to repaint";
            else if (done > 0)
                _label.text = $"{_painting.DisplayName}\nresume {Mathf.RoundToInt(100f * done / total)}%";
            else
                _label.text = times > 0
                    ? $"{_painting.DisplayName}\npainted ×{times}"
                    : _painting.DisplayName;
        }
    }
}
