using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The "connect the dots" painting toy - <b>one station that opens into the whole gallery</b>.
    /// Fly it and a matrix of paintings blooms out ahead, one per <see cref="PaintingDefinitionSO"/>,
    /// each a miniature of its own canvas showing its name + live progress. Fly a painting and it
    /// runs at a fixed world anchor out past the membrane: multi-stroke, multi-domain (start gates
    /// recolour the trail via the server-authoritative pick RPC), pen-up between strokes, resumable
    /// across sessions.
    ///
    /// With no paintings authored, the toy ships a default gallery that ladders from a big single
    /// stroke to a monument: Star → Rainbow → Saturn → Taj Mahal. The painted trail is conserved
    /// mass like any other trail - no caps/TTL/culler.
    /// </summary>
    [CreateAssetMenu(fileName = "Toy_Painting", menuName = "ScriptableObjects/Toys/Painting Toy")]
    public class PaintingToyDefinitionSO : ToyDefinitionSO
    {
        [Header("Painting (Connect the Dots)")]
        [SerializeField, Tooltip("The gallery: one painting station spawns per entry. Leave empty for the " +
                                 "built-in 16-painting default gallery (see DefaultGalleryCatalog).")]
        List<PaintingDefinitionSO> paintings = new();

        [SerializeField, Tooltip("Clearance between the membrane and each painting, AND the guaranteed " +
                                 "gap between any two monuments (each occupies its bounding sphere plus " +
                                 "half this margin in the anchor packing), world units.")]
        float paintingClearance = 150f;

        [SerializeField, Tooltip("Gap between gallery stations in the matrix, as a multiple of the body radius.")]
        float clusterSpacingBodies = 3.2f;

        [SerializeField, Min(0.5f), Tooltip("How far out the gallery matrix blooms, in multiples of the station " +
                                            "spacing, measured from the toy along the outward radial (away from " +
                                            "the cell centre). You fly AT the toy and keep going, so this is the " +
                                            "gap you cross before the paintings.")]
        float matrixDistanceFactor = 4f;

        [SerializeField, Min(0.25f), Tooltip("Station size as a multiple of the toy's own body radius. The " +
                                             "station IS a miniature of its painting, so it has to be big " +
                                             "enough to identify without reading the label - which is where " +
                                             "the toybox is heading. Spacing rides along (it is derived from " +
                                             "this radius).")]
        float iconScaleBodies = 2f;

        public float PaintingClearance => paintingClearance;
        public float ClusterSpacingBodies => clusterSpacingBodies;
        public float MatrixDistanceFactor => matrixDistanceFactor;
        public float IconScaleBodies => iconScaleBodies;

        /// <summary>It leaves conserved PRISM MASS behind - a painting drawn in your own trail, which stays
        /// in the cell as ordinary mass the food web can graze.</summary>
        public override ToyCategory Category => ToyCategory.Creation;

        public override void Spawn(Transform parent, ToyPlacement placement, ToyContext context)
        {
            // ONE toy. It unfolds into the gallery matrix on a pass and folds it away on the next -
            // the station layout and the monument anchor packing both live on the runtime toy now.
            var go = ToyFactory.CreateRoot(Id, parent, placement, AccentColor, DisplayName);
            var toy = go.AddComponent<PaintingGalleryToy>();
            toy.Configure(this);
            toy.Initialize(this, context, placement);
        }

        /// <summary>How many leading gallery entries are the on-ramp (packed first, nearest).</summary>
        const int OnRampCount = 4;

        /// <summary>
        /// Proximity-first greedy sphere packing for the monument anchors. Each painting occupies
        /// its bounding sphere (+ half the clearance); anchors are chosen from deterministic
        /// candidate shells around the slot's outward point, ascending - the first shell with a
        /// valid spot wins, taking the candidate nearest the slot. Pack order is HYBRID: the
        /// on-ramp entries first in ladder order (a new player's first paintings sit right at the
        /// stations), then everything else LARGEST-FIRST - a giant packed early sits at its
        /// physical floor (membrane + its own radius) instead of being exiled to the leftovers
        /// (ladder order pushed the Matterhorn ~3.1km out; hybrid holds it to ~2.4km with the
        /// on-ramp unchanged). Never interpenetrating, never through the membrane. Static +
        /// input-driven so tests can run it without a scene.
        /// </summary>
        public static void PackMonumentAnchors(Bounds[] paintingBounds, Vector3 center,
            Vector3 slotPos, float ringRadius, float clearance,
            Vector3[] outPositions, Quaternion[] outRotations)
        {
            Vector3 toSlot = slotPos - center;
            toSlot.y = 0f;
            Vector3 outward = toSlot.sqrMagnitude > 1f ? toSlot.normalized : Vector3.forward;
            Vector3 anchor0 = center + outward * (ringRadius + clearance);
            anchor0.y = slotPos.y;

            const int DirsPerShell = 96;
            const float ShellStep = 250f;
            const int MaxShells = 60;
            Vector3[] dirs = PaintingStrokeToolkit.FibonacciSphere(DirsPerShell);

            var placedCenters = new List<Vector3>(paintingBounds.Length);
            var placedRadii = new List<float>(paintingBounds.Length);

            var order = new List<int>(paintingBounds.Length);
            for (int i = 0; i < paintingBounds.Length; i++) order.Add(i);
            int rampEnd = Mathf.Min(OnRampCount, paintingBounds.Length);
            order.Sort((x, y) =>
            {
                bool rx = x < rampEnd, ry = y < rampEnd;
                if (rx != ry) return rx ? -1 : 1;                 // on-ramp first…
                if (rx) return x.CompareTo(y);                    // …in ladder order
                int bySize = paintingBounds[y].extents.magnitude  // then largest-first
                    .CompareTo(paintingBounds[x].extents.magnitude);
                return bySize != 0 ? bySize : x.CompareTo(y);     // deterministic tie-break
            });

            foreach (int i in order)
            {
                Bounds b = paintingBounds[i];
                if (b.size.sqrMagnitude < 1e-3f) continue; // null/empty painting slot

                float r = b.extents.magnitude + clearance * 0.5f;
                bool placed = false;

                for (int shell = 0; shell <= MaxShells && !placed; shell++)
                {
                    float bestSqr = float.MaxValue;
                    Vector3 bestPos = default;
                    Quaternion bestRot = default;

                    // shell 0 is the single point anchor0 itself
                    int count = shell == 0 ? 1 : DirsPerShell;
                    for (int d = 0; d < count; d++)
                    {
                        Vector3 pos = shell == 0 ? anchor0 : anchor0 + dirs[d] * (shell * ShellStep);
                        Quaternion rot = FaceRing(pos, center);
                        Vector3 sc = pos + rot * b.center;

                        // The whole monument stays outside the membrane…
                        if ((sc - center).magnitude < ringRadius + r) continue;
                        // …and clear of every already-placed monument.
                        bool overlaps = false;
                        for (int j = 0; j < placedCenters.Count; j++)
                            if ((sc - placedCenters[j]).magnitude < r + placedRadii[j]) { overlaps = true; break; }
                        if (overlaps) continue;

                        float sqr = (pos - slotPos).sqrMagnitude;
                        if (sqr < bestSqr) { bestSqr = sqr; bestPos = pos; bestRot = rot; }
                    }

                    if (bestSqr < float.MaxValue)
                    {
                        outPositions[i] = bestPos;
                        outRotations[i] = bestRot;
                        placedCenters.Add(bestPos + bestRot * b.center);
                        placedRadii.Add(r);
                        placed = true;
                    }
                }

                if (!placed)
                {
                    // Unreachable in practice (MaxShells spans 14km) - fail loud but functional.
                    CosmicShore.Utility.CSDebug.LogWarning(
                        $"[{nameof(PaintingToyDefinitionSO)}] monument {i} did not fit in {MaxShells} shells; placing outward.");
                    Vector3 pos = anchor0 + outward * ((MaxShells + i) * ShellStep);
                    outPositions[i] = pos;
                    outRotations[i] = FaceRing(pos, center);
                }
            }
        }

        /// <summary>Yaw-only rotation so the monument's front (+Z authoring side) faces the ring.</summary>
        static Quaternion FaceRing(Vector3 pos, Vector3 center)
        {
            Vector3 h = pos - center;
            h.y = 0f;
            return h.sqrMagnitude > 1e-4f
                ? Quaternion.LookRotation(-h.normalized, Vector3.up)
                : Quaternion.identity;
        }

        /// <summary>The authored gallery, or the built-in default when none is authored.
        /// Public so the runtime gallery toy resolves the same list the editor tool authors.</summary>
        public List<PaintingDefinitionSO> ResolvePaintings()
        {
            var result = new List<PaintingDefinitionSO>();
            foreach (var p in paintings)
                if (p) result.Add(p);
            if (result.Count > 0) return result;
            return BuildDefaultGallery();
        }

        /// <summary>One entry of the default gallery - shared by the runtime fallback and the editor setup tool.</summary>
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
        /// THE default gallery spec - the single source of truth consumed by both the runtime
        /// fallback (<see cref="BuildDefaultGallery"/>) and the editor asset authoring
        /// (ToyboxSetupTool), so tuning a size/reach here cannot silently diverge the two.
        /// </summary>
        public static readonly DefaultPaintingSpec[] DefaultGalleryCatalog =
        {
            // ── On-ramp ──
            new("painting_star", "Star", "One clean stroke - a warm-up canvas.",
                PaintingPreset.Star, 840f, 30f),
            new("painting_rainbow", "Rainbow", "Three bands, three colours - ride the gates.",
                PaintingPreset.Rainbow, 700f, 30f),
            new("painting_saturn", "Saturn", "A planet and its rings, flown in true 3D.",
                PaintingPreset.Saturn, 800f, 30f),
            new("painting_taj_mahal", "Taj Mahal",
                "The monument. Fifty-five strokes, three colours, hours of flying.",
                PaintingPreset.TajMahal, 1100f, 26f),

            // ── Grandiose 3D constructions - beautiful when spun (each dwarfs the Taj) ──
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
            // The catalog entries below describe the PROCEDURAL fallback the presets generate -
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
        /// Code-built default gallery so the toy delivers the full ladder - big simple shape up to
        /// the Taj Mahal - before any painting assets are authored (the editor setup tool authors
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
