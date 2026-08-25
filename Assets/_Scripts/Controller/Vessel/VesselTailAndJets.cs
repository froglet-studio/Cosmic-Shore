using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The vessel-level owner of a hull's <b>TAIL</b> and <b>JETS</b> — the sibling of
    /// <see cref="VesselCustomization"/>, which does the same job for the hull's materials.
    /// Full contract: <c>Docs/VESSEL_TAIL_AND_JETS.md</c>.
    ///
    /// Every tail and jet is repainted with the vessel's live domain, exactly as its prism trail
    /// and its hull are. A vessel's tail and jets are identity at range; a hull that flies one
    /// colour and streaks another is a lie about whose it is.
    ///
    /// <b>Scope is the MARKERS, not every TrailRenderer under the vessel.</b> That distinction is
    /// the whole reason <see cref="VesselTail"/> and <see cref="VesselJet"/> exist as components
    /// rather than as a naming convention. A vessel can carry streaks that are not identity at
    /// all — the Rhino's five <c>RhinoSwordBladeTracer</c>s are a STATE readout owned by
    /// <c>RhinoSwordFXController</c>, which drives their colour from the blade's energy and its
    /// impact flash. Sweeping every trail under the vessel repaints those with the domain and
    /// fights the controller for them every frame. Anything that wants the domain says so by
    /// carrying a marker.
    ///
    /// Tails and jets are both drawn on every machine. A jet is TUNED for its own pilot (size,
    /// placement, a short life, all judged against that vessel's own camera) but is not hidden
    /// from anybody — a rival's plumes are how you read their thrust up close.
    ///
    /// <b>Discovery is LIVE, never cached at Awake.</b> A vessel's FX arrive across several frames
    /// — prefab children at Awake, a runtime swap later — so a set captured once would silently
    /// omit whatever showed up afterwards and leave it wearing its prefab colour forever.
    /// Re-discovery costs nothing here because this runs on a domain change and on init, never per
    /// frame.
    /// </summary>
    [DisallowMultipleComponent]
    public class VesselTailAndJets : MonoBehaviour
    {
        [Tooltip("Explicit trail renderers to tint per domain. Leave EMPTY (the standard) and " +
                 "every TrailRenderer under this vessel is discovered live on each domain change " +
                 "— which is what lets a tail or jet added later be tinted with no rewiring.")]
        [SerializeField] List<TrailRenderer> _trails;

        /// <summary>
        /// The authored alpha curve per trail, captured the first time each trail is seen. Keyed
        /// per-trail rather than by index because the discovered set can grow; an index-parallel
        /// array silently mis-pairs curves the moment it does.
        /// </summary>
        readonly Dictionary<TrailRenderer, GradientAlphaKey[]> _originalAlphaKeys = new();

        bool _hasColors;
        Color _highlightColor = Color.white;
        Color _coreColor = Color.white;

        static readonly List<TrailRenderer> TrailScratch = new();
        static readonly List<VesselTail> TailScratch = new();
        static readonly List<VesselJet> JetScratch = new();

        /// <summary>Repaint every tail and jet with the vessel's domain colours.</summary>
        public void SetColors(Color highlightColor, Color coreColor)
        {
            _highlightColor = highlightColor;
            _coreColor = coreColor;
            _hasColors = true;
            Apply();
        }

        /// <summary>
        /// Re-apply the last domain colours to the CURRENT set of trails, for FX that appeared
        /// after the vessel's last domain change. No-op before the first <see cref="SetColors"/>.
        /// </summary>
        public void Refresh()
        {
            if (_hasColors) Apply();
        }

        void Apply()
        {
            var trails = ResolveTrails();
            for (int i = 0; i < trails.Count; i++)
            {
                var trail = trails[i];
                if (trail == null) continue;

                if (!_originalAlphaKeys.TryGetValue(trail, out var alphaKeys))
                {
                    alphaKeys = trail.colorGradient.alphaKeys;
                    _originalAlphaKeys[trail] = alphaKeys;
                }

                var gradient = new Gradient();
                gradient.SetKeys(
                    new[]
                    {
                        new GradientColorKey(_highlightColor, 0f),
                        new GradientColorKey(_coreColor, 1f),
                    },
                    alphaKeys);
                trail.colorGradient = gradient;
            }
        }

        /// <summary>
        /// Authored list wins; otherwise collect the trails under this vessel's TAIL and JET
        /// markers — never every TrailRenderer under the vessel, see the class doc. Inactive
        /// objects are included so FX a mode or an ability has switched off are already the right
        /// colour when they come back. The shared scratch lists keep the allocation at zero.
        /// </summary>
        List<TrailRenderer> ResolveTrails()
        {
            if (_trails is { Count: > 0 }) return _trails;

            TrailScratch.Clear();
            TailScratch.Clear();
            JetScratch.Clear();
            GetComponentsInChildren(true, TailScratch);
            GetComponentsInChildren(true, JetScratch);
            for (int i = 0; i < TailScratch.Count; i++) Collect(TailScratch[i]);
            for (int i = 0; i < JetScratch.Count; i++) Collect(JetScratch[i]);
            return TrailScratch;
        }

        static readonly List<TrailRenderer> CollectScratch = new();

        static void Collect(Component marker)
        {
            CollectScratch.Clear();
            marker.GetComponentsInChildren(true, CollectScratch);
            TrailScratch.AddRange(CollectScratch);
        }
    }
}
