using System;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The screens the live flags show (Docs/LAUNCH/LIVE_FLAGS.md): the blocking <b>update required</b> wall when this
    /// build is below the flags' <c>minimumVersion</c>, the dismissible <b>update available</b> notice below
    /// <c>recommendedVersion</c>, and the flags' optional <b>message</b>, once per distinct message.
    ///
    /// <para>It builds its own UI at runtime, as <see cref="FairPlayNoticeOverlay"/> and <see cref="PrivacyConsentOverlay"/>
    /// do (nothing to place in a scene, so nothing to forget), in the same kit: the panel and button sprites from
    /// <c>PrivacyConsentConfig</c>, every word from <see cref="LaunchFlagsConfigSO"/> (the message's own words come
    /// from the flags document, which is authored in git). Colours are the style guide's tokens: Lime for Update (the
    /// call to action), Fire for the one line that says why the game cannot go on, Blue for the rest.</para>
    ///
    /// <para>The wall is shown only on an answer from the server this session (<see cref="LaunchFlags.UpdateRequired"/>),
    /// sits on the consent layer above every menu, cannot be closed, keeps the pad's selection on its own buttons, and
    /// survives scene loads, so no menu (and no matchmaking) is reachable behind it. It is a UI wall: the match path has
    /// no version check of its own (the protocol version in session data, READINESS.md #6, is that guard).</para>
    /// </summary>
    public class UpdateNoticeOverlay : MonoBehaviour
    {
        /// <summary>The wall sits with the consent screen (32766, UI_STYLE_GUIDE.md section 4); a notice under the fair-play notice.</summary>
        const int WallSortingOrder = 32766;
        const int NoticeSortingOrder = 32764;
        const float PanelWidth = 820f;
        const string MessageSeenKey = "LaunchFlags.MessageSeen";
        const string TitleFontPath = "Fonts & Materials/ALDRICH-REGULAR SDF";
        const string LabelFontPath = "Fonts & Materials/ChakraPetch-Regular SDF";

        enum Kind { Required = 0, Recommended = 1, Message = 2 }

        static UpdateNoticeOverlay s_instance;
        static string s_recommendedShownFor;

        Kind _kind;
        LaunchFlagsConfigSO _words;
        PrivacyConsentConfigSO _kit;
        TMP_FontAsset _titleFont;
        TMP_FontAsset _labelFont;
        Button _primary;
        Button _secondary;
        int _shownFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_instance = null;
            s_recommendedShownFor = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Subscribe()
        {
            if (WebToybox.IsBuild) return;
            LaunchFlags.OnChanged -= Evaluate;
            LaunchFlags.OnChanged += Evaluate;
        }

        /// <summary>Shows what the flags in force call for, one screen at a time; the wall replaces any notice.</summary>
        static void Evaluate()
        {
            if (LaunchFlags.UpdateRequired)
            {
                if (s_instance && s_instance._kind == Kind.Required) return;
                if (s_instance) Destroy(s_instance.gameObject);
                Create(Kind.Required);
                return;
            }

            // The wall is never taken down by a later answer in the same run: a build that was too old a moment ago
            // is still that build. Only a notice gives way.
            if (s_instance) return;

            var doc = LaunchFlags.Current;
            if (LaunchFlags.UpdateRecommended && s_recommendedShownFor != doc.RecommendedVersion)
            {
                s_recommendedShownFor = doc.RecommendedVersion;
                Create(Kind.Recommended);
                return;
            }

            if (LaunchFlags.Source == LaunchFlagsSource.Live && !string.IsNullOrEmpty(doc.MessageTitle)
                && ReadMessageSeen() != MessageKey(doc))
            {
                WriteMessageSeen(MessageKey(doc));
                Create(Kind.Message);
            }
        }

        static void Create(Kind kind)
        {
            var go = new GameObject(kind == Kind.Required ? "[UpdateRequired_Overlay]" : "[LaunchNotice_Overlay]");
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<UpdateNoticeOverlay>();
            s_instance._kind = kind;
            s_instance._shownFrame = Time.frameCount;
            s_instance._words = LaunchFlagsConfigSO.LoadOrDefault();
            s_instance._kit = Resources.Load<PrivacyConsentConfigSO>("PrivacyConsentConfig");
            s_instance.Build();
        }

        /// <summary>The message itself is the key (at most 80 + 400 characters): a string hash is not stable across runs.</summary>
        static string MessageKey(LaunchFlagsDocument doc) => doc.MessageTitle + "\n" + doc.MessageBody;

        static string ReadMessageSeen() => PlayerPrefs.GetString(MessageSeenKey, string.Empty);

        static void WriteMessageSeen(string key)
        {
            PlayerPrefs.SetString(MessageSeenKey, key);
            PlayerPrefs.Save();
        }

        void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }

        void Update()
        {
            if (_kind == Kind.Required)
            {
                // Keep the pad on the wall: automatic navigation would otherwise walk to a menu button underneath.
                // With no button shown (no link, a phone), nothing is selected, so nothing behind it can be pressed.
                var events = EventSystem.current;
                if (events)
                {
                    var keep = _primary && _primary.gameObject.activeInHierarchy ? _primary
                             : _secondary && _secondary.gameObject.activeInHierarchy ? _secondary : null;
                    var selected = events.currentSelectedGameObject;
                    bool strayed = selected ? !selected.transform.IsChildOf(transform) : (bool)keep;
                    if (strayed)
                        events.SetSelectedGameObject(keep ? keep.gameObject : null);
                }
                return;
            }

            // The press that closed the screen before this one must not close this one too.
            if (Time.frameCount == _shownFrame) return;
            bool back = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                        || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
            if (back) Close();
        }

        void Build()
        {
            EnsureEventSystem();
            _titleFont = Resources.Load<TMP_FontAsset>(TitleFontPath);
            _labelFont = Resources.Load<TMP_FontAsset>(LabelFontPath);

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = _kind == Kind.Required ? WallSortingOrder : NoticeSortingOrder;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            gameObject.AddComponent<GraphicRaycaster>();

            // The scrim takes every pointer: on the wall it is opaque enough to read as "the game is stopped".
            var scrim = NewRect("Scrim", transform);
            Stretch(scrim);
            scrim.gameObject.AddComponent<Image>().color = Tint(UIColorToken.SurfaceBlack, _kind == Kind.Required ? 0.9f : 0.72f);

            var content = SafeAreaLayer.Create(transform);
            var panel = NewPanel(content);

            var doc = LaunchFlags.Current;
            string current = Application.version ?? string.Empty;
            string url = doc.UpdateUrlFor(Application.platform);
            bool canOpen = !string.IsNullOrEmpty(url);

            switch (_kind)
            {
                case Kind.Required:
                    AddEyebrow(panel, _words.RequiredEyebrow);
                    AddText(panel, _words.RequiredTitle, 30f, _titleFont, UIColorToken.TextLight);
                    AddText(panel, TemplateText.Fill(_words.RequiredReason, "current", current, "minimum", doc.MinimumVersion),
                        18f, _labelFont, UIColorToken.Danger);
                    AddText(panel, TemplateText.Fill(_words.RequiredBody, "current", current, "minimum", doc.MinimumVersion),
                        18f, _labelFont, UIColorToken.NeutralLightest);
                    {
                        var row = NewRow(panel);
                        _primary = AddButton(row, canOpen ? _words.UpdateButton : string.Empty, _words.PrimaryKeycap, true, () => Application.OpenURL(url));
                        _secondary = AddButton(row, CanQuit ? _words.QuitButton : string.Empty, _words.SecondaryKeycap, false, Application.Quit);
                        LinkExplicitly(_primary, _secondary);
                    }
                    break;

                case Kind.Recommended:
                    AddEyebrow(panel, _words.RecommendedEyebrow);
                    AddText(panel, _words.RecommendedTitle, 30f, _titleFont, UIColorToken.TextLight);
                    AddText(panel, TemplateText.Fill(_words.RecommendedBody, "current", current, "recommended", doc.RecommendedVersion),
                        18f, _labelFont, UIColorToken.NeutralLightest);
                    {
                        var row = NewRow(panel);
                        _primary = AddButton(row, canOpen ? _words.UpdateButton : string.Empty, _words.PrimaryKeycap, true, () => { Application.OpenURL(url); Close(); });
                        _secondary = AddButton(row, _words.LaterButton, _words.SecondaryKeycap, false, Close);
                        LinkExplicitly(_primary, _secondary);
                    }
                    break;

                default:
                    AddEyebrow(panel, _words.MessageEyebrow);
                    AddText(panel, doc.MessageTitle, 30f, _titleFont, UIColorToken.TextLight);
                    AddText(panel, doc.MessageBody, 18f, _labelFont, UIColorToken.NeutralLightest);
                    {
                        var row = NewRow(panel);
                        _primary = AddButton(row, _words.CloseButton, _words.SecondaryKeycap, false, Close);
                    }
                    break;
            }

            var first = _primary && _primary.gameObject.activeSelf ? _primary : _secondary;
            var system = EventSystem.current;
            if (system && first && first.gameObject.activeSelf) system.SetSelectedGameObject(first.gameObject);
        }

        /// <summary>A desktop game may offer Quit; a phone app must not (the platforms close apps themselves).</summary>
        static bool CanQuit => Application.platform is RuntimePlatform.WindowsPlayer or RuntimePlatform.OSXPlayer
            or RuntimePlatform.LinuxPlayer or RuntimePlatform.WindowsEditor or RuntimePlatform.OSXEditor
            or RuntimePlatform.LinuxEditor;

        void Close()
        {
            if (_kind == Kind.Required) return;
            if (s_instance == this) s_instance = null;
            Destroy(gameObject);
            // The next screen the flags call for (a message after the update notice).
            Evaluate();
        }

        static void LinkExplicitly(Button a, Button b)
        {
            if (!a || !b) return;
            var na = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = b, selectOnLeft = b };
            var nb = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = a, selectOnLeft = a };
            a.navigation = na;
            b.navigation = nb;
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("[UpdateNotice_EventSystem]", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(es);
        }

        #region Tiny UI toolkit (the PrivacyConsentOverlay kit, for one panel)

        static Color Tint(UIColorToken token, float alpha = 1f)
        {
            var c = UITheme.Resolve(null, token);
            c.a *= alpha;
            return c;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        static Image NewImage(RectTransform r, Sprite sprite, Color color)
        {
            var image = r.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sprite ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            return image;
        }

        static Image AddLayer(RectTransform parent, string name, Sprite sprite, Color color, float outset = 0f)
        {
            var r = NewRect(name, parent);
            Stretch(r);
            r.offsetMin = new Vector2(-outset, -outset);
            r.offsetMax = new Vector2(outset, outset);
            r.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var image = NewImage(r, sprite, color);
            image.raycastTarget = false;
            return image;
        }

        RectTransform NewPanel(Transform parent)
        {
            var panel = NewRect("Panel", parent);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(PanelWidth, 0f);
            NewImage(panel, _kit ? _kit.PanelFillSprite : null, Tint(UIColorToken.SurfaceBlack, 0.96f)).raycastTarget = true;
            AddLayer(panel, "Border", _kit ? _kit.PanelBorderSprite : null, Tint(UIColorToken.NeutralLightest));
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(32, 32, 32, 32);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return panel;
        }

        static RectTransform NewRow(RectTransform parent)
        {
            var row = NewRect("Row", parent);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.padding = new RectOffset(0, 0, 16, 0);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return row;
        }

        void AddEyebrow(Transform parent, string text) =>
            AddText(parent, (text ?? string.Empty).ToUpperInvariant(), 14f, _labelFont, UIColorToken.NeutralLightest).characterSpacing = 14f;

        static TMP_Text AddText(Transform parent, string text, float size, TMP_FontAsset font, UIColorToken color)
        {
            var r = NewRect("Text", parent);
            var tmp = r.gameObject.AddComponent<TextMeshProUGUI>();
            if (font) tmp.font = font;
            tmp.text = text ?? string.Empty;
            tmp.fontSize = size;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.raycastTarget = false;
            tmp.color = Tint(color);
            r.gameObject.SetActive(!string.IsNullOrEmpty(text));   // an empty field shows nothing
            return tmp;
        }

        Button AddButton(Transform parent, string label, string keycap, bool primary, Action onClick)
        {
            var r = NewRect("Button", parent);
            var element = r.gameObject.AddComponent<LayoutElement>();
            element.minWidth = 200f;
            element.preferredHeight = 56f;

            var content = primary ? Tint(UIColorToken.SurfaceBlack) : Tint(UIColorToken.TextLight);
            var fill = NewImage(r, _kit ? _kit.ButtonFillSprite : null, primary ? Tint(UIColorToken.Cta) : Tint(UIColorToken.SurfaceVeryDark));
            AddLayer(r, "Border", _kit ? _kit.ButtonBorderSprite : null, primary ? Tint(UIColorToken.Cta) : Tint(UIColorToken.NeutralLightest));
            var ring = AddLayer(r, "FocusRing", _kit ? _kit.FocusRingSprite : null, Tint(UIColorToken.ControlActive), 6f);

            var layout = r.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 0, 0);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            if (KitInputDevice.ShowsControlHints && !string.IsNullOrEmpty(keycap))
            {
                var cap = AddText(r, keycap, 13f, _labelFont, primary ? UIColorToken.SurfaceBlack : UIColorToken.TextLight);
                cap.alignment = TextAlignmentOptions.Center;
            }
            var tmp = AddText(r, (label ?? string.Empty).ToUpperInvariant(), 18f, _labelFont, primary ? UIColorToken.SurfaceBlack : UIColorToken.TextLight);
            tmp.characterSpacing = 8f;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = content;

            var button = r.gameObject.AddComponent<Button>();
            button.targetGraphic = ring;
            var colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.25f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            fill.raycastTarget = true;
            button.onClick.AddListener(() => onClick?.Invoke());
            r.gameObject.SetActive(!string.IsNullOrEmpty(label));
            return button;
        }

        #endregion
    }
}
