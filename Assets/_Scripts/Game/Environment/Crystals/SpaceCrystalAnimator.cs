using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    using UnityEngine;

    public class SpaceCrystalAnimator : MonoBehaviour  //TODO: Move this to an GPU instance or onto a shader so this doesn't run on all space crystals
    {
        SkinnedMeshRenderer crystalRenderer;
        public float cycleSpeed = 1f; // Speed of the animation cycle
        public float timer = 0f;
        private int currentShapeKey = 0;

        void Start()
        {
            crystalRenderer = GetComponent<SkinnedMeshRenderer>();
        }

        void FixedUpdate()
        {
            if (crystalRenderer != null)
            {
                timer += Time.deltaTime * cycleSpeed;

                if (timer <= 1f)
                {
                    // Smooth increase from 0 to 1
                    float value = timer;
                    crystalRenderer.SetBlendShapeWeight(currentShapeKey, value * 100f);
                }
                else if (timer > 1f && timer <= 1.1f)
                {
                    // Abrupt return to 0
                    crystalRenderer.SetBlendShapeWeight(currentShapeKey, 0f);
                }
                else
                {
                    // Switch to the next shape key and reset timer
                    currentShapeKey = (currentShapeKey + 1) % 2;
                    timer = 0f;
                }
            }
        }
    }
}
