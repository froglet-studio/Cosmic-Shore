using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The "Connect the Dots" gallery, in the Toy Box: one row per canvas, previewed in miniature
    /// with its live progress. Starting one begins (or resumes) its <see cref="PaintingRunner"/> at
    /// a fixed world anchor - the run, its stroke gates and its rides are play, flown in the world.
    /// A finished masterpiece opens into two choices, Share and Repaint.
    ///
    /// Monument anchors (where each painting is actually flown, out past the membrane) come from
    /// the definition's proximity-first sphere packing, computed once and reused. Runs are filed in
    /// one run book (<see cref="LiveRun"/>), so a canvas is never painted twice at once.
    /// </summary>
    public sealed class PaintingGalleryToy : MatrixToy
    {
        PaintingToyDefinitionSO _def;

        readonly List<PaintingDefinitionSO> _gallery = new();
        Vector3[] _anchorPositions;
        Quaternion[] _anchorRotations;

        public void Configure(PaintingToyDefinitionSO definition) => _def = definition;

        protected override void OnInitialized() => ResolveGalleryList();

        // ── The run book ─────────────────────────────────────────────────────

        /// <summary>Live runs, keyed by painting id.</summary>
        static readonly Dictionary<string, PaintingRunner> ActiveRuns = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => ActiveRuns.Clear();

        /// <summary>This painting's live run, or null.</summary>
        internal static PaintingRunner LiveRun(string paintingId)
        {
            if (string.IsNullOrEmpty(paintingId)) return null;
            if (!ActiveRuns.TryGetValue(paintingId, out var live) || !live)
            {
                ActiveRuns.Remove(paintingId);
                return null;
            }
            return live;
        }

        /// <summary>Start a painting's run under the toybox root and file it in the run book.</summary>
        PaintingRunner CreateRun(int index, int resumeFromStroke)
        {
            var painting = _gallery[index];
            var go = new GameObject($"PaintingRunner_{painting.PaintingId}");
            go.transform.SetParent(transform.parent, false);
            var runner = go.AddComponent<PaintingRunner>();
            string id = painting.PaintingId;
            ActiveRuns[id] = runner;
            // The runner destroys itself right after Finished: drop it from the book.
            runner.Finished += () =>
            {
                if (ActiveRuns.TryGetValue(id, out var r) && r == runner) ActiveRuns.Remove(id);
            };
            runner.Begin(painting, Definition, Context, _anchorPositions[index], _anchorRotations[index],
                resumeFromStroke);
            return runner;
        }

        bool ResolveGallery()
        {
            ResolveGalleryList();
            if (_gallery.Count == 0)
            {
                CSDebug.LogWarning($"[PaintingGallery] '{DisplayName}' has no paintings.");
                return false;
            }

            if (_anchorPositions == null) PackAnchors();
            return true;
        }

        /// <summary>
        /// Fill the gallery list once, at init. Hoisted out of <see cref="ResolveGallery"/> so
        /// <c>ResolvePaintings()</c> is called EXACTLY once for the toy's life: with no authored
        /// list it returns 16 freshly-created definition instances with cold stroke caches every
        /// call, so a second call would silently double the gallery's generation cost.
        /// </summary>
        void ResolveGalleryList()
        {
            if (_gallery.Count > 0) return;
            foreach (var painting in _def.ResolvePaintings())
                if (painting) _gallery.Add(painting);
        }

        /// <summary>
        /// Where each monument actually gets flown: the definition's proximity-first sphere packing
        /// around this toy's slot - every painting as close to the gallery as physics allows, none
        /// interpenetrating, none inside the membrane. Deterministic, so once is enough.
        /// </summary>
        void PackAnchors()
        {
            Vector3 center = Placement.LookTarget;
            Vector3 toSlot = Placement.Position - center;
            float ringRadius = new Vector2(toSlot.x, toSlot.z).magnitude;
            if (ringRadius < 1f) ringRadius = Mathf.Max(1f, toSlot.magnitude);

            var bounds = new Bounds[_gallery.Count];
            for (int i = 0; i < _gallery.Count; i++)
            {
                _gallery[i].EnsureStrokes();
                bounds[i] = _gallery[i].LocalBounds;
            }

            _anchorPositions = new Vector3[_gallery.Count];
            _anchorRotations = new Quaternion[_gallery.Count];
            PaintingToyDefinitionSO.PackMonumentAnchors(bounds, center, Placement.Position, ringRadius,
                _def.PaintingClearance, _anchorPositions, _anchorRotations);
        }

        // ── App-shell face ───────────────────────────────────────────────────

        protected override bool ShellAvailable
        {
            get
            {
                ResolveGalleryList();
                return _gallery.Count > 0;
            }
        }

        /// <summary>
        /// The whole gallery, one row per canvas.
        ///
        /// <para><b>No stroke is generated to draw this list.</b> The late gallery pays a real
        /// curl-noise generation on first stroke access (Phoenix's 260 strokes, Peacock's 236), and
        /// paying it for all sixteen just to open a menu is the wrong trade. So the detail line is
        /// read from the progress STORE and from any live run, never from <c>EnsureStrokes</c>; the
        /// strokes are generated in <see cref="BeginFromShell"/>.</para>
        /// </summary>
        protected override void BuildOptions(List<ToyShellOption> into)
        {
            ResolveGalleryList();
            if (_anchorPositions == null) PackAnchors();

            for (int i = 0; i < _gallery.Count; i++)
            {
                var painting = _gallery[i];
                if (!painting) continue;

                int index = i;
                var live = LiveRun(painting.PaintingId);

                var captured = painting;
                if (!live && PaintingProgressStore.IsFinished(painting.PaintingId))
                {
                    // A finished masterpiece: share it or paint it again, never repaint it silently.
                    into.Add(new ToyShellOption
                    {
                        Payload = captured,
                        Label = painting.DisplayName,
                        Detail = DescribeForShell(painting, null),
                        Description = "Finished. Share it, or clear the canvas and paint it again.",
                        Accent = Definition ? Definition.AccentColor : Color.white,
                        BuildPreview = parent => BuildShellPreview(captured, parent),
                        Expand = () => FinishedChoices(index),
                    });
                    continue;
                }

                into.Add(new ToyShellOption
                {
                    Payload = captured,
                    Label = painting.DisplayName,
                    Detail = DescribeForShell(painting, live),
                    Accent = Definition ? Definition.AccentColor : Color.white,
                    IsCurrent = live,
                    RequiresFreestyle = true,
                    // A painting is something you START - the window closes and the player is
                    // flying its first gate, so "Switch" would promise a thing that stays put.
                    CommitVerb = live && !live.IsCelebrating ? (live.IsBenched ? "Resume" : "Pause") : "Start",
                    Apply = () => BeginFromShell(index),
                    BuildPreview = parent => BuildShellPreview(captured, parent),
                    Arrival = () => ArrivalFor(captured),
                });
            }
        }

        /// <summary>A finished canvas's two choices: Share (export the web reconstruction) and Repaint.</summary>
        List<ToyShellOption> FinishedChoices(int index)
        {
            var painting = index >= 0 && index < _gallery.Count ? _gallery[index] : null;
            if (!painting) return new List<ToyShellOption>();
            Color accent = Definition ? Definition.AccentColor : Color.white;
            return new List<ToyShellOption>
            {
                new()
                {
                    Label = "Share",
                    Detail = "export the masterpiece",
                    Accent = accent,
                    CommitVerb = "Share",
                    Repeatable = true,
                    Apply = () =>
                    {
                        if (PaintingShareExporter.TryExport(painting, Context, out string path))
                            PaintingShareExporter.Share(path, painting.DisplayName);
                    },
                    BuildPreview = parent => BuildShellPreview(painting, parent),
                },
                new()
                {
                    Label = "Repaint",
                    Detail = "clear the canvas and start again",
                    Accent = accent,
                    RequiresFreestyle = true,
                    CommitVerb = "Start",
                    Apply = () => Repaint(index),
                    BuildPreview = parent => BuildShellPreview(painting, parent),
                    Arrival = () => ArrivalFor(painting),
                },
            };
        }

        void Repaint(int index)
        {
            if (!ResolveGallery() || index < 0 || index >= _gallery.Count) return;
            var painting = _gallery[index];
            if (!painting || LiveRun(painting.PaintingId)) return;
            PaintingProgressStore.ResetProgress(painting.PaintingId);
            PaintingPrismStore.Clear(painting.PaintingId);
            painting.EnsureStrokes();
            if (painting.Strokes.Count == 0) return;
            CreateRun(index, 0);
        }

        /// <summary>
        /// Where to fly to paint <paramref name="painting"/>: its live run's next ring (the start
        /// gate, or the ride checkpoint it is on). Invalid with no run to fly to.
        /// </summary>
        ToyArrival ArrivalFor(PaintingDefinitionSO painting)
        {
            if (!this || painting == null) return default;
            var live = LiveRun(painting.PaintingId);
            return live && live.TryGetArrival(out var arrival) ? arrival : default;
        }

        float PreviewRadius =>
            (Placement.BodyRadius > 0.01f ? Placement.BodyRadius : 20f) * Mathf.Max(0.25f, _def.IconScaleBodies);

        /// <summary>
        /// The painting in miniature for the window's picture. Pays the stroke generation for that
        /// one painting; a preview is asked for one painting at a time, never the whole late
        /// gallery at once.
        /// </summary>
        GameObject BuildShellPreview(PaintingDefinitionSO painting, Transform parent)
        {
            if (!painting || !parent) return null;
            var body = new GameObject($"{painting.DisplayName} Preview");
            body.transform.SetParent(parent, false);
            if (MiniaturePaintingBuilder.TryBuild(body.transform, painting, PreviewRadius, Context))
                return body;
            Destroy(body);
            return null;
        }

        static string DescribeForShell(PaintingDefinitionSO painting, PaintingRunner live)
        {
            if (live)
            {
                int pct = Mathf.RoundToInt(100f * live.StrokesCompleted / Mathf.Max(1, live.StrokeCount));
                return live.IsCelebrating ? "masterpiece"
                     : live.IsBenched ? $"{pct}% - paused"
                     : $"{pct}% - painting";
            }

            int times = PaintingProgressStore.GetTimesCompleted(painting.PaintingId);
            return times > 0 ? $"painted x{times}" : "";
        }

        /// <summary>
        /// Start, resume or bench a canvas from the app shell. A finished masterpiece is listed as
        /// a branch (<see cref="FinishedChoices"/>), so it is never repainted from here.
        /// </summary>
        void BeginFromShell(int index)
        {
            if (!ResolveGallery()) return;
            if (index < 0 || index >= _gallery.Count) return;

            var painting = _gallery[index];
            if (!painting) return;

            var live = LiveRun(painting.PaintingId);
            if (live)
            {
                if (!live.IsCelebrating) live.ToggleBench();
                return;
            }

            painting.EnsureStrokes();
            int total = painting.Strokes.Count;
            if (total == 0) return;

            int resume = PaintingProgressStore.GetStrokesCompleted(painting.PaintingId, total);
            if (resume >= total) return; // finished: Share / Repaint (FinishedChoices)

            CreateRun(index, resume);
        }
    }
}
