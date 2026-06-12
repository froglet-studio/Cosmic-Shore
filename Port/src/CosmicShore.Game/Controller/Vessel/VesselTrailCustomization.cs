using System.Collections.Generic;
using CosmicShore.Engine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Applies domain-tinted colors to a vessel's TrailRenderer children, mirroring
    /// how <see cref="VesselCustomization"/> swaps ship materials per domain.
    /// Rebuilds each trail's <see cref="TrailRenderer.colorGradient"/> using the
    /// supplied highlight (head) and core (tail) colors while preserving the
    /// prefab-authored alpha curve.
    /// </summary>
    public class VesselTrailCustomization : MonoBehaviour
    {
        [Tooltip("Trail renderers to tint per domain. If left empty, all TrailRenderers under this GameObject are discovered at Awake.")]
        [SerializeField] List<TrailRenderer> _trails;

        GradientAlphaKey[][] _originalAlphaKeys;

        void Awake()
        {
            if (_trails == null || _trails.Count == 0)
            {
                var found = GetComponentsInChildren<TrailRenderer>(includeInactive: true);
                _trails = new List<TrailRenderer>(found);
            }

            _originalAlphaKeys = new GradientAlphaKey[_trails.Count][];
            for (int i = 0; i < _trails.Count; i++)
            {
                var trail = _trails[i];
                if (trail == null) continue;
                _originalAlphaKeys[i] = trail.colorGradient.alphaKeys;
            }
        }

        public void SetTrailColors(Color highlightColor, Color coreColor)
        {
            if (_trails == null) return;

            for (int i = 0; i < _trails.Count; i++)
            {
                var trail = _trails[i];
                if (trail == null) continue;

                var alphaKeys = _originalAlphaKeys != null && i < _originalAlphaKeys.Length
                    ? _originalAlphaKeys[i]
                    : trail.colorGradient.alphaKeys;

                var gradient = new Gradient();
                gradient.SetKeys(
                    new[]
                    {
                        new GradientColorKey(highlightColor, 0f),
                        new GradientColorKey(coreColor, 1f),
                    },
                    alphaKeys);
                trail.colorGradient = gradient;
            }
        }
    }
}
