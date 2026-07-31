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
    public sealed class PaintingGalleryToy : MatrixToy
    {
        PaintingToyDefinitionSO _def;

        readonly List<PaintingDefinitionSO> _gallery = new();
        Vector3[] _anchorPositions;
        Quaternion[] _anchorRotations;

        public void Configure(PaintingToyDefinitionSO definition) => _def = definition;

        // ── Layout ───────────────────────────────────────────────────────────

        protected override int StationCount => _gallery.Count;
        protected override float StationRadius => Placement.BodyRadius > 0.01f ? Placement.BodyRadius : 20f;
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
            if (_gallery.Count > 0) return true; // resolved + packed on the first open

            foreach (var painting in _def.ResolvePaintings())
                if (painting) _gallery.Add(painting);

            if (_gallery.Count == 0)
            {
                CSDebug.LogWarning($"[PaintingGallery] '{DisplayName}' has no paintings.");
                return false;
            }

            PackAnchors();
            return true;
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

            var label = ToyFactory.AddLabel(root.transform, painting.DisplayName,
                Definition.AccentColor, radius * 1.9f);

            // A full Toy, not a light matrix station: a painting station owns its own bloom, its
            // exit-gated re-arm (so a bench/resume toggle can't double-fire), and a per-frame
            // Update for the completion choice gates.
            var toy = root.AddComponent<PaintingToy>();
            toy.Configure(painting, _anchorPositions[index], _anchorRotations[index], label, ToyboxRoot);
            toy.Initialize(_def, Context,
                new ToyPlacement(position, transform.position, radius, radius * 1.6f));
        }
    }
}
