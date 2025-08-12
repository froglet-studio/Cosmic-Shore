using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CosmicShore.Game.UI
{
    public class ResourceDisplay : MonoBehaviour
    {
        public enum DisplayMode
        {
            LegacyFuelImages = 0,
            SliderFill = 1,
            SpriteSwap = 2
        }

        [Header("Display Mode")]
        [SerializeField] private DisplayMode displayMode = DisplayMode.LegacyFuelImages;

        #region Legacy Fuel Images (Default)
        [Header("Legacy Fuel Images")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Sprite backgroundSprite;
        [SerializeField] private TMP_Text fuelLevelText;
        [SerializeField] private Image fuelLevelImage;
        [SerializeField] private List<Sprite> fuelLevelImages = new List<Sprite>();
        [SerializeField] private BoostFillAnimator resourceFillAnimator;
        #endregion

        #region Slider Fill Setup
        [Header("Slider Fill Setup")]
        [SerializeField] private Image sliderFillImage; // Optional, for smooth bar
        [SerializeField] private List<Image> sliderFillSegments = new List<Image>(); // For segmented lines
        [SerializeField] private Color sliderNormalColor = Color.white;
        [SerializeField] private Color sliderFullColor = Color.red;
        [SerializeField] private bool shouldChangeColor = false;
        #endregion

        #region Sprite Swap Setup
        [Header("Sprite Swap Setup")]
        [SerializeField] private Image spriteSwapImage;
        [SerializeField] private List<Sprite> spriteSwapSprites = new List<Sprite>();
        #endregion

        [Header("General")]
        [SerializeField] private bool verboseLogging = false;

        private float currentLevel;
        private Coroutine sliderRoutine;
        private Coroutine spriteSwapRoutine;

        void Start()
        {
            SetupDisplay();
        }

        public void SetupDisplay()
        {
            currentLevel = 0f;
            switch (displayMode)
            {
                case DisplayMode.LegacyFuelImages:
                    if (backgroundImage && backgroundSprite) backgroundImage.sprite = backgroundSprite;
                    if (fuelLevelImages != null && fuelLevelImages.Count > 0 && fuelLevelImage)
                        fuelLevelImage.sprite = fuelLevelImages[0];
                    if (fuelLevelText) fuelLevelText.text = "0";
                    break;
                case DisplayMode.SliderFill:
                    if (sliderFillImage)
                    {
                        sliderFillImage.fillAmount = 0;
                        sliderFillImage.color = sliderNormalColor;
                    }
                    if (sliderFillSegments != null && sliderFillSegments.Count > 0)
                        foreach (var seg in sliderFillSegments)
                            if (seg) seg.fillAmount = 0;
                    break;
                case DisplayMode.SpriteSwap:
                    if (spriteSwapImage && spriteSwapSprites != null && spriteSwapSprites.Count > 0)
                        spriteSwapImage.sprite = spriteSwapSprites[0];
                    break;
            }
        }

        public void UpdateDisplay(float normalizedLevel)
        {
            currentLevel = Mathf.Clamp01(normalizedLevel);

            switch (displayMode)
            {
                case DisplayMode.LegacyFuelImages:
                    UpdateLegacyDisplay(currentLevel);
                    break;
                case DisplayMode.SliderFill:
                    UpdateSliderDisplay(currentLevel, shouldChangeColor);
                    break;
                case DisplayMode.SpriteSwap:
                    UpdateSpriteSwapDisplay(currentLevel);
                    break;
            }
        }

        #region Display Mode Methods

        private void UpdateLegacyDisplay(float normalizedLevel)
        {
            int maxIndex = fuelLevelImages.Count - 1;
            int idx = Mathf.FloorToInt(normalizedLevel * maxIndex);

            if (fuelLevelImage && fuelLevelImages.Count > 0)
                fuelLevelImage.sprite = fuelLevelImages[Mathf.Clamp(idx, 0, maxIndex)];

            if (fuelLevelText)
                fuelLevelText.text = (normalizedLevel * 100f).ToString("F0");

            if (verboseLogging)
                Debug.Log($"[ResourceDisplay] Legacy: idx={idx}, normalized={normalizedLevel}");
        }

        private void UpdateSliderDisplay(float normalizedLevel, bool shouldChangeColor)
        {
            // Smooth main bar (if you want both)
            if (sliderFillImage)
            {
                sliderFillImage.fillAmount = normalizedLevel;
                if (shouldChangeColor)
                {
                    sliderFillImage.color = (normalizedLevel >= 0.99f) ? sliderFullColor : sliderNormalColor;
                }
            }

            // Segmented lines
            if (sliderFillSegments != null && sliderFillSegments.Count > 0)
            {
                int totalSegments = sliderFillSegments.Count;
                float fillPerSegment = 1f / totalSegments;
                for (int i = 0; i < totalSegments; i++)
                {
                    float segmentFill = Mathf.Clamp01((normalizedLevel - (i * fillPerSegment)) * totalSegments);
                    if (sliderFillSegments[i]) sliderFillSegments[i].fillAmount = segmentFill;
                }
            }

            if (verboseLogging)
                Debug.Log($"[ResourceDisplay] SegmentedSlider: fill={normalizedLevel}");
        }

        private void UpdateSpriteSwapDisplay(float normalizedLevel)
        {
            if (!spriteSwapImage || spriteSwapSprites.Count == 0) return;
            int maxIndex = spriteSwapSprites.Count - 1;
            int idx = Mathf.FloorToInt(normalizedLevel * maxIndex);
            spriteSwapImage.sprite = spriteSwapSprites[Mathf.Clamp(idx, 0, maxIndex)];

            if (verboseLogging)
                Debug.Log($"[ResourceDisplay] SpriteSwap: idx={idx}, normalized={normalizedLevel}");
        }

        #endregion

        #region Animation API

        public void AnimateFillDown(float duration, float from)
        {
            switch (displayMode)
            {
                case DisplayMode.LegacyFuelImages:
                    if (resourceFillAnimator != null)
                        resourceFillAnimator.AnimateFillDown(duration, from);
                    break;
                case DisplayMode.SliderFill:
                    if (sliderRoutine != null) StopCoroutine(sliderRoutine);
                    sliderRoutine = StartCoroutine(AnimateSliderFillRoutine(from, 0f, duration, false));
                    break;
                case DisplayMode.SpriteSwap:
                    if (spriteSwapRoutine != null) StopCoroutine(spriteSwapRoutine);
                    spriteSwapRoutine = StartCoroutine(AnimateSpriteSwapRoutine(from, 0f, duration));
                    break;
            }
        }

        public void AnimateFillUp(float duration, float to)
        {
            switch (displayMode)
            {
                case DisplayMode.LegacyFuelImages:
                    if (resourceFillAnimator != null)
                        resourceFillAnimator.AnimateFillUp(duration, to);
                    break;

                case DisplayMode.SliderFill:
                    if (sliderRoutine != null) StopCoroutine(sliderRoutine);
                    var startVal = sliderFillImage ? sliderFillImage.fillAmount : currentLevel; // fallback to currentLevel
                    sliderRoutine = StartCoroutine(AnimateSliderFillRoutine(startVal, to, duration, shouldChangeColor));
                    break;

                case DisplayMode.SpriteSwap:
                    if (spriteSwapRoutine != null) StopCoroutine(spriteSwapRoutine);
                    var fromVal = currentLevel;
                    spriteSwapRoutine = StartCoroutine(AnimateSpriteSwapRoutine(fromVal, to, duration));
                    break;
            }
        }
        #endregion

        #region Animation Coroutines

        private IEnumerator AnimateSliderFillRoutine(float from, float to, float duration, bool colorChangeOnFull)
        {
            var t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                var val = Mathf.Lerp(from, to, t / duration);
                currentLevel = val; 

                if (sliderFillImage)
                {
                    sliderFillImage.fillAmount = val;
                    if (colorChangeOnFull)
                        sliderFillImage.color = (val >= 0.99f) ? sliderFullColor : sliderNormalColor;
                }

                if (sliderFillSegments is { Count: > 0 })
                {
                    var totalSegments = sliderFillSegments.Count;
                    var fillPerSegment = 1f / totalSegments;
                    for (var i = 0; i < totalSegments; i++)
                    {
                        var segmentFill = Mathf.Clamp01((val - (i * fillPerSegment)) * totalSegments);
                        if (sliderFillSegments[i]) sliderFillSegments[i].fillAmount = segmentFill;
                    }
                }
                yield return null;
            }

            currentLevel = to; 
            if (sliderFillImage)
            {
                sliderFillImage.fillAmount = to;
                if (colorChangeOnFull)
                    sliderFillImage.color = (to >= 0.99f) ? sliderFullColor : sliderNormalColor;
            }

            if (sliderFillSegments is not { Count: > 0 }) yield break;
            {
                var totalSegments = sliderFillSegments.Count;
                var fillPerSegment = 1f / totalSegments;
                for (var i = 0; i < totalSegments; i++)
                {
                    var segmentFill = Mathf.Clamp01((to - (i * fillPerSegment)) * totalSegments);
                    if (sliderFillSegments[i]) sliderFillSegments[i].fillAmount = segmentFill;
                }
            }
        }

        private IEnumerator AnimateSpriteSwapRoutine(float from, float to, float duration)
        {
            var t = 0f;
            var maxIdx = spriteSwapSprites.Count - 1;
            while (t < duration)
            {
                t += Time.deltaTime;
                var lerpVal = Mathf.Lerp(from, to, t / duration);
                currentLevel = Mathf.Clamp01(lerpVal); 
                var idx = Mathf.FloorToInt(currentLevel * maxIdx);
                if (spriteSwapImage && maxIdx >= 0)
                    spriteSwapImage.sprite = spriteSwapSprites[Mathf.Clamp(idx, 0, maxIdx)];
                yield return null;
            }
            currentLevel = Mathf.Clamp01(to); 
            var endIdx = Mathf.FloorToInt(currentLevel * maxIdx);
            if (spriteSwapImage && maxIdx >= 0)
                spriteSwapImage.sprite = spriteSwapSprites[Mathf.Clamp(endIdx, 0, maxIdx)];
        }

        #endregion

#if UNITY_EDITOR
        [CustomEditor(typeof(ResourceDisplay))]
        public class ResourceDisplayEditor : Editor
        {
            public override void OnInspectorGUI()
            {
                var display = (ResourceDisplay)target;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("displayMode"));

                switch (display.displayMode)
                {
                    case DisplayMode.LegacyFuelImages:
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("backgroundImage"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("backgroundSprite"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("fuelLevelText"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("fuelLevelImage"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("fuelLevelImages"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("resourceFillAnimator"));
                        break;
                    case DisplayMode.SliderFill:
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("sliderFillImage"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("sliderFillSegments"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("sliderNormalColor"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("sliderFullColor"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("shouldChangeColor"));
                        break;
                    case DisplayMode.SpriteSwap:
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("spriteSwapImage"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("spriteSwapSprites"));
                        break;
                }
                EditorGUILayout.PropertyField(serializedObject.FindProperty("verboseLogging"));
                serializedObject.ApplyModifiedProperties();
            }
        }
#endif
    }
}
