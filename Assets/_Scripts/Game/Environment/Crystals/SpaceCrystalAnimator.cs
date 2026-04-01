using System.Collections;
using UnityEngine;

namespace CosmicShore
{
    public class SpaceCrystalAnimator : MonoBehaviour
    {
        [Header("Idle Loop")]
        [SerializeField] float cycleSpeed = 1f;
        public float timer = 0f;

        [Header("Collect Animation")]
        [SerializeField] float collectDuration = 0.35f;
        [SerializeField] float shrinkDuration = 0.25f;
        [SerializeField] AnimationCurve shrinkCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

        SkinnedMeshRenderer crystalRenderer;
        int currentShapeKey = 0;

        bool isCollecting;
        Vector3 startScale;

        public float TotalCollectTime => collectDuration + shrinkDuration;

        void Awake()
        {
            crystalRenderer = GetComponent<SkinnedMeshRenderer>();
            startScale = transform.localScale;
        }

        void FixedUpdate()
        {
            if (isCollecting) return;
            if (crystalRenderer == null) return;

            timer += Time.deltaTime * cycleSpeed;

            if (timer <= 1f)
            {
                float value = timer; // 0..1
                crystalRenderer.SetBlendShapeWeight(currentShapeKey, value * 100f);
            }
            else if (timer > 1f && timer <= 1.1f)
            {
                crystalRenderer.SetBlendShapeWeight(currentShapeKey, 0f);
            }
            else
            {
                currentShapeKey = (currentShapeKey + 1) % 2;
                timer = 0f;
            }
        }

        /// <summary>
        /// One-shot "collected" animation. Stops idle loop, does a quick pulse then shrinks out.
        /// </summary>
        public void PlayCollect()
        {
            if (isCollecting) return;
            isCollecting = true;
            StartCoroutine(CollectRoutine());
        }

        IEnumerator CollectRoutine()
        {
            // Pulse both blendshapes to 100 quickly
            float t = 0f;
            while (t < collectDuration)
            {
                t += Time.deltaTime;
                float a = Mathf.Clamp01(t / collectDuration);
                float w = a * 100f;

                crystalRenderer.SetBlendShapeWeight(0, w);
                crystalRenderer.SetBlendShapeWeight(1, w);
                yield return null;
            }

            // Shrink to 0
            t = 0f;
            while (t < shrinkDuration)
            {
                t += Time.deltaTime;
                float a = Mathf.Clamp01(t / shrinkDuration);
                float s = shrinkCurve.Evaluate(a);
                transform.localScale = startScale * s;
                yield return null;
            }

            transform.localScale = Vector3.zero;
        }
    }
}
