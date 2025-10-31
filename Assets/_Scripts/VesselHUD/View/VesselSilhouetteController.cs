using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game
{
    /// <summary>
    /// Handles vessel silhouette tilt/rotation & Jaws resource meter. Drift-driven tilt is optional.
    /// </summary>
    public sealed class VesselSilhouetteController : MonoBehaviour
    {
        [Header("Silhouette Containers")]
        [SerializeField] private Transform silhouetteContainer;

        [Header("Silhouette Parts (optional)")]
        [SerializeField] private GameObject[] silhouetteParts;

        [Header("Drift Source (optional)")]
        [SerializeField] private DriftTrailActionExecutor driftTrailAction;

        [Header("Jaws (Resource Meter)")]
        [SerializeField] private Image topJaw;
        [SerializeField] private Image bottomJaw;
        [SerializeField] private int   jawResourceIndex = -1;

        [Range(-0.9999f, 0.9999f)] [SerializeField] private float debugDot = 0f;

        private IVesselStatus _status;
        private bool _driftSub;
        private bool _jawsSub;

        public void Initialize(IVesselStatus statusOrController)
        {
            if (statusOrController is IVessel vessel)
                _status = vessel.VesselStatus;
            else
                _status = statusOrController;

            if (driftTrailAction)
            {
                driftTrailAction.OnChangeDriftAltitude += OnDriftDotChanged;
                _driftSub = true;
            }

            if (silhouetteParts != null)
                foreach (var go in silhouetteParts)
                    if (go) go.SetActive(true);

            TryBindJaws();
            PrimeJaws();
        }

        public void TearDown()
        {
            if (_driftSub && driftTrailAction)
            {
                driftTrailAction.OnChangeDriftAltitude -= OnDriftDotChanged;
                _driftSub = false;
            }

            if (_jawsSub && _status?.ResourceSystem?.Resources != null)
            {
                var resList = _status.ResourceSystem.Resources;
                if (jawResourceIndex >= 0 && jawResourceIndex < resList.Count && resList[jawResourceIndex] != null)
                    resList[jawResourceIndex].OnResourceChange -= OnJawResourceChanged;
            }

            _jawsSub = false;
            _status  = null;
        }

        #region Drift → Silhouette tilt
        private void OnDriftDotChanged(float dot)
        {
            var clamped = Mathf.Clamp(dot, -0.9999f, 0.9999f);
            ApplyTilt(clamped);
        }

        private void ApplyTilt(float dot)
        {
            if (!silhouetteContainer) return;
            var angleZ = Mathf.Asin(dot) * Mathf.Rad2Deg;
            silhouetteContainer.localRotation = Quaternion.Euler(0, 0, angleZ);
            debugDot = dot;
        }
        #endregion

        #region Jaws
        private void TryBindJaws()
        {
            if (_status == null || jawResourceIndex < 0) return;
            if (!topJaw && !bottomJaw) return;

            var resList = _status.ResourceSystem?.Resources;
            if (resList == null || jawResourceIndex >= resList.Count) return;

            var res = resList[jawResourceIndex];
            if (res == null) return;

            res.OnResourceChange += OnJawResourceChanged;
            _jawsSub = true;
        }

        private void PrimeJaws()
        {
            if (_status == null || jawResourceIndex < 0) return;
            var resList = _status.ResourceSystem?.Resources;
            if (resList == null || jawResourceIndex >= resList.Count) return;

            var res = resList[jawResourceIndex];
            if (res == null) return;

            float normalized = 0f;
            try { normalized = res.CurrentAmount; } catch { /* ignore */ }
            OnJawResourceChanged(normalized);
        }

        private void OnJawResourceChanged(float normalized)
        {
            if (topJaw)    topJaw.rectTransform.localRotation    = Quaternion.Euler(0, 0,  21f * normalized);
            if (bottomJaw) bottomJaw.rectTransform.localRotation = Quaternion.Euler(0, 0, -21f * normalized);

            var col = normalized > 0.98f ? Color.green : Color.white;
            if (topJaw)    topJaw.color    = col;
            if (bottomJaw) bottomJaw.color = col;
        }
        #endregion
    }
}
