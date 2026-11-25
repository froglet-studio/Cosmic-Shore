#if UNITY_EDITOR
using System.IO;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Pins the Sparrow's barrel roll to the RIGHT stick as a vessel dynamic (Shombith, 2026-11-24):
    /// any direction rolls, holding repeats, no boost press arms it and no element scales it.
    /// Source and asset reads, because the trigger lives in Update on a live vessel.
    /// <para>Lives under an Editor/ folder per CLAUDE.md.</para>
    /// </summary>
    public class SparrowBarrelRollStickTests
    {
        const string Controller = "Assets/_Scripts/Controller/Vessel/BarrelRollController.cs";
        const string SparrowPrefab = "Assets/_Prefabs/Spacevessels/Sparrow.prefab";
        const string MouseStrategy = "Assets/_Scripts/Controller/IO/SingleStickMouseInputStrategy.cs";

        static string Read(string path)
        {
            Assert.IsTrue(File.Exists(path), $"missing: {path}");
            return File.ReadAllText(path);
        }

        static string Code(string src)
        {
            // Drop comment lines so a doc sentence never satisfies or fails a code check.
            var sb = new System.Text.StringBuilder();
            foreach (var line in src.Split('\n'))
            {
                var t = line.TrimStart();
                if (t.StartsWith("//") || t.StartsWith("///")) continue;
                sb.AppendLine(line);
            }
            return sb.ToString();
        }

        [Test]
        public void TheRollReadsTheRightStick_NotTheLeft()
        {
            string code = Code(Read(Controller));
            StringAssert.Contains("RightNormalizedJoystickPosition", code);
            StringAssert.DoesNotContain("LeftNormalizedJoystickPosition", code,
                "the left stick steers the Sparrow; a hard turn must never roll it");
        }

        [Test]
        public void NoBoostPressArmsIt_AndNoElementScalesIt()
        {
            string code = Code(Read(Controller));
            StringAssert.DoesNotContain("IsBoosting", code, "the roll left the boost button");
            StringAssert.DoesNotContain("ElementalFloat", code, "a vessel dynamic: no element changes it");
            StringAssert.DoesNotContain("rollTimeScale", Read(SparrowPrefab));
        }

        [Test]
        public void TheTriggerIsOwnerGated()
        {
            StringAssert.Contains("IsLocalPilot", Code(Read(Controller)),
                "the right stick is replicated to every peer; only the local pilot may roll from it");
        }

        [Test]
        public void TheMousePilotGetsTheRightStickFromKeys()
        {
            string code = Code(Read(MouseStrategy));
            StringAssert.Contains("RightNormalizedJoystickPosition = rightKeys", code,
                "P ; L ' are the one-thumb hull's right stick on the mouse scheme");
        }
    }
}
#endif
