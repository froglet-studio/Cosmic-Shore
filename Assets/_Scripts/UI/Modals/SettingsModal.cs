using CosmicShore.Core;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    public class SettingsModal : ModalWindowManager
    {
        [Inject] GameSetting gameSetting;
        [SerializeField] Transform controllerConfiguratorHost;
        [SerializeField] bool buildControllerConfigurator = true;

        ControllerConfiguratorPanel controllerConfiguratorPanel;

        protected override void Start()
        {
            base.Start();
            EnsureControllerConfigurator();
        }

        protected override void Update()
        {
            if (controllerConfiguratorPanel != null && controllerConfiguratorPanel.IsOverlayOpen)
                return;

            base.Update();
        }

        void EnsureControllerConfigurator()
        {
            if (!buildControllerConfigurator)
                return;

            var host = controllerConfiguratorHost != null
                ? controllerConfiguratorHost
                : FindDeepChild(transform, "OptionsBorder")
                  ?? transform.Find("Content List")
                  ?? transform.Find("Content")
                  ?? transform;

            controllerConfiguratorPanel = GetComponentInChildren<ControllerConfiguratorPanel>(true);
            if (controllerConfiguratorPanel != null)
                return;

            ExpandSettingsBoxForController(host);

            var go = new UnityEngine.GameObject("Controller Configurator", typeof(UnityEngine.RectTransform));
            go.transform.SetParent(host, false);
            controllerConfiguratorPanel = go.AddComponent<ControllerConfiguratorPanel>();
            controllerConfiguratorPanel.Initialize(gameSetting, transform, FindLauncherTemplate(transform));
        }

        static Transform FindDeepChild(Transform parent, string childName)
        {
            if (parent == null)
                return null;

            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == childName)
                    return child;

                var nested = FindDeepChild(child, childName);
                if (nested != null)
                    return nested;
            }

            return null;
        }

        static Button FindLauncherTemplate(Transform host)
        {
            if (host == null)
                return null;

            var buttons = host.GetComponentsInChildren<Button>(true);
            foreach (var button in buttons)
            {
                if (button == null)
                    continue;

                var name = button.name.ToLowerInvariant();
                if (name.Contains("bug reporting"))
                    return button;
            }

            foreach (var button in buttons)
            {
                if (button == null)
                    continue;

                var name = button.name.ToLowerInvariant();
                if (name.Contains("submit"))
                    return button;
            }

            foreach (var button in buttons)
            {
                if (button != null && !button.name.ToLowerInvariant().Contains("close"))
                    return button;
            }

            return null;
        }

        static void ExpandSettingsBoxForController(Transform host)
        {
            if (host == null || host.name != "OptionsBorder")
                return;

            var rect = host as RectTransform;
            if (rect == null)
                return;

            rect.sizeDelta = new Vector2(rect.sizeDelta.x, rect.sizeDelta.y + 56f);
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, rect.anchoredPosition.y - 28f);

            var options = FindDeepChild(host, "Options") as RectTransform;
            if (options != null)
                options.anchoredPosition = new Vector2(options.anchoredPosition.x, options.anchoredPosition.y + 56f);
        }

        public void ToggleMusicEnabledSetting()
        {
            gameSetting.ChangeMusicEnabledSetting();
        }
        public void AdjustMusicLevel(float level)
        {
            CSDebug.Log($"Music Level: {level}");
            gameSetting.SetMusicLevel(level);
        }
        public void AdjustSFXLevel(float level)
        {
            gameSetting.SetSFXLevel(level);
        }
        public void AdjustHapticsLevel(float level)
        {
            gameSetting.SetHapticsLevel(level);
        }
        public void ToggleSFXEnabledSetting()
        {
            gameSetting.ChangeSFXEnabledSetting();
        }
        public void ToggleHapticEnabledSetting()
        {
            gameSetting.ChangeHapticsEnabledSetting();
        }
        public void ToggleInvertYEnabledSetting()
        {
            gameSetting.ChangeInvertYEnabledStatus();
        }
        public void ToggleInvertThrottleEnabledSetting()
        {
            gameSetting.ChangeInvertThrottleEnabledStatus();
        }
        public void ToggleJoystickVisualsEnabledSetting()
        {
            gameSetting.ChangeJoystickVisualsStatus();
        }
    }
}
