using System;
using System.Collections.Generic;
using CosmicShore.Engine.Events;

namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// Selection-from-a-list control (original contract: TMPro.TMP_Dropdown : Selectable),
    /// serialized under TMP's own field names so authored dropdowns deserialize whole.
    /// <para>
    /// <see cref="Show"/> builds the popup the way the original does: the (inactive)
    /// <c>m_Template</c> is cloned as "Dropdown List" beside the dropdown, the toggle item
    /// inside it is cloned once per option (label + optional image filled, the selected one
    /// on), and a full-screen transparent "Blocker" under the root canvas closes the list when
    /// clicked outside it. Picking an item sets <see cref="value"/> and hides the list.
    /// A dropdown authored without a template keeps the old behaviour — a click cycles to the
    /// next option — so a bare dropdown built in code still exercises value/onValueChanged.
    /// </para>
    /// </summary>
    public class TMP_Dropdown : Selectable, IPointerClickHandler, ISubmitHandler, ICancelHandler
    {
        [Serializable]
        public class OptionData
        {
            [SerializeField] string m_Text;
            [SerializeField] Sprite m_Image;
            [SerializeField] Color m_Color = Color.white;

            public string text { get => m_Text; set => m_Text = value; }
            public Sprite image { get => m_Image; set => m_Image = value; }
            public Color color { get => m_Color; set => m_Color = value; }

            public OptionData() { }
            public OptionData(string text) { m_Text = text; }
            public OptionData(Sprite image) { m_Image = image; }
            public OptionData(string text, Sprite image, Color color) { m_Text = text; m_Image = image; m_Color = color; }
        }

        [Serializable]
        public class OptionDataList
        {
            [SerializeField] List<OptionData> m_Options = new();
            public List<OptionData> options { get => m_Options; set => m_Options = value ?? new List<OptionData>(); }
        }

        [Serializable] public class DropdownEvent : UnityEvent<int> { }

        [SerializeField] RectTransform m_Template;
        [SerializeField] TMP_Text m_CaptionText;
        [SerializeField] Image m_CaptionImage;
        [SerializeField] Graphic m_Placeholder;
        [SerializeField] TMP_Text m_ItemText;
        [SerializeField] Image m_ItemImage;
        [SerializeField] int m_Value;
        [SerializeField] OptionDataList m_Options = new();
        [SerializeField] DropdownEvent m_OnValueChanged = new();
        [SerializeField] float m_AlphaFadeSpeed = 0.15f;

        GameObject m_Dropdown;
        GameObject m_Blocker;
        readonly List<GameObject> m_Items = new();

        public RectTransform template { get => m_Template; set => m_Template = value; }
        public TMP_Text captionText { get => m_CaptionText; set { m_CaptionText = value; RefreshShownValue(); } }
        public Image captionImage { get => m_CaptionImage; set { m_CaptionImage = value; RefreshShownValue(); } }
        public Graphic placeholder { get => m_Placeholder; set { m_Placeholder = value; RefreshShownValue(); } }
        public TMP_Text itemText { get => m_ItemText; set => m_ItemText = value; }
        public Image itemImage { get => m_ItemImage; set => m_ItemImage = value; }
        public float alphaFadeSpeed { get => m_AlphaFadeSpeed; set => m_AlphaFadeSpeed = value; }
        public DropdownEvent onValueChanged { get => m_OnValueChanged; set => m_OnValueChanged = value; }
        public bool IsExpanded => m_Dropdown != null;

        public List<OptionData> options
        {
            get => m_Options.options;
            set
            {
                m_Options.options = value;
                m_Value = ClampValue(m_Value);
                RefreshShownValue();
            }
        }

        public int value
        {
            get => m_Value;
            set => Set(value, sendCallback: true);
        }

        /// <summary>State write without firing onValueChanged (UI sync paths).</summary>
        public void SetValueWithoutNotify(int input) => Set(input, sendCallback: false);

        public void ClearOptions()
        {
            options.Clear();
            m_Value = 0;
            RefreshShownValue();
        }

        public void AddOptions(List<OptionData> newOptions) { options.AddRange(newOptions); RefreshShownValue(); }

        public void AddOptions(List<string> newOptions)
        {
            foreach (var text in newOptions) options.Add(new OptionData(text));
            RefreshShownValue();
        }

        public void AddOptions(List<Sprite> newOptions)
        {
            foreach (var sprite in newOptions) options.Add(new OptionData(sprite));
            RefreshShownValue();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (m_Template != null && m_Template.gameObject.activeSelf) m_Template.gameObject.SetActive(false);
            RefreshShownValue();
        }

        protected override void OnDisable()
        {
            ImmediateDestroyDropdownList();
            base.OnDisable();
        }

        /// <summary>Sync the caption with the selected option (original name).</summary>
        public void RefreshShownValue()
        {
            var opts = options;
            var data = m_Value >= 0 && m_Value < opts.Count ? opts[m_Value] : null;
            if (m_CaptionText != null) m_CaptionText.text = data?.text ?? string.Empty;
            if (m_CaptionImage != null)
            {
                m_CaptionImage.sprite = data?.image;
                m_CaptionImage.enabled = data?.image != null;
            }
            if (m_Placeholder != null) m_Placeholder.enabled = opts.Count == 0 || m_Value < 0;
        }

        void Set(int input, bool sendCallback)
        {
            int clamped = ClampValue(input);
            if (clamped == m_Value)
            {
                RefreshShownValue();
                return;
            }
            m_Value = clamped;
            RefreshShownValue();
            if (sendCallback) m_OnValueChanged.Invoke(m_Value);
        }

        int ClampValue(int input)
        {
            int count = options.Count;
            return count == 0 ? 0 : Mathf.Clamp(input, 0, count - 1);
        }

        public void OnPointerClick(PointerEventData eventData) => Show();
        public void OnSubmit(BaseEventData eventData) => Show();
        public void OnCancel(BaseEventData eventData) => Hide();

        /// <summary>Opens the option list (original contract).</summary>
        public void Show()
        {
            if (!IsActive() || !IsInteractable() || m_Dropdown != null) return;
            var opts = options;
            if (opts.Count == 0) return;

            if (m_Template == null)
            {
                value = (m_Value + 1) % opts.Count; // no authored popup: cycle
                return;
            }

            var item = m_Template.GetComponentInChildren<Toggle>(true);
            if (item == null)
            {
                Debug.LogError("The dropdown template is not valid. The template must have a child GameObject with a Toggle component serving as the item.", this);
                return;
            }

            var rootCanvas = FindRootCanvas();
            m_Blocker = CreateBlocker(rootCanvas);

            m_Dropdown = Instantiate(m_Template.gameObject, transform, false);
            m_Dropdown.name = "Dropdown List";
            m_Dropdown.SetActive(true);
            // The list draws above everything else (the original gives it its own canvas at 30000).
            var listCanvas = m_Dropdown.GetComponent<Canvas>() ?? m_Dropdown.AddComponent<Canvas>();
            listCanvas.overrideSorting = true;
            listCanvas.sortingOrder = 30000;
            if (m_Dropdown.GetComponent<GraphicRaycaster>() == null) m_Dropdown.AddComponent<GraphicRaycaster>();

            var itemTemplatePath = RelativePath(m_Template.transform, item.transform);
            var clonedItem = itemTemplatePath == null ? null : m_Dropdown.transform.Find(itemTemplatePath)?.GetComponent<Toggle>();
            if (clonedItem == null) { ImmediateDestroyDropdownList(); return; }

            string labelPath = m_ItemText != null ? RelativePath(item.transform, m_ItemText.transform) : null;
            string imagePath = m_ItemImage != null ? RelativePath(item.transform, m_ItemImage.transform) : null;

            var content = clonedItem.transform.parent as RectTransform;
            var itemRect = clonedItem.transform as RectTransform;
            float itemHeight = itemRect != null ? itemRect.rect.height : 20f;
            clonedItem.gameObject.SetActive(false);

            m_Items.Clear();
            for (int i = 0; i < opts.Count; i++)
            {
                var go = Instantiate(clonedItem.gameObject, content, false);
                go.SetActive(true);
                go.name = $"Item {i}: {opts[i].text}";
                var toggle = go.GetComponent<Toggle>();
                toggle.SetIsOnWithoutNotify(i == m_Value);
                int index = i;
                toggle.onValueChanged.AddListener(on => OnSelectItem(index));

                if (labelPath != null && go.transform.Find(labelPath)?.GetComponent<TMP_Text>() is { } label)
                    label.text = opts[i].text ?? string.Empty;
                if (imagePath != null && go.transform.Find(imagePath)?.GetComponent<Image>() is { } img)
                {
                    img.sprite = opts[i].image;
                    img.enabled = opts[i].image != null;
                }

                if (go.transform is RectTransform rt)
                {
                    var pos = rt.anchoredPosition;
                    rt.anchoredPosition = new Vector2(pos.x, -itemHeight * i - itemHeight * 0.5f);
                }
                m_Items.Add(go);
            }

            if (content != null)
            {
                var size = content.sizeDelta;
                content.sizeDelta = new Vector2(size.x, itemHeight * opts.Count);
            }

            if (EventSystem.current != null && m_Items.Count > m_Value)
                EventSystem.current.SetSelectedGameObject(m_Items[Mathf.Clamp(m_Value, 0, m_Items.Count - 1)]);
        }

        /// <summary>Closes the option list (original contract).</summary>
        public void Hide()
        {
            ImmediateDestroyDropdownList();
            if (IsActive()) Select();
        }

        void OnSelectItem(int index)
        {
            value = index;
            Hide();
        }

        void ImmediateDestroyDropdownList()
        {
            m_Items.Clear();
            if (m_Dropdown != null) { Destroy(m_Dropdown); m_Dropdown = null; }
            if (m_Blocker != null) { Destroy(m_Blocker); m_Blocker = null; }
        }

        Canvas FindRootCanvas()
        {
            Canvas found = null;
            for (var t = transform; t != null; t = t.parent)
                if (t.gameObject.GetComponent<Canvas>() is { } c) found = c;
            return found;
        }

        GameObject CreateBlocker(Canvas rootCanvas)
        {
            var blocker = new GameObject("Blocker");
            var rt = blocker.AddComponent<RectTransform>();
            if (rootCanvas != null) rt.SetParent(rootCanvas.transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            var canvas = blocker.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 29999;
            blocker.AddComponent<GraphicRaycaster>();
            var image = blocker.AddComponent<Image>();
            image.color = Color.clear;
            var button = blocker.AddComponent<Button>();
            button.onClick.AddListener(Hide);
            return blocker;
        }

        static string RelativePath(Transform root, Transform target)
        {
            if (root == target) return string.Empty;
            var parts = new List<string>();
            for (var t = target; t != null; t = t.parent)
            {
                if (t == root) { parts.Reverse(); return string.Join("/", parts); }
                parts.Add(t.name);
            }
            return null;
        }
    }
}
