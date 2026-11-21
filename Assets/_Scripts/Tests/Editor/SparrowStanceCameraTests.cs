#if UNITY_EDITOR
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Sparrow's camera height follows its stance: flying, the follow camera sits level with the
    /// hull, directly behind it (no height offset); stopped in the MASS turret stance it keeps
    /// <c>SparrowCameraSettingsSO</c>'s elevated <c>followOffset.y</c>. Driven by
    /// <see cref="ToggleTranslationModeActionExecutor"/> through <c>CustomCameraController.FollowHeightScale</c>.
    /// The Serpent and the Stoat share the executor and must keep their authored height in flight.
    /// </summary>
    public class SparrowStanceCameraTests
    {
        const string ExecutorGuid = "8c3f54abda8145ecabf7c85253cab9ed";

        [Test]
        public void Flying_UsesTheFlightHeight_TheStance_TheFullHeight()
        {
            Assert.AreEqual(0f, ToggleTranslationModeActionExecutor.CameraHeightScale(0f, 0f));
            Assert.AreEqual(1f, ToggleTranslationModeActionExecutor.CameraHeightScale(0f, 1f));
            Assert.AreEqual(0.5f, ToggleTranslationModeActionExecutor.CameraHeightScale(0f, 0.5f), 1e-5f);
            Assert.AreEqual(1f, ToggleTranslationModeActionExecutor.CameraHeightScale(1f, 0f),
                "A vessel that keeps its flight height must never be moved by the stance.");
        }

        [Test]
        public void Sparrow_FliesLevel_SerpentAndStoat_KeepTheirHeight()
        {
            Assert.AreEqual(0f, FlightCameraHeight("Assets/_Prefabs/Spacevessels/Sparrow.prefab"),
                "The Sparrow's elevated camera belongs to the turret stance only.");
            Assert.AreEqual(1f, FlightCameraHeight("Assets/_Prefabs/Spacevessels/Serpent.prefab"));
            Assert.AreEqual(1f, FlightCameraHeight("Assets/_Prefabs/Spacevessels/Stoat.prefab"));
        }

        /// <summary>The executor's authored <c>flightCameraHeight</c>, or its default (1) when unauthored.</summary>
        static float FlightCameraHeight(string prefabPath)
        {
            Assert.IsTrue(File.Exists(prefabPath), $"missing asset: {prefabPath}");
            var yaml = File.ReadAllText(prefabPath).Replace("\r\n", "\n");
            var block = Regex.Match(yaml,
                @"m_Script: \{fileID: 11500000, guid: " + ExecutorGuid + @", type: 3\}\n(?<body>(?:  .*\n)*)");
            Assert.IsTrue(block.Success, $"{prefabPath} carries no ToggleTranslationModeActionExecutor");
            var m = Regex.Match(block.Groups["body"].Value, @"^  flightCameraHeight: (-?[0-9.eE+]+)$",
                                RegexOptions.Multiline);
            return m.Success ? float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : 1f;
        }
    }
}
#endif
