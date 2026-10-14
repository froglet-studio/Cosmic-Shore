using System;
using System.IO;

namespace CosmicShore.Launcher
{
    /// <summary>
    /// An exception nothing caught closes Amoebius with no message on Windows (there is no console).
    /// It is written to <c>last-crash.txt</c> in the data folder instead, and the next Amoebius
    /// shows it as a notification, so a "it just closes" report comes with its cause.
    /// </summary>
    public static class CrashLog
    {
        public static string PathOf(string dataDir) => Path.Combine(dataDir, "last-crash.txt");

        public static void Install() =>
            AppDomain.CurrentDomain.UnhandledException += (_, e) => Write(LauncherSettings.DataDir, e.ExceptionObject as Exception);

        public static void Write(string dataDir, Exception? e)
        {
            try
            {
                File.WriteAllText(PathOf(dataDir),
                    $"Amoebius closed unexpectedly at {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{e?.GetType().FullName}: {e?.Message}\n\n{e}\n");
            }
            catch (Exception) { /* nothing more to do on the way down */ }
        }

        /// <summary>The last crash's first two lines (time, then the exception) and its file, once: the file is renamed so it is reported only once.</summary>
        public static (string Summary, string File)? TakeLast(string dataDir)
        {
            var path = PathOf(dataDir);
            try
            {
                if (!File.Exists(path)) return null;
                var lines = File.ReadAllLines(path);
                var kept = Path.Combine(dataDir, $"crash-{File.GetLastWriteTime(path):yyyyMMdd-HHmmss}.txt");
                File.Move(path, kept, overwrite: true);
                return (lines.Length > 1 ? lines[1] : lines.Length > 0 ? lines[0] : "unknown error", kept);
            }
            catch (Exception) { return null; }
        }
    }
}
