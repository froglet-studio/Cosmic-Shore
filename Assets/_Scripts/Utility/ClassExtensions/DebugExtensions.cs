using UnityEngine;

namespace CosmicShore.Utility.ClassExtensions
{
    public static class DebugExtensions
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

        /// <summary>
        /// Logs a message to the Unity Console in the given color.
        /// </summary>
        /// <param name="message">The text to log.</param>
        /// <param name="color">The color to render the text in the Console.</param>
        /// <param name="context">Optional UnityEngine.Object context.</param>
        public static void LogColored(string message, Color color, Object context = null)
        {
            // Convert the Color to a hex string, e.g. "FF0000" for red
            string hex = ColorUtility.ToHtmlStringRGB(color);
            string wrapped = $"<color=#{hex}>{message}</color>";

            if (context != null)
                Debug.Log(wrapped, context);
            else
                Debug.Log(wrapped);
        }

        /// <summary>
        /// Logs a warning message in the given color.
        /// </summary>
        public static void LogWarningColored(string message, Color color, Object context = null)
        {
            string hex = ColorUtility.ToHtmlStringRGB(color);
            string wrapped = $"<color=#{hex}>{message}</color>";

            if (context != null)
                Debug.LogWarning(wrapped, context);
            else
                Debug.LogWarning(wrapped);
        }

        /// <summary>
        /// Logs an error message in the given color.
        /// </summary>
        public static void LogErrorColored(string message, Color color, Object context = null)
        {
            string hex = ColorUtility.ToHtmlStringRGB(color);
            string wrapped = $"<color=#{hex}>{message}</color>";

            if (context != null)
                Debug.LogError(wrapped, context);
            else
                Debug.LogError(wrapped);
        }
    }
}