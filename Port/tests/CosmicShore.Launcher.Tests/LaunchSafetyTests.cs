using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using CosmicShore.Launcher;

namespace CosmicShore.Launcher.Tests
{
    /// <summary>
    /// Amoebius closed every time PLAY started the game: the UI reads <see cref="LauncherJobs.GameRunning"/>
    /// every frame, and the game's Process was published before it had started, so
    /// <see cref="Process.HasExited"/> threw on the UI thread. These pin the state that killed it.
    /// </summary>
    public class LaunchSafetyTests
    {
        [Fact]
        public void HasExited_OnAProcessNotYetStarted_Throws()
        {
            // The hazard itself: why the field must never hold an unstarted process unguarded.
            using var p = new Process { StartInfo = new ProcessStartInfo("does-not-matter") };
            Assert.Throws<InvalidOperationException>(() => p.HasExited);
        }

        [Fact]
        public void GameRunning_WhileTheGameIsStillStarting_IsFalse_AndDoesNotThrow()
        {
            var s = new LauncherSettings();
            var tools = new Toolchain();
            var jobs = new LauncherJobs(s, tools, new Workspace(s, tools));
            using var starting = new Process { StartInfo = new ProcessStartInfo("does-not-matter") };
            typeof(LauncherJobs).GetField("_game", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(jobs, starting);

            Assert.False(jobs.GameRunning);
        }

        [Fact]
        public void IsRunning_FollowsARealProcess_FromStartToExit()
        {
            var psi = OperatingSystem.IsWindows()
                ? new ProcessStartInfo("cmd", "/c ping -n 2 127.0.0.1 >nul")
                : new ProcessStartInfo("sleep", "1");
            psi.UseShellExecute = false;
            using var p = new Process { StartInfo = psi };
            Assert.False(ProcessRunner.IsRunning(p));
            p.Start();
            Assert.True(ProcessRunner.IsRunning(p));
            p.WaitForExit();
            Assert.False(ProcessRunner.IsRunning(p));
            Assert.False(ProcessRunner.IsRunning(null));
        }

        [Fact]
        public void ACrash_IsWritten_AndReportedOnce()
        {
            var dir = Setup.Temp("crash");
            try { throw new InvalidOperationException("No process is associated with this object."); }
            catch (Exception e) { CrashLog.Write(dir, e); }

            var first = CrashLog.TakeLast(dir);
            Assert.NotNull(first);
            Assert.Contains("InvalidOperationException: No process is associated with this object.", first!.Value.Summary);
            Assert.True(File.Exists(first.Value.File));
            Assert.Contains("ACrash_IsWritten_AndReportedOnce", File.ReadAllText(first.Value.File)); // the stack trace is kept
            Assert.Null(CrashLog.TakeLast(dir));
        }
    }
}
