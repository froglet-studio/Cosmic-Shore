#if UNITY_EDITOR
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>The What's new window's rule and words (Docs/MENU_REDESIGN_B.md section 216): it opens by itself once
    /// per update and never over anything else, and the shipped asset has an entry to show.</summary>
    [TestFixture]
    public class WhatsNewRulesTests
    {
        static bool Open(string newest, string seen, bool guided = false, bool flying = false, bool window = false,
            bool card = false) => WhatsNewRules.ShouldOpenByItself(newest, seen, guided, flying, window, card);

        [Test]
        public void OpensForAnUpdateNotYetShown() => Assert.IsTrue(Open("0.3", "0.2"));

        [Test]
        public void OpensOnAFirstVisit() => Assert.IsTrue(Open("0.3", ""));

        [Test]
        public void DoesNotOpenTwiceForOneUpdate() => Assert.IsFalse(Open("0.3", "0.3"));

        [Test]
        public void DoesNotOpenWithNoUpdate()
        {
            Assert.IsFalse(Open(null, ""));
            Assert.IsFalse(Open("  ", "0.2"));
        }

        [Test]
        public void NeverOpensOverAnythingElse()
        {
            Assert.IsFalse(Open("0.3", "0.2", guided: true));
            Assert.IsFalse(Open("0.3", "0.2", flying: true));
            Assert.IsFalse(Open("0.3", "0.2", window: true));
            Assert.IsFalse(Open("0.3", "0.2", card: true));
        }

        [Test]
        public void TheShippedWordsHaveANewestUpdateWithAnId()
        {
            var data = Resources.Load<WhatsNewSO>(WhatsNewSO.ResourcePath);
            Assert.IsNotNull(data, "Assets/Resources/WhatsNew.asset is missing");
            Assert.IsNotNull(data.Newest, "WhatsNew.asset lists no update");
            Assert.IsFalse(string.IsNullOrWhiteSpace(data.Newest.Id), "the newest update has no id, so it never opens by itself");
            Assert.LessOrEqual(data.Newest.Features.Count, 3, "the window shows at most three features");
        }
    }
}
#endif
