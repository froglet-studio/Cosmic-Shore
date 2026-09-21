using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The ONLY tuning surface for <see cref="ScreenshotDirector"/> — where captures land, how big
    /// they are, and the library of capture concepts they are rolled from.
    ///
    /// <para>Zero-wire by default: with no asset at <c>Resources/ScreenshotDirectorConfig</c> the
    /// director runs on <see cref="ApplyDefaults"/>, so the feature works in a fresh clone with
    /// nothing authored. Create the asset (right-click ▸ Create ▸ ScriptableObjects ▸ Screenshot
    /// Director Config) to point captures at your own folder or to add concepts of your own.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "ScreenshotDirectorConfig",
        menuName = "ScriptableObjects/Utility/Screenshot Director Config", order = 40)]
    public class ScreenshotDirectorConfigSO : ScriptableObject
    {
        /// <summary>Where <see cref="Resolve"/> looks before falling back to code defaults.</summary>
        public const string ResourcePath = "ScreenshotDirectorConfig";

        [Header("Output")]
        [Tooltip("Folder captures are written to. Leave EMPTY for the repo's own git-ignored " +
                 "Recordings folder (in a player build, the app's persistent data path). An " +
                 "absolute path is used as given; a relative one hangs off that same default root.")]
        public string outputFolder = string.Empty;

        [Tooltip("Filename prefix. The concept's name and a timestamp are appended, so a folder of " +
                 "captures is sortable by time and greppable by concept.")]
        public string fileNamePrefix = "CosmicShore";

        [Tooltip("Height of the captured image in pixels; width follows the window's aspect, so a " +
                 "capture is framed exactly like what is on screen. 2160 is 4K-tall.")]
        [Range(480, 4320)] public int captureHeight = 2160;

        [Header("Render")]
        [Tooltip("Layers kept OUT of the shot. Screen-space UI never reaches a RenderTexture at " +
                 "all, so this is what excludes WORLD-space UI - the 3D UI layer.")]
        public LayerMask excludedLayers = (1 << 5) | (1 << 6); // UI, 3D UI

        [Tooltip("Adopt the gameplay camera's post-processing, anti-aliasing and shadows. Off " +
                 "renders the raw scene, which is faster and flatter and rarely what you want.")]
        public bool matchGameQuality = true;

        [Tooltip("Hold the prism occlusion corridor closed for the capture frame. ON is almost " +
                 "always right: the corridor dissolves mass between the CAMERA and the ship, and a " +
                 "capture camera looking from somewhere the pilot is not would punch a hole " +
                 "through the trail the shot is of. See Docs/SCREENSHOT_DIRECTOR.md.")]
        public bool holdOcclusionCorridor = true;

        [Header("Pairs")]
        [Tooltip("Two vessels this far apart (world units) are a PAIR worth photographing " +
                 "together. Below the floor they overlap into one blob; above the ceiling a shot " +
                 "framing both has to pull so far back that neither reads as a ship.")]
        public Vector2 pairSeparation = new Vector2(10f, 30f);

        [Tooltip("How often to take the pair shot WHEN a pair is actually available. 1 = always, " +
                 "0 = never (the Pair concepts are then dead weight). Two ships close together is " +
                 "the rarer and more interesting moment, so this is deliberately high.")]
        [Range(0f, 1f)] public float pairChance = 0.85f;

        [Header("Concepts")]
        [Tooltip("Rolled per capture, weighted. Add your own - a concept is only ranges.")]
        public List<ScreenshotConcept> concepts = new List<ScreenshotConcept>();

        // ───────────────────────── resolution ─────────────────────────

        static ScreenshotDirectorConfigSO _fallback;

        /// <summary>The authored asset if there is one, else a defaults instance (created once).</summary>
        public static ScreenshotDirectorConfigSO Resolve()
        {
            var asset = Resources.Load<ScreenshotDirectorConfigSO>(ResourcePath);
            if (asset != null) return asset;

            if (_fallback == null)
            {
                _fallback = CreateInstance<ScreenshotDirectorConfigSO>();
                _fallback.name = "ScreenshotDirectorConfig (defaults)";
                _fallback.ApplyDefaults();
            }
            return _fallback;
        }

        /// <summary>
        /// The shipped library — eight solo concepts covering the useful vantages on a vessel in
        /// flight, plus three PAIR concepts that only come up when two ships are close together.
        ///
        /// <para>The distances are deliberately mostly INSIDE 150u, where the vessel vision band
        /// leaves a hull rendered as itself (Docs/VESSEL_VISION.md) — the two that break that rule
        /// do it on purpose and say so.</para>
        ///
        /// <para><b>Each solo band is a UNION, not a window.</b> The first cut framed tight; a
        /// pass that wanted more air scaled every band 1.5x, which moved the near edge out with
        /// the far one and quietly deleted the close shots rather than adding to them. Each band
        /// now runs from the tight cut's FLOOR to the roomy cut's CEILING, so one concept rolls
        /// the whole range it has ever been able to frame and the library gets its variety from
        /// the roll instead of from a decision made once at authoring time. The one number that
        /// is not a free scale is Static Tracking Cam's ceiling, held at the vision band's 150u
        /// near edge rather than its arithmetic 165 — <i>a ratio applied to a list of numbers is
        /// not a decision until you check what each number was up against.</i></para>
        /// </summary>
        public void ApplyDefaults()
        {
            concepts = new List<ScreenshotConcept>
            {
                new ScreenshotConcept
                {
                    name = "Over the Shoulder", weight = 1.2f,
                    azimuthDegrees = new Vector2(155f, 205f), elevationDegrees = new Vector2(6f, 22f),
                    distance = new Vector2(12f, 39f), fieldOfView = new Vector2(55f, 68f),
                    rollDegrees = new Vector2(-3f, 3f), aimLeadSeconds = new Vector2(0.05f, 0.30f),
                    framingPitchDegrees = new Vector2(-2f, 4f),
                },
                new ScreenshotConcept
                {
                    name = "Sidecar", weight = 1.4f,
                    azimuthDegrees = new Vector2(60f, 120f), elevationDegrees = new Vector2(-8f, 14f),
                    distance = new Vector2(14f, 51f), fieldOfView = new Vector2(45f, 62f),
                    rollDegrees = new Vector2(-6f, 6f), aimLeadSeconds = new Vector2(0.0f, 0.20f),
                    framingPitchDegrees = new Vector2(-3f, 3f),
                },
                new ScreenshotConcept
                {
                    // The mirror of the above. Two entries rather than one 240-wide range, because
                    // a single range spanning both sides would also roll the useless angles
                    // straight through the hull's nose and tail.
                    name = "Sidecar (port)", weight = 1.4f,
                    azimuthDegrees = new Vector2(240f, 300f), elevationDegrees = new Vector2(-8f, 14f),
                    distance = new Vector2(14f, 51f), fieldOfView = new Vector2(45f, 62f),
                    rollDegrees = new Vector2(-6f, 6f), aimLeadSeconds = new Vector2(0.0f, 0.20f),
                    framingPitchDegrees = new Vector2(-3f, 3f),
                },
                new ScreenshotConcept
                {
                    name = "Oncoming", weight = 1.0f,
                    azimuthDegrees = new Vector2(-28f, 28f), elevationDegrees = new Vector2(-6f, 18f),
                    distance = new Vector2(18f, 67.5f), fieldOfView = new Vector2(38f, 55f),
                    rollDegrees = new Vector2(-5f, 5f), aimLeadSeconds = new Vector2(0f, 0f),
                    framingPitchDegrees = new Vector2(-2f, 2f),
                },
                new ScreenshotConcept
                {
                    // Low, close, and short-lensed: the trail fills the foreground and the hull
                    // sits on top of it.
                    name = "Low Chase", weight = 1.0f,
                    azimuthDegrees = new Vector2(165f, 195f), elevationDegrees = new Vector2(-22f, -4f),
                    distance = new Vector2(8f, 27f), fieldOfView = new Vector2(65f, 82f),
                    rollDegrees = new Vector2(-8f, 8f), aimLeadSeconds = new Vector2(0.10f, 0.35f),
                    framingPitchDegrees = new Vector2(-5f, 0f),
                },
                new ScreenshotConcept
                {
                    name = "Top Down", weight = 0.6f, worldAligned = true,
                    azimuthDegrees = new Vector2(0f, 360f), elevationDegrees = new Vector2(62f, 86f),
                    distance = new Vector2(28f, 105f), fieldOfView = new Vector2(45f, 65f),
                    rollDegrees = new Vector2(-12f, 12f), aimLeadSeconds = new Vector2(0f, 0.15f),
                    framingPitchDegrees = new Vector2(-2f, 2f),
                },
                new ScreenshotConcept
                {
                    // World-aligned, so it reads as a camera planted in the arena that the vessel
                    // flies past, rather than one riding along with it.
                    name = "Static Tracking Cam", weight = 1.1f, worldAligned = true,
                    azimuthDegrees = new Vector2(0f, 360f), elevationDegrees = new Vector2(-14f, 30f),
                    distance = new Vector2(35f, 150f), fieldOfView = new Vector2(28f, 45f),
                    rollDegrees = new Vector2(-3f, 3f), aimLeadSeconds = new Vector2(0.15f, 0.45f),
                    framingPitchDegrees = new Vector2(-4f, 4f),
                },
                new ScreenshotConcept
                {
                    // Deliberately PAST the vessel vision band's near edge: the hull comes back a
                    // flat domain-coloured silhouette against the arena. That is the band working,
                    // not a bug - it is the one concept that photographs the world rather than the ship.
                    name = "Establishing (banded hull)", weight = 0.5f, worldAligned = true,
                    azimuthDegrees = new Vector2(0f, 360f), elevationDegrees = new Vector2(-10f, 35f),
                    distance = new Vector2(220f, 520f), fieldOfView = new Vector2(35f, 60f),
                    rollDegrees = new Vector2(-2f, 2f), aimLeadSeconds = new Vector2(0f, 0.5f),
                    framingPitchDegrees = new Vector2(-5f, 5f),
                },

                // ── PAIR ──────────────────────────────────────────────────────────
                // Drawn only when two vessels are actually inside `pairSeparation`. Their
                // `azimuthDegrees` sweeps the perpendicular bisector plane of the two, so a full
                // 0-360 is a free orbit around the line joining them and every angle on it keeps
                // both ships equidistant. `distance` is a FLOOR: the solve pushes back further
                // whenever that is what it takes to fit both, so these numbers set the CLOSEST a
                // two-shot may be rather than where it will land.
                new ScreenshotConcept
                {
                    name = "Duo Two-Shot", weight = 1.6f, framing = ScreenshotFramingKind.Pair,
                    azimuthDegrees = new Vector2(0f, 360f),
                    distance = new Vector2(30f, 70f), fieldOfView = new Vector2(48f, 62f),
                    rollDegrees = new Vector2(-4f, 4f),
                    framingPitchDegrees = new Vector2(-2f, 2f),
                },
                new ScreenshotConcept
                {
                    // Long lens, well back: the gap between the two compresses and the arena
                    // stacks up behind them.
                    name = "Duo Long Lens", weight = 1.0f, framing = ScreenshotFramingKind.Pair,
                    azimuthDegrees = new Vector2(0f, 360f),
                    distance = new Vector2(90f, 140f), fieldOfView = new Vector2(26f, 38f),
                    rollDegrees = new Vector2(-2f, 2f),
                    framingPitchDegrees = new Vector2(-2f, 2f),
                },
                new ScreenshotConcept
                {
                    // As close as the fit allows, wide, and tilted - the pass reads as fast.
                    name = "Duo Close Pass", weight = 1.2f, framing = ScreenshotFramingKind.Pair,
                    azimuthDegrees = new Vector2(0f, 360f),
                    distance = new Vector2(0f, 0f), fieldOfView = new Vector2(66f, 82f),
                    rollDegrees = new Vector2(-14f, 14f),
                    framingPitchDegrees = new Vector2(-4f, 4f),
                },
            };
        }

        void Reset() => ApplyDefaults();

        // ───────────────────────── queries ─────────────────────────

        /// <summary>
        /// Weighted draw from the library. Returns null when nothing is usable, which the director
        /// reports rather than silently taking a default shot the author never asked for.
        /// </summary>
        public ScreenshotConcept PickConcept(System.Random rng) =>
            PickConcept(rng, ScreenshotFramingKind.Solo);

        /// <summary>
        /// Weighted draw from the concepts of one framing kind. Returns null when that kind has
        /// nothing usable, which the director reports or falls back from rather than silently
        /// taking a shot of a different shape than the one it decided on.
        /// </summary>
        public ScreenshotConcept PickConcept(System.Random rng, ScreenshotFramingKind kind)
        {
            if (concepts == null || concepts.Count == 0) return null;

            float total = 0f;
            foreach (var c in concepts)
                if (Eligible(c, kind)) total += c.weight;

            if (total <= 0f) return null;

            float roll = (float)rng.NextDouble() * total;
            foreach (var c in concepts)
            {
                if (!Eligible(c, kind)) continue;
                roll -= c.weight;
                if (roll <= 0f) return c;
            }

            // Floating-point tail: the loop above can exhaust its budget a hair early.
            for (int i = concepts.Count - 1; i >= 0; i--)
                if (Eligible(concepts[i], kind)) return concepts[i];

            return null;
        }

        static bool Eligible(ScreenshotConcept c, ScreenshotFramingKind kind) =>
            c != null && c.framing == kind && c.IsUsable;

        /// <summary>Whether this config carries any usable concept of a given framing kind.</summary>
        public bool HasConcepts(ScreenshotFramingKind kind)
        {
            if (concepts == null) return false;
            for (int i = 0; i < concepts.Count; i++)
                if (Eligible(concepts[i], kind)) return true;
            return false;
        }

        /// <summary>
        /// The pair separation band, low end first, floored at zero. Two vessels closer than
        /// <c>min</c> overlap into one shape; further than <c>max</c> and a shot holding both
        /// has to pull back until neither reads as a ship.
        /// </summary>
        public void ResolvePairBand(out float min, out float max)
        {
            min = Mathf.Max(0f, Mathf.Min(pairSeparation.x, pairSeparation.y));
            max = Mathf.Max(min, Mathf.Max(pairSeparation.x, pairSeparation.y));
        }

        /// <summary>
        /// The folder captures are written to, as an absolute path. Never throws — a path the OS
        /// rejects falls back to the app's persistent data path, because losing a screenshot to an
        /// unwritable folder is worse than putting it somewhere unexpected.
        /// </summary>
        public string ResolveOutputFolder()
        {
            string root = DefaultRoot();

            if (string.IsNullOrWhiteSpace(outputFolder)) return root;

            string expanded = Environment.ExpandEnvironmentVariables(outputFolder.Trim());
            try
            {
                return Path.IsPathRooted(expanded) ? expanded : Path.Combine(root, expanded);
            }
            catch (ArgumentException)
            {
                return root;
            }
        }

        /// <summary>The folder name every clone's captures land in, at the repository root.</summary>
        public const string DefaultFolderName = "Recordings";

        /// <summary>
        /// Where captures go when nothing is authored: <c>&lt;repo&gt;/Recordings</c>.
        ///
        /// <para>Resolved PER MACHINE rather than hardcoded — in the Editor
        /// <c>Application.dataPath</c> is <c>&lt;repo&gt;/Assets</c>, so its parent is whatever
        /// each person's clone lives in. That is the whole trick: one default that is
        /// <c>C:\Users\Will\source\repos\Cosmic-Shore\Recordings</c> on one machine and the
        /// equivalent on the next, with no per-user setting to get wrong.</para>
        ///
        /// <para><b>Captures are never pushed.</b> <c>/Recordings</c> is already in
        /// <c>.gitignore</c> (line 77), so the folder is private to the machine that made the
        /// shots — which is the point: you keep what you want and nobody else carries the rest.
        /// Do not remove that ignore rule without moving this default with it.</para>
        ///
        /// <para>A player build has no repository, and the folder beside a shipped executable is
        /// routinely unwritable (Program Files), so a build falls through to the persistent data
        /// path.</para>
        /// </summary>
        static string DefaultRoot()
        {
            if (Application.isEditor)
            {
                try
                {
                    var repoRoot = Directory.GetParent(Application.dataPath);
                    if (repoRoot != null) return Path.Combine(repoRoot.FullName, DefaultFolderName);
                }
                catch (Exception)
                {
                    // An unexpected dataPath shape; the persistent path always exists.
                }
            }
            return Path.Combine(Application.persistentDataPath, DefaultFolderName);
        }

        /// <summary>
        /// <c>{prefix}_{concept}_{timestamp}.png</c>. The concept name is in the filename so a
        /// folder of captures says which concepts are earning their weight.
        /// </summary>
        public string BuildFileName(string conceptName, DateTime timestamp)
        {
            string prefix = Sanitize(string.IsNullOrWhiteSpace(fileNamePrefix) ? "CosmicShore" : fileNamePrefix);
            string concept = Sanitize(string.IsNullOrWhiteSpace(conceptName) ? "Shot" : conceptName);
            return $"{prefix}_{concept}_{timestamp:yyyy-MM-dd_HH-mm-ss-fff}.png";
        }

        /// <summary>Strips anything the filesystem would refuse, and collapses spaces to hyphens.</summary>
        public static string Sanitize(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (Array.IndexOf(invalid, c) >= 0) continue;
                builder.Append(c == ' ' ? '-' : c);
            }
            string result = builder.ToString().Trim('-', '.');
            return result.Length == 0 ? "Shot" : result;
        }
    }
}
