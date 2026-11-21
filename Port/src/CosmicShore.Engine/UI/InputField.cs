using System;
using CosmicShore.Engine.Events;
using CosmicShore.Engine.InputSystem;

namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// The legacy uGUI text field (original contract: UnityEngine.UI.InputField) — the one the
    /// development-build diagnostics consoles build in code. Same editing model as
    /// <see cref="TMP_InputField"/> (typed characters from <see cref="Keyboard.onTextInput"/>,
    /// Backspace/Delete/arrows/Home/End/Enter/Escape as key presses, focus on click/select,
    /// <see cref="onEndEdit"/> when focus leaves), labelled by a legacy <see cref="Text"/>.
    /// Character validation shares the TMP field's rules.
    /// </summary>
    public class InputField : Selectable, IPointerClickHandler, ISubmitHandler
    {
        public enum ContentType { Standard = 0, Autocorrected = 1, IntegerNumber = 2, DecimalNumber = 3, Alphanumeric = 4, Name = 5, EmailAddress = 6, Password = 7, Pin = 8, Custom = 9 }
        public enum InputType { Standard = 0, AutoCorrect = 1, Password = 2 }
        public enum CharacterValidation { None = 0, Integer = 1, Decimal = 2, Alphanumeric = 3, Name = 4, EmailAddress = 5 }
        public enum LineType { SingleLine = 0, MultiLineSubmit = 1, MultiLineNewline = 2 }

        [Serializable] public class SubmitEvent : UnityEvent<string> { }
        [Serializable] public class EndEditEvent : UnityEvent<string> { }
        [Serializable] public class OnChangeEvent : UnityEvent<string> { }

        public delegate char OnValidateInput(string text, int charIndex, char addedChar);

        [SerializeField] protected Text m_TextComponent;
        [SerializeField] protected Graphic m_Placeholder;
        [SerializeField] protected ContentType m_ContentType = ContentType.Standard;
        [SerializeField] protected InputType m_InputType = InputType.Standard;
        [SerializeField] protected char m_AsteriskChar = '*';
        [SerializeField] protected LineType m_LineType = LineType.SingleLine;
        [SerializeField] protected CharacterValidation m_CharacterValidation = CharacterValidation.None;
        [SerializeField] protected int m_CharacterLimit;
        [SerializeField] protected EndEditEvent m_OnEndEdit = new();
        [SerializeField] protected SubmitEvent m_OnSubmit = new();
        [SerializeField] protected OnChangeEvent m_OnValueChanged = new();
        [SerializeField] protected Color m_CaretColor = new(50f / 255f, 50f / 255f, 50f / 255f, 1f);
        [SerializeField] protected Color m_SelectionColor = new(168f / 255f, 206f / 255f, 255f / 255f, 192f / 255f);
        [SerializeField] protected string m_Text = string.Empty;
        [SerializeField] protected float m_CaretBlinkRate = 0.85f;
        [SerializeField] protected int m_CaretWidth = 1;
        [SerializeField] protected bool m_ReadOnly;

        bool m_IsFocused;
        bool m_WasCanceled;
        string m_OriginalText = string.Empty;
        int m_Caret;
        Keyboard m_Keyboard;

        public OnValidateInput onValidateInput { get; set; }

        public string text { get => m_Text; set => SetText(value, true); }
        public void SetTextWithoutNotify(string input) => SetText(input, false);

        void SetText(string value, bool notify)
        {
            value ??= string.Empty;
            if (m_CharacterLimit > 0 && value.Length > m_CharacterLimit) value = value.Substring(0, m_CharacterLimit);
            if (value == m_Text) { UpdateLabel(); return; }
            m_Text = value;
            m_Caret = Math.Clamp(m_Caret, 0, m_Text.Length);
            UpdateLabel();
            if (notify) m_OnValueChanged?.Invoke(m_Text);
        }

        public Text textComponent { get => m_TextComponent; set { m_TextComponent = value; UpdateLabel(); } }
        public Graphic placeholder { get => m_Placeholder; set { m_Placeholder = value; UpdateLabel(); } }
        public int characterLimit { get => m_CharacterLimit; set { m_CharacterLimit = Math.Max(0, value); SetText(m_Text, true); } }
        public bool readOnly { get => m_ReadOnly; set => m_ReadOnly = value; }
        public char asteriskChar { get => m_AsteriskChar; set { m_AsteriskChar = value; UpdateLabel(); } }
        public float caretBlinkRate { get => m_CaretBlinkRate; set => m_CaretBlinkRate = value; }
        public int caretWidth { get => m_CaretWidth; set => m_CaretWidth = value; }
        public Color caretColor { get => m_CaretColor; set => m_CaretColor = value; }
        public Color selectionColor { get => m_SelectionColor; set => m_SelectionColor = value; }
        public bool isFocused => m_IsFocused;
        public bool wasCanceled => m_WasCanceled;
        public bool multiLine => m_LineType != LineType.SingleLine;
        public int caretPosition { get => m_Caret; set => m_Caret = Math.Clamp(value, 0, m_Text.Length); }
        public int selectionAnchorPosition { get => m_Caret; set { } }
        public int selectionFocusPosition { get => m_Caret; set => caretPosition = value; }

        public EndEditEvent onEndEdit { get => m_OnEndEdit; set => m_OnEndEdit = value; }
        public SubmitEvent onSubmit { get => m_OnSubmit; set => m_OnSubmit = value; }
        public OnChangeEvent onValueChanged { get => m_OnValueChanged; set => m_OnValueChanged = value; }

        public LineType lineType { get => m_LineType; set => m_LineType = value; }
        public InputType inputType { get => m_InputType; set { m_InputType = value; UpdateLabel(); } }
        public CharacterValidation characterValidation { get => m_CharacterValidation; set => m_CharacterValidation = value; }

        public ContentType contentType
        {
            get => m_ContentType;
            set
            {
                m_ContentType = value;
                (m_InputType, m_CharacterValidation) = value switch
                {
                    ContentType.IntegerNumber => (InputType.Standard, CharacterValidation.Integer),
                    ContentType.DecimalNumber => (InputType.Standard, CharacterValidation.Decimal),
                    ContentType.Alphanumeric => (InputType.Standard, CharacterValidation.Alphanumeric),
                    ContentType.Name => (InputType.Standard, CharacterValidation.Name),
                    ContentType.EmailAddress => (InputType.Standard, CharacterValidation.EmailAddress),
                    ContentType.Password => (InputType.Password, CharacterValidation.None),
                    ContentType.Pin => (InputType.Password, CharacterValidation.Integer),
                    ContentType.Autocorrected => (InputType.AutoCorrect, CharacterValidation.None),
                    ContentType.Custom => (m_InputType, m_CharacterValidation),
                    _ => (InputType.Standard, CharacterValidation.None),
                };
                if (value is not (ContentType.Standard or ContentType.Autocorrected or ContentType.Custom))
                    m_LineType = LineType.SingleLine;
                UpdateLabel();
            }
        }

        protected override void OnEnable() { base.OnEnable(); UpdateLabel(); }
        protected override void OnDisable() { DeactivateInputField(); base.OnDisable(); }

        public void ActivateInputField()
        {
            if (m_IsFocused || !IsInteractable()) return;
            m_IsFocused = true;
            m_WasCanceled = false;
            m_OriginalText = m_Text;
            m_Caret = m_Text.Length;
            Subscribe(Keyboard.current);
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != gameObject)
                EventSystem.current.SetSelectedGameObject(gameObject);
        }

        public void DeactivateInputField()
        {
            if (!m_IsFocused) return;
            m_IsFocused = false;
            Subscribe(null);
            m_OnEndEdit?.Invoke(m_Text);
        }

        void Subscribe(Keyboard kb)
        {
            if (m_Keyboard == kb) return;
            if (m_Keyboard != null) m_Keyboard.onTextInput -= OnTextInput;
            m_Keyboard = kb;
            if (m_Keyboard != null) m_Keyboard.onTextInput += OnTextInput;
        }

        public override void OnSelect(BaseEventData eventData) { base.OnSelect(eventData); ActivateInputField(); }
        public override void OnDeselect(BaseEventData eventData) { DeactivateInputField(); base.OnDeselect(eventData); }
        public void OnPointerClick(PointerEventData eventData) => ActivateInputField();
        public void OnSubmit(BaseEventData eventData) { if (!m_IsFocused) ActivateInputField(); }

        protected virtual void LateUpdate()
        {
            if (!m_IsFocused) return;
            var kb = Keyboard.current;
            if (kb != m_Keyboard) Subscribe(kb);
            if (kb == null) return;
            if (kb[Key.Escape].wasPressedThisFrame)
            {
                m_WasCanceled = true;
                text = m_OriginalText;
                DeactivateInputField();
                return;
            }
            if (kb[Key.Enter].wasPressedThisFrame || kb[Key.NumpadEnter].wasPressedThisFrame)
            {
                if (m_LineType == LineType.MultiLineNewline) Insert('\n');
                else { m_OnSubmit?.Invoke(m_Text); DeactivateInputField(); return; }
            }
            if (m_ReadOnly) return;
            if (kb[Key.Backspace].wasPressedThisFrame && m_Caret > 0)
            {
                m_Text = m_Text.Remove(m_Caret - 1, 1);
                m_Caret--;
                Changed();
            }
            if (kb[Key.Delete].wasPressedThisFrame && m_Caret < m_Text.Length)
            {
                m_Text = m_Text.Remove(m_Caret, 1);
                Changed();
            }
            if (kb[Key.LeftArrow].wasPressedThisFrame) m_Caret = Math.Max(0, m_Caret - 1);
            if (kb[Key.RightArrow].wasPressedThisFrame) m_Caret = Math.Min(m_Text.Length, m_Caret + 1);
            if (kb[Key.Home].wasPressedThisFrame) m_Caret = 0;
            if (kb[Key.End].wasPressedThisFrame) m_Caret = m_Text.Length;
        }

        void OnTextInput(char c)
        {
            if (!m_IsFocused || m_ReadOnly) return;
            if (char.IsControl(c) && c != '\n' && c != '\t') return;
            if (c == '\n' && m_LineType != LineType.MultiLineNewline) return;
            Insert(c);
        }

        void Insert(char c)
        {
            if (m_CharacterLimit > 0 && m_Text.Length >= m_CharacterLimit) return;
            char v = onValidateInput != null
                ? onValidateInput(m_Text, m_Caret, c)
                : TMP_InputField.ValidateChar(ToTmp(m_CharacterValidation), null, m_Text, m_Caret, c);
            if (v == '\0') return;
            m_Text = m_Text.Insert(m_Caret, v.ToString());
            m_Caret++;
            Changed();
        }

        void Changed()
        {
            UpdateLabel();
            m_OnValueChanged?.Invoke(m_Text);
        }

        static TMP_InputField.CharacterValidation ToTmp(CharacterValidation v) => v switch
        {
            CharacterValidation.Integer => TMP_InputField.CharacterValidation.Integer,
            CharacterValidation.Decimal => TMP_InputField.CharacterValidation.Decimal,
            CharacterValidation.Alphanumeric => TMP_InputField.CharacterValidation.Alphanumeric,
            CharacterValidation.Name => TMP_InputField.CharacterValidation.Name,
            CharacterValidation.EmailAddress => TMP_InputField.CharacterValidation.EmailAddress,
            _ => TMP_InputField.CharacterValidation.None,
        };

        public void ForceLabelUpdate() => UpdateLabel();

        void UpdateLabel()
        {
            if (m_TextComponent != null)
                m_TextComponent.text = m_InputType == InputType.Password ? new string(m_AsteriskChar, m_Text.Length) : m_Text;
            if (m_Placeholder != null)
                m_Placeholder.enabled = string.IsNullOrEmpty(m_Text);
        }
    }
}
