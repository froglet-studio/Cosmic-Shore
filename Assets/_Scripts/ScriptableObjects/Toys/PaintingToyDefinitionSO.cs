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
                                 "built-in default gallery (Star, Rainbow, Saturn, Taj Mahal).")]
        List<PaintingDefinitionSO> paintings = new();

        [SerializeField, Tooltip("Angular gap (degrees) between adjacent painting stations around the ring. " +
                                 "The full 16-painting gallery fans ~120° at 8°.")]
        float anglePerToyDeg = 8f;

        [SerializeField, Tooltip("Clearance between the toy ring and the near edge of each painting, world units.")]
        float paintingClearance = 150f;

        public override void Spawn(Transform parent, ToyPlacement placement, ToyContext context)
        {
            var gallery = ResolvePaintings();
            if (gallery.Count == 0)
            {
                CosmicShore.Utility.CSDebug.LogWarning($"[{nameof(PaintingToyDefinitionSO)}] '{Id}' has no paintings.");
                return;
            }

            // Fan the stations around this definition's slot on the toy ring (same layout maths as
            // the swap-set coordinator), and anchor each painting radially outward behind its station.
            Vector3 center = placement.LookTarget;
            Vector3 toSlot = placement.Position - center;
            float ringRadius = new Vector2(toSlot.x, toSlot.z).magnitude;
            if (ringRadius < 1f) ringRadius = Mathf.Max(1f, toSlot.magnitude);
            float baseAngle = Mathf.Atan2(toSlot.x, toSlot.z);
            float step = anglePerToyDeg * Mathf.Deg2Rad;

            for (int i = 0; i < gallery.Count; i++)
            {
                var painting = gallery[i];
                if (!painting) continue;

                float a = baseAngle + step * (i - (gallery.Count - 1) * 0.5f);
                var outward = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                Vector3 toyPos = center + outward * ringRadius;
                toyPos.y = placement.Position.y;

                // The painting stands upright beyond the ring, front (+Z) facing the ring centre;
                // its near edge lands paintingClearance past the station.
                painting.EnsureStrokes();
                Bounds bounds = painting.LocalBounds;
                float anchorDistance = ringRadius + paintingClearance + Mathf.Max(40f, bounds.max.z);
                Vector3 anchorPos = center + outward * anchorDistance;
                anchorPos.y = placement.Position.y;
                Quaternion anchorRot = Quaternion.LookRotation(-outward, Vector3.up);

                var root = ToyFactory.CreateBareRoot($"{Id}_{painting.PaintingId}", parent,
                    toyPos, center, placement.TriggerRadius);
                var body = new GameObject("Body");
                body.transform.SetParent(root.transform, false);
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
                PaintingPreset.Star, 420f, 30f),
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
            new("painting_phoenix", "Phoenix",
                "A firebird of feathered wings above an impressionist flame tail.",
                PaintingPreset.Phoenix, 1200f, 22f),
            new("painting_bob_ross", "Almighty Mountain",
                "A Bob Ross vista you fly into: fractal ridges, a mirror lake, and happy little firs.",
                PaintingPreset.BobRossVista, 1300f, 24f),
            new("painting_starry_night", "Starry Night",
                "Step into Van Gogh: a swirling sky shell, star vortices, a cypress flame, a village.",
                PaintingPreset.StarryNight, 1300f, 24f),
            new("painting_lions_head", "Lion's Head",
                "A golden mane of a hundred and sixty curl-field strands around a Ruby-eyed face.",
                PaintingPreset.LionsHead, 1100f, 20f),
            new("painting_peacock", "Peacock",
                "A fanned 3D train of eye-feathers — the toy's magnum opus.",
                PaintingPreset.Peacock, 1100f, 20f),
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
