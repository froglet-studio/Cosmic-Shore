using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    public class ControllerConfiguratorPanel : MonoBehaviour
    {
        private enum BindingTarget
        {
            None,
            LeftStick,
            RightStick,
            Button1,
            Button2,
            Button3,
            LeftTrigger,
            RightTrigger,
            Throttle,
            Flip,
        }

        private static readonly BindingTarget[] CaptureAllOrder =
        {
            BindingTarget.LeftStick,
            BindingTarget.RightStick,
            BindingTarget.Button1,
            BindingTarget.Button2,
            BindingTarget.Button3,
            BindingTarget.LeftTrigger,
            BindingTarget.RightTrigger,
            BindingTarget.Throttle,
            BindingTarget.Flip,
        };

        private readonly Dictionary<BindingTarget, TMP_Text> bindingValueLabels = new();

        private GameSetting gameSetting;
        private ControllerMappingProfile draft;
        private Transform overlayParent;
        private Button launcherTemplate;
        private GameObject overlay;
        private GameObject presetList;
        private TMP_Text presetButtonLabel;
        private TMP_Text connectedControllerLabel;
        private TMP_Text statusLabel;

        private BindingTarget activeCaptureTarget = BindingTarget.None;
        private bool captureAllActive;
        private int captureAllIndex;
        private int captureInputReadyFrame;

        public bool IsOverlayOpen => overlay != null && overlay.activeSelf;
        public bool IsCaptureActive => activeCaptureTarget != BindingTarget.None;

        public void Initialize(GameSetting setting, Transform overlayRoot = null, Button visualTemplate = null)
        {
            gameSetting = setting != null ? setting : FindFirstObjectByType<GameSetting>();
            overlayParent = overlayRoot != null ? overlayRoot : transform;
            launcherTemplate = visualTemplate;
            draft = gameSetting != null
                ? gameSetting.GetControllerMapping()
                : ControllerMappingStore.Current.Clone();

            BuildUi();
            RefreshUi();
        }

        private void Update()
        {
            UpdateConnectedControllerLabel();

            if (activeCaptureTarget != BindingTarget.None)
                PollCapture();
        }

        private void BuildUi()
        {
            var rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var launcher = CreateLauncherButton();
            var launcherRect = launcher.GetComponent<RectTransform>();
            launcherRect.anchorMin = new Vector2(0.5f, 0f);
            launcherRect.anchorMax = new Vector2(0.5f, 0f);
            launcherRect.pivot = new Vector2(0.5f, 0f);
            launcherRect.anchoredPosition = new Vector2(0f, 16f);
            launcherRect.sizeDelta = launcherTemplate != null && launcherTemplate.transform is RectTransform templateRect
                ? templateRect.sizeDelta
                : new Vector2(180f, 38f);
            launcher.GetComponent<Button>().onClick.AddListener(ToggleOverlay);

            overlay = CreatePanel("Controller Configurator Overlay", overlayParent, new Color(0.01f, 0.03f, 0.06f, 0.95f));
            var overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = new Vector2(0.06f, 0.08f);
            overlayRect.anchorMax = new Vector2(0.94f, 0.92f);
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            overlay.SetActive(false);

            var content = CreatePanel("Controller Configurator Content", overlay.transform, new Color(0f, 0f, 0f, 0f));
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = new Vector2(22f, 18f);
            contentRect.offsetMax = new Vector2(-22f, -18f);

            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            var titleRow = CreateRow("Title Row", content.transform, 48f);
            CreateLabel("Controller", titleRow.transform, 28, FontStyles.Bold, TextAlignmentOptions.Left);
            var close = CreateButton("Close", titleRow.transform, new Color(0.17f, 0.06f, 0.10f, 1f), new Color(1f, 0.55f, 0.62f, 1f));
            close.GetComponent<Button>().onClick.AddListener(CloseOverlay);
            close.GetComponent<LayoutElement>().preferredWidth = 108f;

            connectedControllerLabel = CreateLabel("No controller", content.transform, 16, FontStyles.Normal, TextAlignmentOptions.Left);

            var presetColumn = CreatePanel("Preset Column", content.transform, new Color(0.04f, 0.08f, 0.12f, 0.85f));
            presetColumn.AddComponent<LayoutElement>().preferredHeight = 44f;
            var presetLayout = presetColumn.AddComponent<VerticalLayoutGroup>();
            presetLayout.spacing = 2f;
            presetLayout.childForceExpandHeight = false;
            presetLayout.childForceExpandWidth = true;

            var presetButton = CreateButton("Preset", presetColumn.transform, new Color(0.06f, 0.18f, 0.24f, 1f), new Color(0.8f, 1f, 1f, 1f));
            presetButton.GetComponent<Button>().onClick.AddListener(TogglePresetList);
            presetButtonLabel = presetButton.GetComponentInChildren<TMP_Text>();

            presetList = CreatePanel("Preset List", presetColumn.transform, new Color(0.02f, 0.05f, 0.08f, 0.98f));
            var presetListLayout = presetList.AddComponent<VerticalLayoutGroup>();
            presetListLayout.spacing = 2f;
            presetListLayout.childForceExpandHeight = false;
            presetListLayout.childForceExpandWidth = true;
            foreach (var preset in ControllerMappingStore.PresetIds)
            {
                var capturedPreset = preset;
                var button = CreateButton(ControllerMappingPresets.GetLabel(preset), presetList.transform, new Color(0.05f, 0.12f, 0.18f, 1f), Color.white);
                button.GetComponent<Button>().onClick.AddListener(() => SelectPreset(capturedPreset));
                button.GetComponent<LayoutElement>().preferredHeight = 34f;
            }
            presetList.SetActive(false);

            var actionRow = CreateRow("Actions Row", content.transform, 38f);
            var mapAll = CreateButton("Map All", actionRow.transform, new Color(0.05f, 0.22f, 0.16f, 1f), new Color(0.65f, 1f, 0.84f, 1f));
            mapAll.GetComponent<Button>().onClick.AddListener(StartCaptureAll);
            var reset = CreateButton("Reset", actionRow.transform, new Color(0.14f, 0.10f, 0.04f, 1f), new Color(1f, 0.78f, 0.42f, 1f));
            reset.GetComponent<Button>().onClick.AddListener(() => SelectPreset(ControllerMappingPresetId.XboxWindows));

            CreateBindingRows(content.transform);

            statusLabel = CreateLabel("Ready", content.transform, 15, FontStyles.Normal, TextAlignmentOptions.Left);
            statusLabel.color = new Color(0.7f, 0.95f, 1f, 1f);
        }

        private void CreateBindingRows(Transform parent)
        {
            CreateBindingRow(parent, BindingTarget.LeftStick, "Left Stick");
            CreateBindingRow(parent, BindingTarget.RightStick, "Right Stick");
            CreateBindingRow(parent, BindingTarget.Button1, "Button 1");
            CreateBindingRow(parent, BindingTarget.Button2, "Button 2");
            CreateBindingRow(parent, BindingTarget.Button3, "Button 3");
            CreateBindingRow(parent, BindingTarget.LeftTrigger, "Left Trigger");
            CreateBindingRow(parent, BindingTarget.RightTrigger, "Right Trigger");
            CreateBindingRow(parent, BindingTarget.Throttle, "Throttle");
            CreateBindingRow(parent, BindingTarget.Flip, "Flip");
        }

        private void CreateBindingRow(Transform parent, BindingTarget target, string label)
        {
            var row = CreateRow($"{label} Row", parent, 32f);
            CreateLabel(label, row.transform, 16, FontStyles.Normal, TextAlignmentOptions.Left);

            var button = CreateButton("", row.transform, new Color(0.04f, 0.10f, 0.15f, 1f), Color.white);
            button.GetComponent<Button>().onClick.AddListener(() => StartCapture(target, false));
            button.GetComponent<LayoutElement>().preferredWidth = 220f;
            bindingValueLabels[target] = button.GetComponentInChildren<TMP_Text>();
        }

        private void ToggleOverlay()
        {
            overlay.SetActive(!overlay.activeSelf);
            if (overlay.activeSelf)
            {
                EventSystem.current?.SetSelectedGameObject(null);
                RefreshUi();
            }
            else
            {
                activeCaptureTarget = BindingTarget.None;
                captureAllActive = false;
            }
        }

        private void CloseOverlay()
        {
            activeCaptureTarget = BindingTarget.None;
            captureAllActive = false;
            overlay.SetActive(false);
            EventSystem.current?.SetSelectedGameObject(null);
        }

        private void TogglePresetList()
        {
            presetList.SetActive(!presetList.activeSelf);
        }

        private void SelectPreset(ControllerMappingPresetId preset)
        {
            presetList.SetActive(false);

            if (gameSetting != null)
            {
                gameSetting.ApplyControllerPreset(preset);
                draft = gameSetting.GetControllerMapping();
            }
            else
            {
                draft = ControllerMappingStore.ApplyPreset(preset, true);
            }

            activeCaptureTarget = BindingTarget.None;
            captureAllActive = false;
            RefreshUi();
        }

        private void StartCaptureAll()
        {
            captureAllActive = true;
            captureAllIndex = 0;
            draft = ControllerMappingStore.Current.Clone();
            StartCapture(CaptureAllOrder[captureAllIndex], true);
        }

        private void StartCapture(BindingTarget target, bool keepCaptureAllState)
        {
            if (Gamepad.current == null)
            {
                statusLabel.text = "Connect a controller first.";
                return;
            }

            activeCaptureTarget = target;
            captureInputReadyFrame = Time.frameCount + 2;
            EventSystem.current?.SetSelectedGameObject(null);

            if (!keepCaptureAllState)
                captureAllActive = false;

            statusLabel.text = IsVectorTarget(target)
                ? $"Move {GetTargetLabel(target)}"
                : $"Press {GetTargetLabel(target)}";
        }

        private void PollCapture()
        {
            var gamepad = Gamepad.current;
            if (gamepad == null)
            {
                statusLabel.text = "Controller disconnected.";
                return;
            }

            if (Time.frameCount < captureInputReadyFrame)
                return;

            if (IsVectorTarget(activeCaptureTarget))
            {
                var source = ControllerMappingRuntime.DetectMovedVector(gamepad);
                if (source == GamepadVectorSource.None)
                    return;

                ApplyVectorSource(activeCaptureTarget, source);
                CompleteCapture(ControllerMappingRuntime.GetVectorDisplayName(source));
            }
            else
            {
                var source = ControllerMappingRuntime.DetectPressedButton(gamepad);
                if (source == GamepadButtonSource.None)
                    return;

                ApplyButtonSource(activeCaptureTarget, source);
                CompleteCapture(ControllerMappingRuntime.GetButtonDisplayName(source));
            }
        }

        private void CompleteCapture(string capturedName)
        {
            var completedTarget = activeCaptureTarget;
            activeCaptureTarget = BindingTarget.None;
            RefreshUi();

            if (captureAllActive)
            {
                captureAllIndex++;
                if (captureAllIndex < CaptureAllOrder.Length)
                {
                    statusLabel.text = $"{GetTargetLabel(completedTarget)} = {capturedName}";
                    StartCapture(CaptureAllOrder[captureAllIndex], true);
                    return;
                }

                captureAllActive = false;
            }

            SaveDraft();
            statusLabel.text = $"{GetTargetLabel(completedTarget)} = {capturedName}";
            EventSystem.current?.SetSelectedGameObject(null);
        }

        private void SaveDraft()
        {
            draft.displayName = "Custom";
            if (gameSetting != null)
                gameSetting.SaveControllerMapping(draft);
            else
                ControllerMappingStore.SaveCustom(draft, true);

            draft = ControllerMappingStore.Current.Clone();
            RefreshUi();
        }

        private void RefreshUi()
        {
            draft ??= ControllerMappingStore.Current.Clone();
            presetButtonLabel.text = $"Preset: {draft.displayName}";

            SetBindingLabel(BindingTarget.LeftStick, ControllerMappingRuntime.GetVectorDisplayName(draft.leftStick));
            SetBindingLabel(BindingTarget.RightStick, ControllerMappingRuntime.GetVectorDisplayName(draft.rightStick));
            SetBindingLabel(BindingTarget.Button1, ControllerMappingRuntime.GetButtonDisplayName(draft.button1));
            SetBindingLabel(BindingTarget.Button2, ControllerMappingRuntime.GetButtonDisplayName(draft.button2));
            SetBindingLabel(BindingTarget.Button3, ControllerMappingRuntime.GetButtonDisplayName(draft.button3));
            SetBindingLabel(BindingTarget.LeftTrigger, ControllerMappingRuntime.GetButtonDisplayName(draft.leftTrigger));
            SetBindingLabel(BindingTarget.RightTrigger, ControllerMappingRuntime.GetButtonDisplayName(draft.rightTrigger));
            SetBindingLabel(BindingTarget.Throttle, ControllerMappingRuntime.GetButtonDisplayName(draft.throttle));
            SetBindingLabel(BindingTarget.Flip, ControllerMappingRuntime.GetButtonDisplayName(draft.flip));
        }

        private void UpdateConnectedControllerLabel()
        {
            if (connectedControllerLabel == null)
                return;

            var gamepad = Gamepad.current;
            connectedControllerLabel.text = gamepad == null
                ? "Controller: none"
                : $"Controller: {gamepad.displayName}";
        }

        private void SetBindingLabel(BindingTarget target, string value)
        {
            if (bindingValueLabels.TryGetValue(target, out var label))
                label.text = value;
        }

        private static bool IsVectorTarget(BindingTarget target)
        {
            return target is BindingTarget.LeftStick or BindingTarget.RightStick;
        }

        private void ApplyVectorSource(BindingTarget target, GamepadVectorSource source)
        {
            if (target == BindingTarget.LeftStick)
                draft.leftStick = source;
            else if (target == BindingTarget.RightStick)
                draft.rightStick = source;
        }

        private void ApplyButtonSource(BindingTarget target, GamepadButtonSource source)
        {
            switch (target)
            {
                case BindingTarget.Button1:
                    draft.button1 = source;
                    break;
                case BindingTarget.Button2:
                    draft.button2 = source;
                    break;
                case BindingTarget.Button3:
                    draft.button3 = source;
                    break;
                case BindingTarget.LeftTrigger:
                    draft.leftTrigger = source;
                    break;
                case BindingTarget.RightTrigger:
                    draft.rightTrigger = source;
                    break;
                case BindingTarget.Throttle:
                    draft.throttle = source;
                    break;
                case BindingTarget.Flip:
                    draft.flip = source;
                    break;
            }
        }

        private static string GetTargetLabel(BindingTarget target)
        {
            return target switch
            {
                BindingTarget.LeftStick => "Left Stick",
                BindingTarget.RightStick => "Right Stick",
                BindingTarget.Button1 => "Button 1",
                BindingTarget.Button2 => "Button 2",
                BindingTarget.Button3 => "Button 3",
                BindingTarget.LeftTrigger => "Left Trigger",
                BindingTarget.RightTrigger => "Right Trigger",
                BindingTarget.Throttle => "Throttle",
                BindingTarget.Flip => "Flip",
                _ => "Binding",
            };
        }

        private static GameObject CreatePanel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            return go;
        }

        private GameObject CreateLauncherButton()
        {
            var button = CreateButton("CONTROLLER", transform, new Color(0.05f, 0.18f, 0.28f, 0.92f), Color.white);
            var launcher = button.GetComponent<Button>();
            launcher.navigation = new Navigation { mode = Navigation.Mode.None };

            if (launcherTemplate != null)
                ApplyTemplateStyle(button, launcherTemplate);

            return button;
        }

        private static void ApplyTemplateStyle(GameObject target, Button template)
        {
            var targetImage = target.GetComponent<Image>();
            var sourceImage = template.GetComponent<Image>();
            if (targetImage != null && sourceImage != null)
            {
                targetImage.sprite = sourceImage.sprite;
                targetImage.type = sourceImage.type;
                targetImage.preserveAspect = sourceImage.preserveAspect;
                targetImage.pixelsPerUnitMultiplier = sourceImage.pixelsPerUnitMultiplier;
                targetImage.material = sourceImage.material;
                targetImage.color = sourceImage.color;
            }

            var targetButton = target.GetComponent<Button>();
            if (targetButton != null)
            {
                targetButton.transition = template.transition;
                targetButton.colors = template.colors;
                targetButton.spriteState = template.spriteState;
                targetButton.animationTriggers = template.animationTriggers;
                targetButton.navigation = new Navigation { mode = Navigation.Mode.None };
            }

            var targetLabel = target.GetComponentInChildren<TMP_Text>(true);
            var sourceLabel = template.GetComponentInChildren<TMP_Text>(true);
            if (targetLabel != null && sourceLabel != null)
            {
                targetLabel.font = sourceLabel.font;
                targetLabel.fontSharedMaterial = sourceLabel.fontSharedMaterial;
                targetLabel.fontSize = sourceLabel.fontSize;
                targetLabel.fontStyle = sourceLabel.fontStyle;
                targetLabel.color = sourceLabel.color;
                targetLabel.alignment = sourceLabel.alignment;
                targetLabel.enableWordWrapping = sourceLabel.enableWordWrapping;
                targetLabel.text = "CONTROLLER";

                var targetRect = targetLabel.GetComponent<RectTransform>();
                var sourceRect = sourceLabel.GetComponent<RectTransform>();
                if (targetRect != null && sourceRect != null)
                {
                    targetRect.anchorMin = sourceRect.anchorMin;
                    targetRect.anchorMax = sourceRect.anchorMax;
                    targetRect.pivot = sourceRect.pivot;
                    targetRect.anchoredPosition = sourceRect.anchoredPosition;
                    targetRect.sizeDelta = sourceRect.sizeDelta;
                    targetRect.offsetMin = sourceRect.offsetMin;
                    targetRect.offsetMax = sourceRect.offsetMax;
                }
            }
        }

        private static GameObject CreateRow(string name, Transform parent, float preferredHeight)
        {
            var row = CreatePanel(name, parent, new Color(0f, 0f, 0f, 0f));
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            row.AddComponent<LayoutElement>().preferredHeight = preferredHeight;
            return row;
        }

        private static TMP_Text CreateLabel(string text, Transform parent, int fontSize, FontStyles style, TextAlignmentOptions alignment)
        {
            var go = new GameObject(text + " Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var label = go.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.alignment = alignment;
            label.color = Color.white;
            label.enableWordWrapping = false;

            return label;
        }

        private static GameObject CreateButton(string text, Transform parent, Color background, Color foreground)
        {
            var go = new GameObject(text + " Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = background;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            var label = CreateLabel(text, go.transform, 16, FontStyles.Bold, TextAlignmentOptions.Center);
            label.color = foreground;
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 4f);
            labelRect.offsetMax = new Vector2(-8f, -4f);

            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 34f;
            return go;
        }
    }
}
