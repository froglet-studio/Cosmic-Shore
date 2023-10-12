using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace StarWriter.Utility.Tools
{
    public struct Tools
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