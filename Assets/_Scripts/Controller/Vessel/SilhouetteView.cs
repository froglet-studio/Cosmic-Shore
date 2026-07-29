using CosmicShore.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Gameplay
{
    public class SilhouetteView : MonoBehaviour
    {
        [Header("Silhouette Root & Jaws")]
        [SerializeField] private RectTransform silhouetteRoot;
        [SerializeField] private RectTransform jawTop;
        [SerializeField] private RectTransform jawBottom;
        [SerializeField, Min(0f)] private float jawMaxOffset = 40f;
        [SerializeField] private Color jawNormalColor = Color.white;
        [SerializeField] private Color jawFullColor = Color.green;
        [SerializeField] private Image[] jawTintTargets;

        [Header("Config")]
        [SerializeField] private SilhouetteConfigSO config;

        [Header("Manta Flower Overlay")]
        [SerializeField] private GameObject overlayPrefab;
        [SerializeField] private float overlayDuration = 1f;

        private static readonly int DomainColorId = Shader.PropertyToID("_DomainColor");
        private Material _holoInstance;

        float Alpha => (config != null && config.smooth)
            ? (1f - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(0.0001f, config.smoothingSeconds)))
            : 1f;

        // --- Holo icon treatment ---

        /// <summary>
        /// Applies the shared holographic material (domain-tinted body, pulsing rim, scanline
        /// shimmer) to every Image under the silhouette root. One material instance per HUD (this
        /// is the local player's icon) so the domain accent can be set without touching the shared
        /// asset; every look parameter beyond the accent lives on the material.
        /// </summary>
        public void ApplyHoloStyle(Color domainAccent)
        {
            if (!config || !config.enableHoloStyle || !config.holoMaterial || !silhouetteRoot) return;

            if (!_holoInstance)
                _holoInstance = new Material(config.holoMaterial);
            _holoInstance.SetColor(DomainColorId, domainAccent);

            foreach (var img in silhouetteRoot.GetComponentsInChildren<Image>(true))
                img.material = _holoInstance;
        }

        void OnDestroy()
        {
            if (_holoInstance)
            {
                if (Application.isPlaying) Destroy(_holoInstance); else DestroyImmediate(_holoInstance);
                _holoInstance = null;
            }
        }

        // --- Energy UI ---
        public void UpdateEnergyUI(float current, float max)
        {
            float norm = (max > 0f) ? Mathf.Clamp01(current / max) : 0f;

            if (jawTop)
            {
                var p = jawTop.anchoredPosition;
                p.y = Mathf.Lerp(0f, +jawMaxOffset, norm);
                jawTop.anchoredPosition = Vector2.Lerp(jawTop.anchoredPosition, p, Alpha);
            }
            if (jawBottom)
            {
                var p = jawBottom.anchoredPosition;
                p.y = Mathf.Lerp(0f, -jawMaxOffset, norm);
                jawBottom.anchoredPosition = Vector2.Lerp(jawBottom.anchoredPosition, p, Alpha);
            }

            var col = (norm >= 0.999f) ? jawFullColor : jawNormalColor;
            if (jawTintTargets != null)
            {
                for (int i = 0; i < jawTintTargets.Length; i++)
                {
                    if (jawTintTargets[i]) jawTintTargets[i].color = Color.Lerp(jawTintTargets[i].color, col, Alpha);
                }
            }
        }

        public void SyncSilhouetteRotation2D(IVesselStatus status)
        {
            if (!silhouetteRoot) return;

            var fwd = status.ShipTransform ? status.ShipTransform.forward : Vector3.forward;
            var course = status.Course;

            var fwd2 = Vector3.ProjectOnPlane(fwd, Vector3.up);
            var course2 = Vector3.ProjectOnPlane(course, Vector3.up);

            if (fwd2.sqrMagnitude < 1e-6f || course2.sqrMagnitude < 1e-6f) return;

            float angle = Vector3.SignedAngle(course2, fwd2, Vector3.up);
            var target = Quaternion.Euler(0f, 0f, angle);
            silhouetteRoot.localRotation = Quaternion.Slerp(silhouetteRoot.localRotation, target, Alpha);
        }

        public void ShowMantaFlowerOverlay()
        {
            if (silhouetteRoot == null || overlayPrefab == null) return;

            var overlay = Instantiate(overlayPrefab, silhouetteRoot, false);
            overlay.transform.localPosition = Vector3.zero;
            overlay.transform.localScale = Vector3.one;

            if (overlayDuration > 0f)
                Destroy(overlay, overlayDuration);
        }

    }
}
