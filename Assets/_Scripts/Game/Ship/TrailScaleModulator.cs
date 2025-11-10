using System.Collections;
using UnityEngine;

namespace CosmicShore.Game
{
    public class TrailScaleModulator : MonoBehaviour
    {
        [SerializeField] private VesselPrismController controller;
        [SerializeField] private TrailScaleProfileSO   profile;

        Vector3 _original; // XScaler, YScaler, ZScaler

        void Awake()
        {
            if (!controller) controller = GetComponent<VesselPrismController>();
        }

        public void Apply()
        {
            if (!controller || !profile) return;
            _original = new Vector3(controller.XScaler, controller.YScaler, controller.ZScaler);

            Vector3 target;
            if (profile.isChange)
                target = new Vector3(_original.x * profile.scaleXYZ.x,
                    _original.y * profile.scaleXYZ.y,
                    _original.z * profile.scaleXYZ.z);
            else
                target = profile.scaleXYZ;

            StopAllCoroutines();
            StartCoroutine(LerpScalers(_original, target, Mathf.Max(0f, profile.applyLerpSeconds)));
        }

        public void Revert()
        {
            if (!controller || !profile) return;
            StopAllCoroutines();
            StartCoroutine(LerpScalers(new Vector3(controller.XScaler, controller.YScaler, controller.ZScaler),
                _original,
                Mathf.Max(0f, profile.revertLerpSeconds)));
        }

        IEnumerator LerpScalers(Vector3 from, Vector3 to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float a = duration > 0f ? t / duration : 1f;
                var v = Vector3.Lerp(from, to, a);
                controller.XScaler = Mathf.Max(0.0001f, v.x);
                controller.YScaler = Mathf.Max(0.0001f, v.y);
                controller.ZScaler = Mathf.Max(0.0001f, v.z);
                yield return null;
            }
            controller.XScaler = Mathf.Max(0.0001f, to.x);
            controller.YScaler = Mathf.Max(0.0001f, to.y);
            controller.ZScaler = Mathf.Max(0.0001f, to.z);
        }
    }
}