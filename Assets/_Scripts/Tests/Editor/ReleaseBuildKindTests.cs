#if UNITY_EDITOR
using CosmicShore.Editor;
using NUnit.Framework;
using UnityEditor;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The release guards' one decision (<see cref="ReleaseBuildKind"/>): a build is a development build
    /// only when its own options say so. The editor's Development checkbox is not an input, so a release
    /// build made while it is ticked is still guarded.
    /// </summary>
    public class ReleaseBuildKindTests
    {
        [Test]
        public void ReleaseOptions_AreNotDevelopment() =>
            Assert.IsFalse(ReleaseBuildKind.IsDevelopment(BuildOptions.None));

        [Test]
        public void DevelopmentFlag_IsDevelopment() =>
            Assert.IsTrue(ReleaseBuildKind.IsDevelopment(BuildOptions.Development | BuildOptions.AllowDebugging));

        [Test]
        public void OtherFlags_DoNotMakeABuildDevelopment() =>
            Assert.IsFalse(ReleaseBuildKind.IsDevelopment(
                BuildOptions.AllowDebugging | BuildOptions.ConnectWithProfiler | BuildOptions.CompressWithLz4HC));

        [Test]
        public void TheCheckboxIsNotAnInput()
        {
            bool saved = EditorUserBuildSettings.development;
            try
            {
                EditorUserBuildSettings.development = true;
                Assert.IsFalse(ReleaseBuildKind.IsDevelopment(BuildOptions.None),
                    "a release build made while the Development checkbox is ticked must still be guarded");
            }
            finally
            {
                EditorUserBuildSettings.development = saved;
            }
        }
    }
}
#endif
