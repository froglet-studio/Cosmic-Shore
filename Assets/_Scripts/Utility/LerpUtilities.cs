using System;
using System.Collections;
using UnityEngine;

namespace CosmicShore.Utility
{
    public class LerpUtilities
    {
        public static IEnumerator LerpingCoroutine(float getCurrent, float newValue, float duration, Action<float> replacementMethod)
        {
            float elapsedTime = 0;
            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                replacementMethod(Mathf.Lerp(getCurrent, newValue, elapsedTime / duration));
                yield return null;
            }
        }

        public static IEnumerator LerpingCoroutine(Vector3 getCurrent, Vector3 newValue, float duration, Action<Vector3> replacementMethod)
        {
            float elapsedTime = 0;
            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                replacementMethod(Vector3.Lerp(getCurrent, newValue, elapsedTime / duration));
                yield return null;
            }
        }
    }
}