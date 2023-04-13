using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StarWriter.Utility.Tools
{
    public struct Tools
    {
        public IEnumerator LerpingCoroutine(System.Action<float> replacementMethod, System.Func<float> getCurrent, float newValue, float duration, int steps)
        {
            float elapsedTime = 0;
            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                replacementMethod(Mathf.Lerp(getCurrent(), newValue, elapsedTime / duration));
                yield return new WaitForSeconds(duration / (float)steps);
            }
        }
    }
}

