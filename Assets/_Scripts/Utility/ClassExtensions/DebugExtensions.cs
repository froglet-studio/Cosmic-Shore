using UnityEngine;

namespace CosmicShore.Utility
{
    public static class DebugExtensions
    {
        public static void LogWithClassMethod<T>(this T obj, string methodName, string message)
        {
            CSDebug.LogFormat("{0} - {1}: {2}", obj.GetType(), methodName, message);
        }

        public static void LogWarningWithClassMethod<T>(this T obj, string methodName, string message)
        {
            CSDebug.LogWarningFormat("{0} - {1}: {2}", obj.GetType(), methodName, message);
        }

        public static void LogErrorWithClassMethod<T>(this T obj, string methodName, string message)
        {
            CSDebug.LogErrorFormat("{0} - {1}: {2}", obj.GetType(), methodName, message);
        }
    }
}
