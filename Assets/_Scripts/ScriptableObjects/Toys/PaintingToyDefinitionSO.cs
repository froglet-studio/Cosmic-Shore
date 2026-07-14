using System.Collections.Generic;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The "connect the dots" painting toy — a small gallery of painting stations fanned around its
    /// toybox slot, one per <see cref="PaintingDefinitionSO"/>. Each station shows its painting's
    /// name + live progress and, when flown through, runs that painting at a fixed world anchor
    /// just outside the toy ring: multi-stroke, multi-domain (start gates recolour the trail via
    /// the server-authoritative pick RPC), pen-up between strokes, resumable across sessions.
    ///
    /// With no paintings authored, the toy ships a default gallery that ladders from a big single
    /// stroke to a monument: Star → Rainbow → Saturn → Taj Mahal. The painted trail is conserved
    /// mass like any other trail — no caps/TTL/culler.
    /// </summary>
    [CreateAssetMenu(fileName = "Toy_Painting", menuName = "ScriptableObjects/Toys/Painting Toy")]
    public class PaintingToyDefinitionSO : ToyDefinitionSO
    {
        [Header("Painting (Connect the Dots)")]
        [SerializeField, Tooltip("The gallery: one painting station spawns per entry. Leave empty for the " +
                                 "built-in 16-painting default gallery (see DefaultGalleryCatalog).")]
        List<PaintingDefinitionSO> paintings = new();

        [SerializeField, Tooltip("Clearance between the toy ring and the near edge of each painting, AND the " +
                                 "guaranteed gap between adjacent monuments (column/row pitches are derived " +
                                 "from the largest painting's bounds plus this margin), world units.")]
        float paintingClearance = 150f;

        [SerializeField, Tooltip("Gap between gallery stations in the cluster, as a multiple of the body radius.")]
        float clusterSpacingBodies = 3.2f;

        public override void Spawn(Transform parent, ToyPlacement placement, ToyContext context)
        {
            var gallery = ResolvePaintings();
            if (gallery.Count == 0)
            {
                CosmicShore.Utility.CSDebug.LogWarning($"[{nameof(PaintingToyDefinitionSO)}] '{Id}' has no paintings.");
                return;
            }

            // The gallery is a roughly-SQUARE matrix cluster at this definition's slot — columns run
            // along the ring tangent, rows climb vertically (the off-plane space) — each station a
            // miniature of its painting. Monuments anchor radially outward behind their station's
            // column, tiered vertically to match their row.
            Vector3 center = placement.LookTarget;
            Vector3 toSlot = placement.Position - center;
            float ringRadius = new Vector2(toSlot.x, toSlot.z).magnitude;
            if (ringRadius < 1f) ringRadius = Mathf.Max(1f, toSlot.magnitude);
            float baseAngle = Mathf.Atan2(toSlot.x, toSlot.z);

            int cols = Mathf.CeilToInt(Mathf.Sqrt(gallery.Count));
            int rows = Mathf.CeilToInt(gallery.Count / (float)cols);
            float spacing = Mathf.Max(placement.TriggerRadius * 2.2f, placement.BodyRadius * clusterSpacingBodies);

            // Monument pitches are derived from the LARGEST painting so no two can interpenetrate:
            // (w_i + w_j)/2 <= maxWidth for any pair, so maxExtent + clearance guarantees the gap.
            // Monuments are ground-rebased (they occupy anchorY .. anchorY + height), so the row
            // pitch must clear a full height, not a half.
            float maxWidth = 0f, maxHeight = 0f, maxDepth = 0f;
            foreach (var p in gallery)
            {
                if (!p) continue;
                p.EnsureStrokes();
                Bounds b = p.LocalBounds;
                maxWidth = Mathf.Max(maxWidth, b.size.x);
                maxHeight = Mathf.Max(maxHeight, b.size.y);
                maxDepth = Mathf.Max(maxDepth, b.size.z);
            }
            float colPitch = maxWidth * 1.05f + paintingClearance;
            float rowPitch = maxHeight * 1.05f + paintingClearance;

            var outward = new Vector3(Mathf.Sin(baseAngle), 0f, Mathf.Cos(baseAngle));
            var tangent = new Vector3(Mathf.Cos(baseAngle), 0f, -Mathf.Sin(baseAngle));

            for (int i = 0; i < gallery.Count; i++)
            {
                var painting = gallery[i];
                if (!painting) continue;

                int col = i % cols, row = i / cols;
                float cOff = col - (cols - 1) * 0.5f;
                float rOff = row - (rows - 1) * 0.5f;

                // Station: grid cell in the tangent × up plane at the slot, facing the ring centre.
                float a = baseAngle + Mathf.Atan2(cOff * spacing, ringRadius);
                var stationOut = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                Vector3 toyPos = center + stationOut * ringRadius;
                toyPos.y = placement.Position.y + rOff * spacing;

                // Monument: a width-aware WALL of masterpieces behind the cluster — columns step
                // along the ring tangent by the widest painting plus clearance, rows climb the
                // off-plane vertical by the tallest, with a modest radial stagger for depth.
                Bounds bounds = painting.LocalBounds;
                float anchorDistance = ringRadius + paintingClearance + Mathf.Max(40f, bounds.max.z)
                                       + row * 0.35f * Mathf.Max(200f, maxDepth);
                Vector3 anchorPos = center + outward * anchorDistance + tangent * (cOff * colPitch);
                anchorPos.y = placement.Position.y + rOff * rowPitch;
                Quaternion anchorRot = Quaternion.LookRotation(-outward, Vector3.up);

                var root = ToyFactory.CreateBareRoot($"{Id}_{painting.PaintingId}", parent,
                    toyPos, center, placement.TriggerRadius);
                var body = new GameObject("Body");
                body.transform.SetParent(root.transform, false);
                // The station IS its painting in miniature; anonymous sphere only as fallback.
                if (!MiniaturePaintingBuilder.TryBuild(body.transform, painting, placement.BodyRadius, context))
                    ToyFactory.AddSphereBody(body.transform, placement.BodyRadius, AccentColor);
                var label = ToyFactory.AddLabel(root.transform, painting.DisplayName, AccentColor,
                    placement.BodyRadius * 1.9f);

                var toy = root.AddComponent<PaintingToy>();
                toy.Configure(painting, anchorPos, anchorRot, label);
                toy.Initialize(this, context, placement);
            }
        }

        List<PaintingDefinitionSO> ResolvePaintings()
        {
            var result = new List<PaintingDefinitionSO>();
            foreach (var p in paintings)
                if (p) result.Add(p);
            if (result.Count > 0) return result;
            return BuildDefaultGallery();
        }

        /// <summary>One entry of the default gallery — shared by the runtime fallback and the editor setup tool.</summary>
        public readonly struct DefaultPaintingSpec
        {
            public readonly string Id;
            public readonly string Name;
            public readonly string Description;
            public readonly PaintingPreset Preset;
            public readonly float Size;
            public readonly float Reach;

            public DefaultPaintingSpec(string id, string name, string description,
                PaintingPreset preset, float size, float reach)
            {
                Id = id;
                Name = name;
                Description = description;
                Preset = preset;
                Size = size;
                Reach = reach;
            }
        }

        /// <summary>
        /// THE default gallery spec — the single source of truth consumed by both the runtime
        /// fallback (<see cref="BuildDefaultGallery"/>) and the editor asset authoring
        /// (ToyboxSetupTool), so tuning a size/reach here cannot silently diverge the two.
        /// </summary>
        public static readonly DefaultPaintingSpec[] DefaultGalleryCatalog =
        {
            // ── On-ramp ──
            new("painting_star", "Star", "One clean stroke — a warm-up canvas.",
                PaintingPreset.Star, 840f, 30f),
            new("painting_rainbow", "Rainbow", "Three bands, three colours — ride the gates.",
                PaintingPreset.Rainbow, 700f, 30f),
            new("painting_saturn", "Saturn", "A planet and its rings, flown in true 3D.",
                PaintingPreset.Saturn, 800f, 30f),
            new("painting_taj_mahal", "Taj Mahal",
                "The monument. Fifty-five strokes, three colours, hours of flying.",
                PaintingPreset.TajMahal, 1100f, 26f),

            // ── Grandiose 3D constructions — beautiful when spun (each dwarfs the Taj) ──
            new("painting_torus_knot", "Torus Knot",
                "A machine-clean trefoil tube that flows through itself forever.",
                PaintingPreset.TorusKnot, 1000f, 18f),
            new("painting_buckyball", "Buckyball",
                "Exact C60: twelve pentagons, twenty hexagons, thirty double bonds.",
                PaintingPreset.Buckyball, 1000f, 18f),
            new("painting_double_helix", "Double Helix",
                "B-DNA at true proportions: ribbon backbones, ten base pairs per turn.",
                PaintingPreset.DoubleHelix, 900f, 18f),
            new("painting_nautilus", "Nautilus",
                "The real shell: nested whorls, growth-line ribs, tiger striping, the open aperture.",
                PaintingPreset.Nautilus, 900f, 18f),
            new("painting_lotus", "Lotus",
                "Nelumbo anatomy: lily pads, three petal whorls, a stamen ring, the seed pod.",
                PaintingPreset.Lotus, 900f, 18f),
            new("painting_rose", "Rose",
                "A real bloom: recurved petal rims spiralling to a furled heart, sepals below.",
                PaintingPreset.Rose, 900f, 18f),
            new("painting_spiral_galaxy", "Spiral Galaxy",
                "A two-arm grand design: dust lanes, an old-gold bulge, stars streaming along the arms.",
                PaintingPreset.SpiralGalaxy, 1200f, 22f),
            // NOTE: these five ship as BAKED assets (real-reference strokes; provenance +
            // attribution live in the asset descriptions and Tools/PaintingPipeline/README.md).
            // The catalog entries below describe the PROCEDURAL fallback the presets generate —
            // they must not claim reference provenance the fallback geometry doesn't have.
            new("painting_phoenix", "Phoenix",
                "A firebird of feathered wings above an impressionist flame tail.",
                PaintingPreset.Phoenix, 1400f, 24f),
            new("painting_bob_ross", "Almighty Mountain",
                "A mountain vista you fly into: fractal ridges, a mirror lake, and happy little firs.",
                PaintingPreset.BobRossVista, 1500f, 26f),
            new("painting_starry_night", "Starry Night",
                "Step into Van Gogh: a swirling sky shell, star vortices, a cypress flame, a village.",
                PaintingPreset.StarryNight, 1300f, 24f),
            new("painting_lions_head", "Lion's Head",
                "A golden mane of a hundred and sixty curl-field strands around a Ruby-eyed face.",
                PaintingPreset.LionsHead, 1800f, 26f),
            new("painting_peacock", "Peacock",
                "A fanned 3D train of eye-feathers - the toy's magnum opus.",
                PaintingPreset.Peacock, 1300f, 22f),
        };

        /// <summary>
        /// Code-built default gallery so the toy delivers the full ladder — big simple shape up to
        /// the Taj Mahal — before any painting assets are authored (the editor setup tool authors
        /// real assets that replace these).
        /// </summary>
        static List<PaintingDefinitionSO> BuildDefaultGallery()
        {
            var gallery = new List<PaintingDefinitionSO>(DefaultGalleryCatalog.Length);
            foreach (var spec in DefaultGalleryCatalog)
            {
                var p = ScriptableObject.CreateInstance<PaintingDefinitionSO>();
                p.SetRuntimeData(spec.Id, spec.Name, spec.Description, spec.Preset, spec.Size, spec.Reach);
                gallery.Add(p);
            }
            return gallery;
        }
    }
}
