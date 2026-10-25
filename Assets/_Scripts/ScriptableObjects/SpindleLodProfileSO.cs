using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The spindle LOD's one asset, at <c>Resources/SpindleLodProfile</c> (Docs/SPINDLE_LOD.md).
    ///
    /// It has two halves with two owners. The TUNING half (the tier bands, the thread's look, the
    /// cadence) is the designer's: <c>Tools/Build/author_spindle_lod_meshes.py</c> writes it once,
    /// when the asset does not exist, and never again. The MEASURED half (the template materials,
    /// and per source mesh its two LOD meshes, its girth, its strand radius and the bounds Unity's
    /// import must produce) belongs to that generator and is rewritten on every run, with
    /// <c>--check</c> as the drift gate — never hand-edit it.
    ///
    /// Every distance is in GIRTHS: camera distance over the braid's own world-space width. A
    /// spindle is a wire, and a wire's visibility is its thickness on screen, not its length or its
    /// bounding sphere, so a girth count means the same picture on a gyroid branch, a
    /// lattice-stretched limb and a tadpole body. At the reference display (1080p, 60 degree FOV) a
    /// flora branch's girth is 1.40 units and one girth-count of 100 is ~140 world units.
    /// </summary>
    [CreateAssetMenu(fileName = "SpindleLodProfile", menuName = "ScriptableObjects/Environment/Spindle LOD Profile")]
    public class SpindleLodProfileSO : ScriptableObject
    {
        public const string ResourcePath = "SpindleLodProfile";

        /// <summary>How the LOD shows itself (Docs/SPINDLE_LOD.md §2 and §7).</summary>
        public enum Style
        {
            /// <summary>The LOD hides itself: bands in girths, the braid converges into one thread.</summary>
            Covert = 0,
            /// <summary>The LOD IS the effect: in lockstep with the prisms' squared-distance ramp, the braid
            /// opens face by face as hard as danger mass and a wave travels through it
            /// (<c>IncorporealDepth.hlsl</c>, tuned on <c>Resources/IncorporealDepth</c>).</summary>
            Overt = 1,
        }

        [Header("Style")]
        [Tooltip("COVERT hides the LOD (the braid converges into a thread; bands below in girths). OVERT makes it the effect: the tiers swap exactly where the prisms' own depth ramp says the braid is closed or fully open, and the braid opens like danger mass with a wave running through it (bands from the prisms + the overt fade below; the look on Resources/IncorporealDepth). Switchable in Play.")]
        [SerializeField] Style style = Style.Overt;
        public Style LookStyle => style;

        [Header("System")]
        [Tooltip("Master switch. OFF returns every spindle to its authored braid and stops the system - the kill switch if a tier ever looks wrong in play.")]
        [SerializeField] bool lodEnabled = true;
        public bool LodEnabled => lodEnabled;

        [Tooltip("Seconds between classification passes (one Burst pass over every registered spindle). The shader blends continuously between passes, so this only sets how promptly a mesh swap follows the camera.")]
        [SerializeField, Min(0.05f)] float tickIntervalSeconds = 0.2f;
        public float TickIntervalSeconds => tickIntervalSeconds;

        [Tooltip("Mesh/material swaps applied per frame. A swap left for the next frame is invisible: both sides of every swap draw the same picture over the hysteresis band. Leaving the instanced batch because a spindle MOVED is never budgeted.")]
        [SerializeField, Min(16)] int maxTransitionsPerFrame = 384;
        public int MaxTransitionsPerFrame => maxTransitionsPerFrame;

        [Tooltip("Milliseconds of swaps per frame, on top of the count above: whichever runs out first ends the frame's drain. Each swap is several renderer writes, so a full count budget measured 14.7 ms in one frame (Menu_Main, 2026-10-23); the rest waits invisibly for the next frame.")]
        [SerializeField, Min(0.5f)] float maxTransitionMsPerFrame = 3f;
        public float MaxTransitionMsPerFrame => maxTransitionMsPerFrame;

        [Tooltip("Draw still, far spindles as ONE instanced call per material instead of one renderer each. Off keeps every far thread on its own renderer (the per-limb CPU cost stays; the triangle saving remains).")]
        [SerializeField] bool batchFarThreads = true;
        public bool BatchFarThreads => batchFarThreads;

        [Header("Tiers (girths)")]
        [Tooltip("The authored braid on SpindleGraph, untouched, out to here. The mid tier's decimation error (0.05 units) is under half a pixel from ~70 girths.")]
        [SerializeField, Min(1f)] float nearEndGirths = 100f;
        public float NearEndGirths => nearEndGirths;

        [Tooltip("The mid tier (the braid at every third ring) out to here; beyond it the far thread. The strands finish drawing together at midEnd x (1 - hysteresis), so the far thread takes over from a picture identical to its own.")]
        [SerializeField, Min(1f)] float midEndGirths = 420f;
        public float MidEndGirths => midEndGirths;

        [Tooltip("Fractional band below each boundary inside which a spindle keeps its current tier, so one standing near a boundary does not swap meshes every pass.")]
        [SerializeField, Range(0.01f, 0.3f)] float hysteresis = 0.08f;
        public float Hysteresis => hysteresis;

        [Tooltip("Where the three strands start drawing together and the lace starts closing into the thread. Must sit past nearEnd: the near tier (SpindleGraph) cannot draw a converging braid.")]
        [SerializeField, Min(1f)] float convergeStartGirths = 110f;
        public float ConvergeStartGirths => convergeStartGirths;

        [Tooltip("The far thread starts fading out here...")]
        [SerializeField, Min(1f)] float farFadeStartGirths = 1300f;
        public float FarFadeStartGirths => farFadeStartGirths;

        [Tooltip("...and is gone here, where it stops being drawn at all (zero cost).")]
        [SerializeField, Min(1f)] float farFadeEndGirths = 1900f;
        public float FarFadeEndGirths => farFadeEndGirths;

        [Header("Overt ladder past the peak (the prisms' normalised squared distance d^2 / _SqrDistance: 1 = fully open, ~316 units)")]
        [Tooltip("OVERT: where the settled braid (72 triangles, from the end of Resources/IncorporealDepth's recede) hands over to the 12-triangle thread. 10 = ~3.2x the ramp's distance, ~1,000 units.")]
        [SerializeField, Min(1.01f)] float overtThreadStartNorm = 10f;
        public float OvertThreadStartNorm => overtThreadStartNorm;

        [Tooltip("OVERT: where the thread starts fading out (and every other spindle body with it). 15.625 = 1.25x 1,000 units, ~1,250 units.")]
        [SerializeField, Min(1.01f)] float overtFadeStartNorm = 15.625f;
        public float OvertFadeStartNorm => overtFadeStartNorm;

        [Tooltip("OVERT: where it is gone; it stops being drawn just past here. 22.5 = 1.5x 1,000 units, ~1,500 units.")]
        [SerializeField, Min(1.02f)] float overtFadeEndNorm = 22.5f;
        public float OvertFadeEndNorm => overtFadeEndNorm;

        [Header("Overt strength (share of the danger prisms' spread, Resources/IncorporealDepth)")]
        [Tooltip("OVERT: how far a CHARGE lifeform's limbs fan open, as a share of the danger prisms' face travel (0.01 = a tip travels ~0.15 units at the peak: round 6's deliberately negligible sanity setting).")]
        [SerializeField, Range(0f, 2f)] float chargeStrength = 0.01f;
        public float ChargeStrength => chargeStrength;

        [Tooltip("OVERT: how far every other lifeform's limbs fan open (Mass, Space, Time, Omni and none). 0.005 = ~0.08 units at the peak (round 6's negligible sanity setting).")]
        [SerializeField, Range(0f, 2f)] float otherStrength = 0.005f;
        public float OtherStrength => otherStrength;

        [Header("The thread's look")]
        [Tooltip("The narrowest a thread is ever DRAWN, in screen pixels. Thinner geometry rasterises as broken, sparkling dots; past this width the thread is drawn wider and its alpha pays for the difference (coverage), so it reads as a continuous line of the right brightness.")]
        [SerializeField, Range(0.5f, 3f)] float minThreadPixels = 1.25f;
        public float MinThreadPixels => minThreadPixels;

        [Tooltip("Where the thread sits between the material's dull and bright colours when a facet is edge-on.")]
        [SerializeField, Range(0f, 1f)] float threadBase = 0.35f;
        public float ThreadBase => threadBase;

        [Tooltip("How much a facet facing the camera brightens toward the bright colour - what keeps the thread a faceted volume rather than a flat line.")]
        [SerializeField, Range(0f, 1f)] float threadFacet = 0.55f;
        public float ThreadFacet => threadFacet;

        [Tooltip("Colour multiplier the thread reaches as it recedes toward farFadeStart (above 1 reaches bloom). The far look: the skeleton glows instead of vanishing.")]
        [SerializeField, Range(0.5f, 3f)] float threadGlow = 1.35f;
        public float ThreadGlow => threadGlow;

        [Tooltip("The converged thread's opacity before coverage and fades.")]
        [SerializeField, Range(0.05f, 1f)] float threadAlpha = 0.85f;
        public float ThreadAlpha => threadAlpha;

        [Header("Beyond the braid (SpindleDepthGraph, Docs/SPINDLE_LOD.md §8)")]
        [Tooltip("OVERT: every spindle-material renderer that is NOT the braid - a creature's own model (the shark, brittlestar, worm and quadfish) - wears the same depth effect, blooming outward from its pivot (its element sets its strength, as for a limb). Off: those keep their authored material.")]
        [SerializeField] bool depthOnBodies = true;
        public bool DepthOnBodies => depthOnBodies;

        [Tooltip("OVERT: how far a switch ring's facets open, as a share of the danger prisms' face travel (0.005 = ~0.08 units at the peak, the same as a non-Charge limb). Rings are drawn in SpindleDepthGraph (ToyFactory.AddSwitchRing).")]
        [SerializeField, Range(0f, 2f)] float switchRingStrength = 0.005f;
        public float SwitchRingStrength => switchRingStrength;

        [Tooltip("SpindleGraph's _Distance on the switch rings: the squared distance its lace fades out by. A branch fades by 2,000,000 (~1,400 units); a switch is a signal you fly toward, so 200,000,000 keeps it readable ~14,000 units out.")]
        [SerializeField, Min(1f)] float switchRingDistance = 200000000f;
        public float SwitchRingDistance => switchRingDistance;

        [Tooltip("SpindleGraph's sway amplitude on the switch rings. 0: a ring is a trigger volume drawn at its own radius, and a swaying ring would draw it somewhere it is not.")]
        [SerializeField, Min(0f)] float switchRingSway = 0f;
        public float SwitchRingSway => switchRingSway;

        [Tooltip("The ring's DARK colour (its body, what an edge-on facet shows) as a share of its signal colour. Low keeps the machined facets readable: a ring between two bright colours washes flat (the prism rings used 0.22).")]
        [SerializeField, Range(0f, 1f)] float switchRingDarkShare = 0.18f;
        public float SwitchRingDarkShare => switchRingDarkShare;

        [Tooltip("How far the ring's BRIGHT colour (a facet square to the camera) sits from its signal colour toward white. 0 = the signal colour itself.")]
        [SerializeField, Range(0f, 1f)] float switchRingRimWhiten = 0f;
        public float SwitchRingRimWhiten => switchRingRimWhiten;

        [Tooltip("The synthetic wire's facet shading: where a facet edge-on to the camera sits between dark (0) and bright (1).")]
        [SerializeField, Range(0f, 1f)] float switchRingShadeBase = 0f;
        public float SwitchRingShadeBase => switchRingShadeBase;

        [Tooltip("The synthetic wire's facet shading: how far a facet square to the camera brightens past switchRingShadeBase (base + facet x facing^2). 1 = the full dark-to-bright range across the cable's facets.")]
        [SerializeField, Range(0f, 1f)] float switchRingShadeFacet = 1f;
        public float SwitchRingShadeFacet => switchRingShadeFacet;

        [Header("Measured (owned by Tools/Build/author_spindle_lod_meshes.py - do not hand-edit)")]
        [Tooltip("SpindleLodMid.mat: SpindleMaterial on SpindleLodMidGraph. A build-variant anchor; runtime LOD materials are cloned from it.")]
        [SerializeField] Material midTemplate;
        public Material MidTemplate => midTemplate;

        [Tooltip("SpindleLodFar.mat: SpindleMaterial on SpindleLodFarGraph, instancing on. A build-variant anchor; runtime LOD materials are cloned from it.")]
        [SerializeField] Material farTemplate;
        public Material FarTemplate => farTemplate;

        [Tooltip("SpindleDepth.mat: SpindleMaterial on SpindleDepthGraph. A build-variant anchor; the creatures' depth materials are cloned from it.")]
        [SerializeField] Material depthTemplate;
        public Material DepthTemplate => depthTemplate;

        [Tooltip("SpindleWire.mat: SpindleMaterial on SpindleWireGraph. A build-variant anchor; every tier of a SYNTHETIC mesh set (the switch rings' wire) is cloned from it.")]
        [SerializeField] Material wireTemplate;
        public Material WireTemplate => wireTemplate;

        [Tooltip("One entry per spindle mesh that has LOD meshes.")]
        [SerializeField] List<MeshSet> meshSets = new();
        public List<MeshSet> MeshSets => meshSets;

        [Serializable]
        public class MeshSet
        {
            [Tooltip("The authored mesh a spindle renderer draws (BezierCurve.001 in bonita.fbx).")]
            public Mesh source;
            [Tooltip("The braid at every third ring, with the convergence channels.")]
            public Mesh mid;
            [Tooltip("The braid at every sixth ring (overt: the far-side recede).")]
            public Mesh low;
            [Tooltip("The single converged thread.")]
            public Mesh far;
            [Tooltip("Object-space width of the braid: the unit every band is measured in.")]
            public float girth;
            [Tooltip("Object-space mean strand radius: what the minimum-width hold measures.")]
            public float strandRadius;
            [Tooltip("The source mesh's bounds centre as the generator predicts Unity imports it.")]
            public Vector3 expectedSourceCenter;
            [Tooltip("The source mesh's bounds extent as the generator predicts Unity imports it.")]
            public Vector3 expectedSourceExtent;
            [Tooltip("A SYNTHETIC wire (the switch rings, Docs/SPINDLE_LOD.md section 9): every tier draws on SpindleWireGraph (wireTemplate) - solid up close, a spindle's lace while open - and it is never culled.")]
            public bool synthetic;

            /// <summary>
            /// True when the imported source mesh is the one the LOD meshes were built against.
            /// The bounds are readable on a non-readable mesh, and they carry the import's axis
            /// convention (the braid's centre sits off the x axis, so a sign flip moves it), so a
            /// mesh re-export or a different import setting fails here instead of drawing a thread
            /// beside its branch.
            /// </summary>
            public bool MatchesImport(out string why)
            {
                why = null;
                if (!source || !mid || !low || !far) { why = "a mesh reference is missing"; return false; }
                if (!(girth > 0f) || !(strandRadius > 0f)) { why = "girth / strand radius not measured"; return false; }
                var b = source.bounds;
                float tol = Mathf.Max(1e-4f, expectedSourceExtent.magnitude * 0.005f);
                if ((b.center - expectedSourceCenter).magnitude > tol || (b.extents - expectedSourceExtent).magnitude > tol)
                {
                    why = $"imported bounds {b.center}/{b.extents} differ from the generator's {expectedSourceCenter}/{expectedSourceExtent}";
                    return false;
                }
                return true;
            }
        }

        /// <summary>Where the strands finish converging: the far tier's inward boundary, so the
        /// far thread only ever replaces a fully converged braid.</summary>
        public float ConvergeEndGirths => midEndGirths * (1f - hysteresis);

        /// <summary>The overt ladder against the shared recede: the peak must hold past the far tier's
        /// band, the 72-triangle braid only take over once the recede has ended, the thread only once
        /// that braid's band is behind it, and the fade only once the thread has taken over.</summary>
        public bool OvertBandsAreSane(Vector4 recede, out string why)
        {
            why = null;
            float keep = 1f - hysteresis;
            if (!(recede.x > 1f / keep)) why = "IncorporealDepth.recedeStart must lie past the over-distance band (1 / (1 - hysteresis))";
            else if (!(recede.y > recede.x / keep)) why = "IncorporealDepth.recedeEnd must lie past recedeStart's band";
            else if (!(overtThreadStartNorm * keep > recede.y / keep)) why = "overtThreadStart's band must lie past the 72-triangle braid's (recedeEnd / (1 - hysteresis))";
            else if (!(overtFadeStartNorm >= overtThreadStartNorm)) why = "overtFadeStart must not begin before the thread has taken over";
            return why == null;
        }

        /// <summary>The synthetic wire's mesh set (the switch rings), or null when the profile has none.</summary>
        public MeshSet SyntheticSet
        {
            get
            {
                for (int i = 0; i < meshSets.Count; i++)
                    if (meshSets[i] != null && meshSets[i].synthetic) return meshSets[i];
                return null;
            }
        }

        /// <summary>A limb's overt strength: Charge lifeforms part hardest.</summary>
        public float StrengthFor(bool charge) => charge ? chargeStrength : otherStrength;

        /// <summary>The bands are in the order the tiers need them. A profile that is not falls
        /// back to the authored braid everywhere rather than drawing a tier out of its range.</summary>
        public bool IsSane(out string why)
        {
            if (!BandsAreSane(out why)) return false;
            if (!midTemplate || !farTemplate) why = "a template material is missing";
            else if (SyntheticSet != null && !wireTemplate) why = "a synthetic mesh set has no wireTemplate";
            return why == null;
        }

        /// <summary>The ordering every invisible swap depends on: each CPU boundary's hysteresis band
        /// must sit where the two tiers it separates draw the same picture (near and mid both draw
        /// the plain lace below convergeStart; mid and far both draw the thread past convergeEnd).</summary>
        public bool BandsAreSane(out string why)
        {
            why = null;
            if (!(convergeStartGirths >= nearEndGirths)) why = "convergeStart must not precede nearEnd";
            else if (!(ConvergeEndGirths > convergeStartGirths)) why = "midEnd x (1 - hysteresis) must exceed convergeStart";
            else if (!(farFadeStartGirths > midEndGirths)) why = "farFadeStart must exceed midEnd";
            else if (!(farFadeEndGirths > farFadeStartGirths)) why = "farFadeEnd must exceed farFadeStart";
            else if (!(overtFadeStartNorm > 1f / (1f - hysteresis))) why = "overtFadeStart must lie past the over-distance band (1 / (1 - hysteresis))";
            else if (!(overtFadeEndNorm > overtFadeStartNorm)) why = "overtFadeEnd must exceed overtFadeStart";
            return why == null;
        }

        static SpindleLodProfileSO _instance;
        static bool _resolved;

        /// <summary>The shipped profile, or null when none exists (no LOD at all - every spindle
        /// keeps its braid, exactly as before the system existed).</summary>
        public static SpindleLodProfileSO Instance
        {
            get
            {
                // Resolved once: a missing profile is a stable answer, and this is asked once per
                // spindle born, so it must not become a Resources.Load per birth.
                if (!_resolved)
                {
                    _instance = Resources.Load<SpindleLodProfileSO>(ResourcePath);
                    _resolved = true;
                }
                return _instance;
            }
        }
    }
}
