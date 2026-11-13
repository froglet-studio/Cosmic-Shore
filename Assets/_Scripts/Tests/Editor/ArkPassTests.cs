#if UNITY_EDITOR
using System.Reflection;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>The Ark's HyperSea pass (Docs/MENU_REDESIGN_B.md section 197): the shipped config names the pass shader,
    /// the glide eases still at both ends, and a tab change hands the page over (the one left takes no input at once, the
    /// one reached rises in and lands at rest).</summary>
    public class ArkPassTests
    {
        GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root) Object.DestroyImmediate(_root);
        }

        [Test]
        public void ShippedConfig_NamesThePassShader()
        {
            var config = AssetDatabase.LoadAssetAtPath<ArkBackdropConfigSO>("Assets/Resources/ArkBackdropConfig.asset");
            Assert.IsNotNull(config, "Assets/Resources/ArkBackdropConfig.asset");
            Assert.IsNotNull(config.PassShader, "author_ark_backdrop_assets.py writes passShader");
            Assert.AreEqual("CosmicShore/UI/ArkPass", config.PassShader.name);
            Assert.Greater(config.PassSeconds, config.DissolveSeconds, "a pass between rooms is the longer change");
            CollectionAssert.AreEquivalent(
                new[] { ArkPassStyle.Membrane, ArkPassStyle.Ripple, ArkPassStyle.CausticVeil }, config.PassStyles,
                "A, B and E at random until one is final (Shombith, 2026-11-13)");
        }

        [Test]
        public void Smootherstep_IsStillAtBothEnds()
        {
            Assert.AreEqual(0f, ArkBackdrop.Smootherstep(0f), 1e-6f);
            Assert.AreEqual(1f, ArkBackdrop.Smootherstep(1f), 1e-6f);
            Assert.AreEqual(0.5f, ArkBackdrop.Smootherstep(0.5f), 1e-6f);
            Assert.Less(ArkBackdrop.Smootherstep(0.02f), 0.001f);
            Assert.Greater(ArkBackdrop.Smootherstep(0.98f), 0.999f);
            float last = 0f;
            for (int i = 1; i <= 50; i++)
            {
                float v = ArkBackdrop.Smootherstep(i / 50f);
                Assert.GreaterOrEqual(v, last);
                last = v;
            }
        }

        [Test]
        public void PickStyle_CoversEveryStyle_AndFallsBackToTheMembrane()
        {
            var styles = new[] { ArkPassStyle.Membrane, ArkPassStyle.Ripple, ArkPassStyle.CausticVeil };
            Assert.AreEqual(ArkPassStyle.Membrane, ArkBackdrop.PickStyle(styles, 0f));
            Assert.AreEqual(ArkPassStyle.Ripple, ArkBackdrop.PickStyle(styles, 0.5f));
            Assert.AreEqual(ArkPassStyle.CausticVeil, ArkBackdrop.PickStyle(styles, 0.99f));
            Assert.AreEqual(ArkPassStyle.CausticVeil, ArkBackdrop.PickStyle(styles, 1f), "Random.value can return 1");
            Assert.AreEqual(ArkPassStyle.Membrane, ArkBackdrop.PickStyle(null, 0.7f));
            Assert.AreEqual(ArkPassStyle.Ripple, ArkBackdrop.PickStyle(new[] { ArkPassStyle.Ripple }, 0.3f));
        }

        static CanvasGroup Page(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            return go.GetComponent<CanvasGroup>();
        }

        [Test]
        public void TabChange_HandsThePageOver_AndLandsAtRest()
        {
            _root = new GameObject("Ark tabs", typeof(RectTransform));
            var view = _root.AddComponent<ArkTabsView>();
            var a = Page(_root.transform, "A");
            var b = Page(_root.transform, "B");
            var rest = new Vector2(3f, 7f);
            ((RectTransform)b.transform).anchoredPosition = rest;
            var tabs = new[] { new ArkTabsView.Tab { page = a }, new ArkTabsView.Tab { page = b } };
            typeof(ArkTabsView).GetField("tabs", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(view, tabs);

            view.Show(0);
            Assert.AreEqual(1f, a.alpha, "the first page of all just shows");

            view.Show(1);
            Assert.IsFalse(a.interactable || a.blocksRaycasts, "the page left takes no input from the press on");
            Assert.AreEqual(1f, a.alpha, "the page left fades rather than vanishing");
            Assert.IsTrue(b.interactable && b.blocksRaycasts);
            Assert.AreEqual(0f, b.alpha, "the page reached waits for the membrane");
            Assert.AreNotEqual(rest, ((RectTransform)b.transform).anchoredPosition, "it rises from below its rest");

            view.OnScreenExit();   // leaving lands every change where it was going
            Assert.AreEqual(0f, a.alpha);
            Assert.AreEqual(1f, b.alpha);
            Assert.AreEqual(rest, ((RectTransform)b.transform).anchoredPosition);
        }
    }
}
#endif
