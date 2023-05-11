using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace StarWriter.Utility.Tools
{
    public struct Tools
    {
        public static IEnumerator LerpingCoroutine(Action<float> replacementMethod, Func<float> getCurrent, float newValue, float duration)
        {
            float elapsedTime = 0;
            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                replacementMethod(Mathf.Lerp(getCurrent(), newValue, elapsedTime / duration));
                yield return null;
            }
        }

        public IEnumerator LateStart(float seconds, string functionName)
        {
            yield return new WaitForSeconds(seconds);

            Type thisType = this.GetType();
            MethodInfo method = thisType.GetMethod(functionName);
            if (method != null)
            {
                Action function = (Action)Delegate.CreateDelegate(typeof(Action), this, method);
                function();
            }
            else
            {
                Debug.LogError("Could not find function: " + functionName);
            }
        }

    }
}