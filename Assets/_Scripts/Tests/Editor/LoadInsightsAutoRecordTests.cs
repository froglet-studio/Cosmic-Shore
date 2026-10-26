#if UNITY_EDITOR
using System.Reflection;
using CosmicShore.Utility.PerformanceBenchmark;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// B36 (Docs/PartySystem/BUGS.md): the Bloomrush -> menu load froze ~10 s and Unity Relay dropped the host,
    /// and no recording existed because Record Insight Mode is armed by hand. Online play now records every load
    /// by itself (Yash, 2026-10-25: "it should not be manually done"). Arming by hand still works offline.
    /// </summary>
    [TestFixture]
    public class LoadInsightsAutoRecordTests
    {
        static readonly FieldInfo s_armedCached = typeof(LoadInsights).GetField("s_armedCached", BindingFlags.NonPublic | BindingFlags.Static);
        static readonly FieldInfo s_armedRead = typeof(LoadInsights).GetField("s_armedRead", BindingFlags.NonPublic | BindingFlags.Static);

        bool _armedBefore, _readBefore;
        System.Func<bool> _onlineBefore;

        [SetUp]
        public void SetUp()
        {
            Assert.IsNotNull(s_armedCached, "LoadInsights keeps its armed flag in s_armedCached");
            _armedBefore = (bool)s_armedCached.GetValue(null);
            _readBefore = (bool)s_armedRead.GetValue(null);
            _onlineBefore = LoadInsights.OnlineProvider;
        }

        [TearDown]
        public void TearDown()
        {
            s_armedCached.SetValue(null, _armedBefore);
            s_armedRead.SetValue(null, _readBefore);
            LoadInsights.OnlineProvider = _onlineBefore;
        }

        // Sets the hand-armed flag without touching PlayerPrefs.
        static void Armed(bool on)
        {
            s_armedCached.SetValue(null, on);
            s_armedRead.SetValue(null, true);
        }

        [Test]
        public void OnlinePlay_RecordsWithoutArming()
        {
            Armed(false);
            LoadInsights.OnlineProvider = () => true;
            Assert.IsTrue(LoadInsights.ShouldRecord, "online: every load records by itself");
        }

        [Test]
        public void OfflineAndUnarmed_DoesNotRecord()
        {
            Armed(false);
            LoadInsights.OnlineProvider = () => false;
            Assert.IsFalse(LoadInsights.ShouldRecord, "offline and not armed: nothing records (negative control)");
        }

        [Test]
        public void ArmedByHand_RecordsOffline()
        {
            Armed(true);
            LoadInsights.OnlineProvider = () => false;
            Assert.IsTrue(LoadInsights.ShouldRecord, "Record Insight Mode still works offline");
        }

        [Test]
        public void AThrowingOnlineCheck_DoesNotRecord()
        {
            Armed(false);
            LoadInsights.OnlineProvider = () => throw new System.InvalidOperationException("NetworkManager torn down");
            Assert.IsFalse(LoadInsights.ShouldRecord);
        }

        [Test]
        public void NoNetworkManager_IsNotOnline()
        {
            LoadInsights.OnlineProvider = _onlineBefore;
            Armed(false);
            Assert.IsFalse(LoadInsights.ShouldRecord, "edit mode has no listening NetworkManager");
        }

        [Test]
        public void TheReport_SaysWhichKindOfLoad()
        {
            Assert.AreEqual("launch", new LoadInsightReport().loadKind, "a report defaults to a launch");
            Assert.IsFalse(LoadInsights.IsRecordingMenuLoad, "nothing records in edit mode");
        }

        // B36, the 26 Oct sweep: the boot's load of the menu froze 10.4 s with the Relay host up and was never timed.
        [Test]
        public void TheBootsMenuLoad_IsRecordedAsBoot_AndEndsAtTheMenu()
        {
            Assert.AreEqual("boot", RecordedKind(() => LoadInsights.BeginBootLoad("Boot - Authentication -> Menu_Main"), out bool endsAtMenu));
            Assert.IsTrue(endsAtMenu, "landing in the menu is the point of a boot load: not aborted, closed when the menu shows");
        }

        [Test]
        public void AGameLaunch_IsStillALaunch_AndAReturnStillAMenuReturn()
        {
            Assert.AreEqual("launch", RecordedKind(() => LoadInsights.BeginLoad("Game launch"), out bool launchEndsAtMenu));
            Assert.IsFalse(launchEndsAtMenu, "a launch ends in the game, so a landing in the menu still aborts it");
            Assert.AreEqual("menuReturn", RecordedKind(() => LoadInsights.BeginLoad("Return to menu", menuReturn: true), out bool returnEndsAtMenu));
            Assert.IsTrue(returnEndsAtMenu);
        }

        static readonly FieldInfo s_hostAvailable = typeof(LoadInsights).GetField("HostAvailable", BindingFlags.NonPublic | BindingFlags.Static);
        static readonly FieldInfo s_recording = typeof(LoadInsights).GetField("s_recording", BindingFlags.NonPublic | BindingFlags.Static);
        static readonly MethodInfo s_buildReport = typeof(LoadInsights).GetMethod("BuildReportUnlocked", BindingFlags.NonPublic | BindingFlags.Static);

        // Starts a recording, reads the kind its report would carry, then drops the recording without writing a file.
        static string RecordedKind(System.Action begin, out bool endsAtMenu)
        {
            bool hostBefore = (bool)s_hostAvailable.GetValue(null);
            try
            {
                s_hostAvailable.SetValue(null, true);
                LoadInsights.OnlineProvider = () => true;
                begin();
                Assert.IsTrue(LoadInsights.IsRecording);
                endsAtMenu = LoadInsights.IsRecordingMenuLoad;
                return ((LoadInsightReport)s_buildReport.Invoke(null, new object[] { "test", true })).loadKind;
            }
            finally
            {
                s_recording.SetValue(null, false);
                s_hostAvailable.SetValue(null, hostBefore);
            }
        }
    }
}
#endif
