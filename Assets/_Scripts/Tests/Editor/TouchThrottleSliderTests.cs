using CosmicShore.Gameplay;
using CosmicShore.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The phone throttle slider (SCARAB.md §3.11): the whole column takes the touch and the value is read
    /// over the column's own height, at whatever size TouchControlsHud lays it out, and the fill and knob
    /// follow that value. The bars used to keep their build-time size when the root was resized.
    /// </summary>
    public class TouchThrottleSliderTests
    {
        GameObject _parent;
        GameObject _inputGo;
        InputStatus _input;
        TouchThrottleSlider _slider;

        [SetUp]
        public void SetUp()
        {
            _parent = new GameObject("TouchThrottleSliderTests", typeof(RectTransform));
            _inputGo = new GameObject("TouchThrottleSliderTestsInput");
            _input = _inputGo.AddComponent<InputStatus>();
            _slider = TouchThrottleSlider.Create((RectTransform)_parent.transform, null, Color.cyan, 80f, 240f);
            _slider.Bind(_input, 0.5f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_parent);
            Object.DestroyImmediate(_inputGo);
        }

        static PointerEventData At(float x, float y) => new PointerEventData(null) { position = new Vector2(x, y) };

        RectTransform Region => (RectTransform)_slider.transform;

        [Test]
        public void Press_SetsTheThrottleOverTheColumnsHeight()
        {
            Region.sizeDelta = new Vector2(60f, 200f);

            _slider.OnPointerDown(At(0f, 100f));
            Assert.AreEqual(1f, _slider.Value, 1e-3f);
            Assert.AreEqual(1f, _input.Throttle, 1e-3f);

            _slider.OnDrag(At(0f, -100f));
            Assert.AreEqual(0f, _input.Throttle, 1e-3f);

            _slider.OnDrag(At(0f, 0f));
            Assert.AreEqual(0.5f, _input.Throttle, 1e-3f);
        }

        [Test]
        public void ResizingTheColumn_ResizesTheBarsAndKnob()
        {
            Region.sizeDelta = new Vector2(50f, 400f);

            var track = (RectTransform)Region.Find("Track");
            var fill = (RectTransform)Region.Find("Fill");
            var knob = (RectTransform)Region.Find("Knob");
            Assert.AreEqual(400f, track.rect.height, 1e-3f);
            Assert.AreEqual(400f * 0.5f, fill.rect.height, 1e-3f);
            Assert.AreEqual(50f, knob.rect.width, 1e-3f);

            _slider.OnPointerDown(At(0f, 200f));
            Assert.AreEqual(400f, fill.rect.height, 1e-3f);
        }

        [Test]
        public void TheWholeColumnTakesTheTouch()
        {
            var hit = _slider.GetComponent<Image>();
            Assert.IsNotNull(hit);
            Assert.IsTrue(hit.raycastTarget);
            Assert.IsFalse(Region.Find("Track").GetComponent<Image>().raycastTarget);
        }
    }
}
