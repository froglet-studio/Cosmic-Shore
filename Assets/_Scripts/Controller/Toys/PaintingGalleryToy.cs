using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The "Connect the Dots" gallery: <b>one toy that opens into the whole collection</b>. Fly it
    /// and a matrix of paintings blooms out ahead - each station a miniature of its own canvas
    /// with its name and live progress. Fly a painting and its <see cref="PaintingRunner"/> starts
    /// (or resumes) at a fixed world anchor.
    ///
    /// Sixteen permanently-visible stations fanned around the membrane was clutter; unfolding them
    /// on demand makes the gallery one thing you can pick up and put down. **A run outlives the
    /// matrix**: the runner is parented to the toybox root, not the grid, and
    /// <see cref="PaintingToy"/> re-adopts a live run when the matrix is re-opened - so folding the
    /// gallery away mid-painting never abandons or duplicates a canvas.
    ///
    /// Monument anchors (where each painting is actually flown, out past the membrane) come from
    /// the definition's proximity-first sphere packing, computed once on the first open and reused.
    /// </summary>
    public sealed class PaintingGalleryToy : MatrixToy, IToyShellSurface
    {
        PaintingToyDefinitionSO _def;

        readonly List<PaintingDefinitionSO> _gallery = new();
        Vector3[] _anchorPositions;
        Quaternion[] _anchorRotations;

        public void Configure(PaintingToyDefinitionSO definition) => _def = definition;

        // ── The toy's own emblem: the gallery in four canvases ───────────────

        int[] _emblemOrder;

        protected override void OnInitialized()
        {
            ResolveGalleryList();
            AttachEmblem(new EmblemSource(this), 6f);
        }

        /// <summary>
        /// The gallery in one glyph: the CORE is the on-ramp painting you have taken furthest,
        /// the SATELLITES are the rest of the on-ramp - four real canvases in signature strokes,
        /// each in its own domain colours. This is the only multi-coloured emblem in the toybox,
        /// which is itself an identity channel.
        ///
        /// Deliberately restricted to the four on-ramp entries: the late gallery (Phoenix's 260
        /// strokes, Peacock's 236, Starry Night's 173) pays a real curl-noise generation on first
        /// stroke access, and an icon is never a reason to pay it.
        /// </summary>
        sealed class EmblemSource : ToyEmblem.IEmblemSource
        {
            readonly PaintingGalleryToy _toy;
            public EmblemSource(PaintingGalleryToy toy) => _toy = toy;

            public int SatelliteCount => 3;

            // Strokes carry their own domain colours through ToyFactory's shared line material.
            public bool UsesSharedMaterial => false;

            public bool TryBuildSlot(int slot, Transform holder, float radius, Material shared, out bool heavy)
            {
                // Slot 0 also forces the on-ramp's stroke generation (which the gallery pays
                // anyway on first open - this is a prepay, not an addition).
                heavy = slot == 0;

                var painting = _toy.EmblemPainting(slot);
                if (!painting) return false;

                // Parents itself under the holder, brings its own turntable, and paints with the
                // shared vertex-coloured line material - so it allocates nothing and ignores
                // `shared`, keeping every stroke's own domain colour.
                return MiniaturePaintingBuilder.TryBuild(holder, painting, radius, _toy.Context);
            }

            public bool TryGetLiveKey(out object key)
            {
                key = null;
                return false; // the gallery's line-up doesn't change while the toy exists
            }

            public bool TryGetLiveTint(out Color tint)
            {
                tint = default;
                return false; // per-stroke domain colours, never one tint
            }
        }

        /// <summary>
        /// Which on-ramp painting goes in which emblem slot. Core = the one with the most strokes
        /// completed (ties to the earliest in ladder order); satellites = the others, in order.
        /// </summary>
        PaintingDefinitionSO EmblemPainting(int slot)
        {
            _emblemOrder ??= BuildEmblemOrder();
            return slot >= 0 && slot < _emblemOrder.Length ? _gallery[_emblemOrder[slot]] : null;
        }

        int[] BuildEmblemOrder()
        {
            const int onRamp = 4;
            int count = Mathf.Min(onRamp, _gallery.Count);
            if (count <= 0) return System.Array.Empty<int>();

            int best = 0, bestProgress = -1;
            for (int i = 0; i < count; i++)
            {
                var painting = _gallery[i];
                if (!painting) continue;
                int total = painting.Strokes?.Count ?? 0;
                int done = total > 0 ? PaintingProgressStore.GetStrokesCompleted(painting.PaintingId, total) : 0;
                if (done <= bestProgress) continue;
                bestProgress = done;
                best = i;
            }

            var order = new int[count];
            order[0] = best;
            int next = 1;
            for (int i = 0; i < count && next < count; i++)
                if (i != best) order[next++] = i;
            return order;
        }

        // ── Layout ───────────────────────────────────────────────────────────

        protected override int StationCount => _gallery.Count;
        protected override float StationRadius =>
            (Placement.BodyRadius > 0.01f ? Placement.BodyRadius : 20f) * Mathf.Max(0.25f, _def.IconScaleBodies);
        protected override float MatrixDistanceFactor => _def.MatrixDistanceFactor;

        protected override float StationSpacing =>
            Mathf.Max(Placement.TriggerRadius * 2.2f, StationRadius * _def.ClusterSpacingBodies);

        protected override void OnActivated(IVesselStatus localVessel)
        {
            if (IsMatrixOpen)
            {
                CloseMatrix();
                return;
            }

            // Resolve the gallery BEFORE the base opens - StationCount reads from it.
            if (!ResolveGallery()) return;
            base.OnActivated(localVessel);
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
        /// call, so a second call would silently double the gallery's generation cost and hand the
        /// emblem different objects than the stations use.
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

        ToyDefinitionSO IToyShellSurface.ShellDefinition => Definition;

        bool IToyShellSurface.ShellAvailable => _gallery.Count > 0;

        /// <summary>
        /// The whole gallery, one row per canvas - the flat twin of the matrix of miniatures.
        ///
        /// <para><b>No stroke is generated to draw this list.</b> The late gallery pays a real
        /// curl-noise generation on first stroke access (Phoenix's 260 strokes, Peacock's 236),
        /// which is why the toy's own emblem is restricted to the four on-ramp canvases - and
        /// paying it for all sixteen just to open a menu would be worse than paying it to fly in.
        /// So the detail line is read from the progress STORE and from any live run, never from
        /// <c>EnsureStrokes</c>; the strokes are generated in <see cref="BeginFromShell"/>, at the
        /// same moment flying the gallery would have generated them.</para>
        /// </summary>
        void IToyShellSurface.BuildShellOptions(List<ToyShellOption> into)
        {
            for (int i = 0; i < _gallery.Count; i++)
            {
                var painting = _gallery[i];
                if (!painting) continue;

                int index = i;
                var live = PaintingToy.LiveRun(painting.PaintingId);

                into.Add(new ToyShellOption
                {
                    Label = painting.DisplayName,
                    Detail = DescribeForShell(painting, live),
                    Accent = Definition ? Definition.AccentColor : Color.white,
                    IsCurrent = live,
                    RequiresFreestyle = true,
                    Apply = () => BeginFromShell(index),
                });
            }
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
        /// Start, resume or bench a canvas from the app shell - the same decision tree
        /// <see cref="PaintingToy.OnActivated"/> walks, against the same run book, so the two
        /// surfaces cannot disagree about what a press does.
        ///
        /// <para>The one case that cannot be answered flat is a FINISHED masterpiece: its SHARE
        /// and REPAINT choices are fly-through gates in the world, so the shell opens the gallery
        /// matrix and lets the player fly the station that carries them.</para>
        /// </summary>
        void BeginFromShell(int index)
        {
            if (!ResolveGallery()) return;
            if (index < 0 || index >= _gallery.Count) return;

            var painting = _gallery[index];
            if (!painting) return;

            var live = PaintingToy.LiveRun(painting.PaintingId);
            if (live)
            {
                if (!live.IsCelebrating) live.ToggleBench();
                return;
            }

            painting.EnsureStrokes();
            int total = painting.Strokes.Count;
            if (total == 0) return;

            int resume = PaintingProgressStore.GetStrokesCompleted(painting.PaintingId, total);
            if (resume >= total)
            {
                // Finished: the choice gates are world objects on the station, so hand the player
                // the gallery rather than silently repainting over a masterpiece.
                OpenMatrix();
                return;
            }

            PaintingToy.CreateRun(painting, Definition, Context,
                _anchorPositions[index], _anchorRotations[index], ToyboxRoot, resume);
        }

        // ── Stations: the painting, and nothing but the painting ─────────────

        protected override void BuildStation(int index, Transform parent, Vector3 position, float radius)
        {
            var painting = _gallery[index];

            var root = ToyFactory.CreateBareRoot($"{Definition.Id}_{painting.PaintingId}", parent,
                position, transform.position, radius * 1.6f);

            // The station IS its painting in miniature; anonymous sphere only as fallback.
            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            if (!MiniaturePaintingBuilder.TryBuild(body.transform, painting, radius, Context))
                ToyFactory.AddSphereBody(body.transform, radius, Definition.AccentColor);

            float ringRadius = StationRingRadius(radius * 1.6f);
            var label = ToyFactory.AddRingedLabel(root.transform, painting.DisplayName,
                Definition.AccentColor, ringRadius, radius);

            // A full Toy, not a light matrix station: a painting station owns its own bloom, its
            // exit-gated re-arm (so a bench/resume toggle can't double-fire), and a per-frame
            // Update for the completion choice gates.
            var toy = root.AddComponent<PaintingToy>();
            toy.Configure(painting, _anchorPositions[index], _anchorRotations[index], label, ToyboxRoot);
            // Its switch ring comes from the base like every other toy's - only the radius is ours,
            // because a gallery station's trigger overruns half the gap to its neighbour.
            toy.ConfigureSwitchRing(ringRadius);
            toy.Initialize(_def, Context,
                new ToyPlacement(position, transform.position, radius, radius * 1.6f));
        }
    }
}
