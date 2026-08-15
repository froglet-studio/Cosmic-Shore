using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Applies domain-tinted colors to a vessel's TrailRenderer children, mirroring
    /// how <see cref="VesselCustomization"/> swaps ship materials per domain.
    /// Rebuilds each trail's <see cref="TrailRenderer.colorGradient"/> using the
    /// supplied highlight (head) and core (tail) colors while preserving the
    /// prefab-authored alpha curve.
    ///
    /// This is the component that makes BOTH jet FX layers wear the vessel's domain — the
    /// long beacon ribbon and the per-engine plumes alike (see <c>Docs/VESSEL_JET_FX.md</c>).
    /// It does not know or care which layer a trail belongs to: anything under the vessel that
    /// draws a trail is the vessel's colour, which is exactly why a new FX layer inherits the
    /// tint for free and cannot be authored into the wrong domain.
    ///
    /// DISCOVERY IS LIVE, NOT CACHED AT AWAKE. Trails arrive after Awake — <see cref="VesselJetFX"/>
    /// spawns its layers during <c>VesselController.Initialize</c> — so a set captured at Awake
    /// would silently omit every runtime jet and leave it wearing its prefab colour forever.
    /// Re-discovery is cheap because this runs only on a domain change, not per frame.
    /// </summary>
    public class VesselTrailCustomization : MonoBehaviour
    {
        [Tooltip("Explicit trail renderers to tint per domain. If left empty, ALL TrailRenderers " +
                 "under this GameObject are discovered live on each domain change — which is what " +
                 "lets runtime-spawned jet FX be tinted too.")]
        [SerializeField] List<TrailRenderer> _trails;

        /// <summary>
        /// The authored alpha curve per trail, captured the first time each trail is seen.
        /// Keyed per-trail rather than by index because the discovered set grows at runtime;
        /// an index-parallel array silently mis-pairs curves the moment it does.
        /// </summary>
        readonly Dictionary<TrailRenderer, GradientAlphaKey[]> _originalAlphaKeys = new();

        bool _hasColors;
        Color _highlightColor = Color.white;
        Color _coreColor = Color.white;

        static readonly List<TrailRenderer> Scratch = new();

        public void SetTrailColors(Color highlightColor, Color coreColor)
        {
            _highlightColor = highlightColor;
            _coreColor = coreColor;
            _hasColors = true;
            Apply();
        }

        /// <summary>
        /// Re-applies the last domain colors to the CURRENT set of trails. Called after new
        /// trails are spawned (jet FX) so they do not sit at their prefab colour until the
        /// player's next domain change. No-op before the first <see cref="SetTrailColors"/>.
        /// </summary>
        public void Refresh()
        {
            if (_hasColors) Apply();
        }

        void Apply()
        {
            foreach (var trail in ResolveTrails())
            {
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
        /// Authored list wins; otherwise discover live. The shared scratch list keeps the
        /// per-domain-change allocation at zero for the common (auto-discover) path.
        /// </summary>
        List<TrailRenderer> ResolveTrails()
        {
            if (_trails is { Count: > 0 }) return _trails;

            Scratch.Clear();
            GetComponentsInChildren(true, Scratch);
            return Scratch;
        }
    }
}
