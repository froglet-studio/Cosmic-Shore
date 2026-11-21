using System.Collections.Generic;

// UnityEngine.CrashReportHandler (the live-src sync maps it to CosmicShore.Engine.CrashReportHandler).
namespace CosmicShore.Engine.CrashReportHandler
{
    /// <summary>
    /// Unity Cloud Diagnostics crash reporting. The port has no crash-report backend, so the
    /// switches and metadata are recorded (and read back) but nothing is ever uploaded.
    /// </summary>
    public static class CrashReportHandler
    {
        static readonly Dictionary<string, string> s_Metadata = new();

        public static bool enableCaptureExceptions { get; set; } = true;
        public static uint logBufferSize { get; set; } = 10;

        public static void SetUserMetadata(string key, string value)
        {
            if (key == null) return;
            lock (s_Metadata)
            {
                if (value == null) s_Metadata.Remove(key);
                else s_Metadata[key] = value;
            }
        }

        public static string GetUserMetadata(string key)
        {
            if (key == null) return null;
            lock (s_Metadata)
                return s_Metadata.TryGetValue(key, out var v) ? v : null;
        }
    }
}
