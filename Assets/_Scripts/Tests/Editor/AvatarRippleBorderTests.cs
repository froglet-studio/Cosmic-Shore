#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.UI;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Guards for the Rev 127 D avatar border (Docs/MENU_REDESIGN_B.md section 217): the avatar keeps its identity and
    /// its place when the border is built round it, the build is idempotent, and a wave runs from the ring to exactly
    /// the halo's rim, where it has faded out.
    /// </summary>
    [TestFixture]
    public class AvatarRippleBorderTests
    {
        GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root) Object.DestroyImmediate(_root);
        }

        Image MakeAvatar(Vector2 position, Vector2 size)
        {
            _root = new GameObject("slot", typeof(RectTransform));
            new GameObject("before", typeof(RectTransform)).transform.SetParent(_root.transform, false);
            var go = new GameObject("AvatarIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(_root.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            new GameObject("after", typeof(RectTransform)).transform.SetParent(_root.transform, false);
            return go.GetComponent<Image>();
        }

        [Test]
        public void EnsureFor_TakesTheAvatarsPlace_AndKeepsTheSameImage()
        {
            var avatar = MakeAvatar(new Vector2(12, -7), new Vector2(72, 72));
            var border = AvatarRippleBorder.EnsureFor(avatar);

            Assert.That(border, Is.Not.Null);
            Assert.That(border.Avatar, Is.SameAs(avatar));
            var holder = (RectTransform)border.transform;
            Assert.That(holder.parent, Is.SameAs(_root.transform));
            Assert.That(holder.GetSiblingIndex(), Is.EqualTo(1), "the holder sits where the avatar was");
            Assert.That(holder.anchoredPosition, Is.EqualTo(new Vector2(12, -7)));
            Assert.That(holder.sizeDelta, Is.EqualTo(new Vector2(72, 72)));
            Assert.That(avatar.GetComponentInParent<Mask>(), Is.Not.Null, "the avatar is cut by the round mask");
            Assert.That(avatar.rectTransform.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(avatar.rectTransform.anchorMax, Is.EqualTo(Vector2.one));
        }

        [Test]
        public void EnsureFor_IsIdempotent()
        {
            var avatar = MakeAvatar(Vector2.zero, new Vector2(64, 64));
            var first = AvatarRippleBorder.EnsureFor(avatar);
            var second = AvatarRippleBorder.EnsureFor(avatar);
            Assert.That(second, Is.SameAs(first));
            Assert.That(_root.GetComponentsInChildren<AvatarRippleBorder>(true).Length, Is.EqualTo(1));
        }

        [Test]
        public void Wave_LeavesTheRing_AndFadesOutAtTheHalosRim()
        {
            AvatarRippleBorder.WaveAt(0f, 1.3f, 0.9f, out float s0, out float a0);
            AvatarRippleBorder.WaveAt(1f, 1.3f, 0.9f, out float s1, out float a1);
            AvatarRippleBorder.WaveAt(0.5f, 1.3f, 0.9f, out float sh, out float ah);
            Assert.That(s0, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(a0, Is.EqualTo(0.9f).Within(1e-5f));
            Assert.That(s1, Is.EqualTo(1.3f).Within(1e-5f));
            Assert.That(a1, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(sh, Is.GreaterThan(1.15f), "ease-out: past halfway at half time");
            Assert.That(ah, Is.LessThan(0.45f));
        }

        [Test]
        public void BandFraction_MatchesTheBoard()
        {
            // 340 px avatar: ring 340 * 0.016 + 1.6 = 7.04 px of a 170 px radius.
            Assert.That(AvatarRippleBorder.BandFraction(340f, 0.016f, 1.6f), Is.EqualTo(7.04f / 170f).Within(1e-5f));
            Assert.That(AvatarRippleBorder.BandFraction(0f, 0.016f, 1.6f), Is.EqualTo(0f));
        }
    }
}
#endif
