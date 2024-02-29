using UnityEngine;

namespace CosmicShore.Utility.ClassExtensions
{
    public static class DebugLogExtensions
    {
        public static void LogWithClassMethod<T>(this T obj, string methodName, string message)
        {
            Debug.LogFormat("{0} - {1}: {2}", obj.GetType(), methodName, message);
        }
        
        public static void LogWarningWithClassMethod<T>(this T obj, string methodName, string message)
        {
            Debug.LogWarningFormat("{0} - {1}: {2}", obj.GetType(), methodName, message);
        }
        
        public static void LogErrorWithClassMethod<T>(this T obj, string methodName, string message)
        {
            Debug.LogErrorFormat("{0} - {1}: {2}", obj.GetType(), methodName, message);
        }
    }
}
