using System.Collections.Generic;
using CosmicShore.Engine.Events;

namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// Selection-from-a-list control (original contract: TMP_Dropdown : Selectable).
    /// The used surface — options / value / onValueChanged / SetValueWithoutNotify /
    /// captionText — is REAL: value clamps to the option range and fires only on
    /// change, and the caption label mirrors the selected option's text. The
    /// expanded item-list popup (template instantiation, blocker canvas, scroll)
    /// is presentation the leaderboards screen never reads and is intentionally
    /// not built — clicking cycles to the next option instead, which exercises the
    /// same value/onValueChanged contract the ported code consumes.
    /// </summary>
    public class TMP_Dropdown : Selectable, IPointerClickHandler, ISubmitHandler
    {
        public class OptionData
        {
            public string text;
            public OptionData() { }
            public OptionData(string text) { this.text = text; }
        }

        List<OptionData> m_Options = new();
        int m_Value;

        /// <summary>The caption label showing the selected option (optional).</summary>
        public TMP_Text captionText;

        public UnityEvent<int> onValueChanged = new();

        public List<OptionData> options
        {
            get => m_Options;
            set
            {
                m_Options = value ?? new List<OptionData>();
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
            m_Options.Clear();
            m_Value = 0;
            RefreshShownValue();
        }

        public void AddOptions(List<OptionData> newOptions)
        {
            m_Options.AddRange(newOptions);
            RefreshShownValue();
        }

        public void AddOptions(List<string> newOptions)
        {
            foreach (var text in newOptions)
                m_Options.Add(new OptionData(text));
            RefreshShownValue();
        }

        /// <summary>Sync the caption with the selected option (original name).</summary>
        public void RefreshShownValue()
        {
            if (captionText != null)
                captionText.text = m_Value >= 0 && m_Value < m_Options.Count
                    ? m_Options[m_Value].text
                    : string.Empty;
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
            if (sendCallback) onValueChanged.Invoke(m_Value);
        }

        int ClampValue(int input)
            => m_Options.Count == 0 ? 0 : Mathf.Clamp(input, 0, m_Options.Count - 1);

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!IsInteractable() || m_Options.Count == 0) return;
            value = (m_Value + 1) % m_Options.Count; // popup stand-in: cycle
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (!IsInteractable() || m_Options.Count == 0) return;
            value = (m_Value + 1) % m_Options.Count;
        }
    }
}
