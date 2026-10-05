using System;
using System.Text;
using CosmicShore.Engine.Events;
using CosmicShore.Engine.InputSystem;

namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// An editable single- or multi-line text field (original contract: TMPro.TMP_InputField).
    /// Keeps TMP's serialized field names so scenes deserialize onto it, applies the
    /// content-type / character-validation rules, the character limit and the password mask,
    /// and edits from the Input System keyboard while focused: typed characters arrive through
    /// <see cref="Keyboard.onTextInput"/>; Backspace, Delete, arrows, Home/End, Enter and Escape
    /// are read as key presses. Clicking activates it, deselecting ends the edit (firing
    /// <see cref="onEndEdit"/>), and Enter on a single-line field submits.
    /// </summary>
    public class TMP_InputField : Selectable, IPointerClickHandler, ISubmitHandler
    {
        public enum ContentType { Standard = 0, Autocorrected = 1, IntegerNumber = 2, DecimalNumber = 3, Alphanumeric = 4, Name = 5, EmailAddress = 6, Password = 7, Pin = 8, Custom = 9 }
        public enum InputType { Standard = 0, AutoCorrect = 1, Password = 2 }
        public enum CharacterValidation { None = 0, Digit = 1, Integer = 2, Decimal = 3, Alphanumeric = 4, Name = 5, Regex = 6, EmailAddress = 7, CustomValidator = 8 }
        public enum LineType { SingleLine = 0, MultiLineSubmit = 1, MultiLineNewline = 2 }

        [Serializable] public class SubmitEvent : UnityEvent<string> { }
        [Serializable] public class OnChangeEvent : UnityEvent<string> { }
        [Serializable] public class SelectionEvent : UnityEvent<string> { }
        [Serializable] public class TextSelectionEvent : UnityEvent<string, int, int> { }
        [Serializable] public class TouchScreenKeyboardEvent : UnityEvent<int> { }

        public delegate char OnValidateInput(string text, int charIndex, char addedChar);

        [SerializeField] protected RectTransform m_TextViewport;
        [SerializeField] protected TMP_Text m_TextComponent;
        [SerializeField] protected Graphic m_Placeholder;
        [SerializeField] protected ContentType m_ContentType = ContentType.Standard;
        [SerializeField] protected InputType m_InputType = InputType.Standard;
        [SerializeField] protected char m_AsteriskChar = '*';
        [SerializeField] protected LineType m_LineType = LineType.SingleLine;
        [SerializeField] protected bool m_HideMobileInput;
        [SerializeField] protected CharacterValidation m_CharacterValidation = CharacterValidation.None;
        [SerializeField] protected string m_RegexValue = string.Empty;
        [SerializeField] protected float m_GlobalPointSize = 14f;
        [SerializeField] protected int m_CharacterLimit;
        [SerializeField] protected int m_LineLimit;
        [SerializeField] protected SubmitEvent m_OnEndEdit = new();
        [SerializeField] protected SubmitEvent m_OnSubmit = new();
        [SerializeField] protected SelectionEvent m_OnSelect = new();
        [SerializeField] protected SelectionEvent m_OnDeselect = new();
        [SerializeField] protected TextSelectionEvent m_OnTextSelection = new();
        [SerializeField] protected TextSelectionEvent m_OnEndTextSelection = new();
        [SerializeField] protected OnChangeEvent m_OnValueChanged = new();
        [SerializeField] protected TouchScreenKeyboardEvent m_OnTouchScreenKeyboardStatusChanged = new();
        [SerializeField] protected Color m_CaretColor = new(50f / 255f, 50f / 255f, 50f / 255f, 1f);
        [SerializeField] protected bool m_CustomCaretColor;
        [SerializeField] protected Color m_SelectionColor = new(168f / 255f, 206f / 255f, 255f / 255f, 192f / 255f);
        [SerializeField] protected string m_Text = string.Empty;
        [SerializeField] protected float m_CaretBlinkRate = 0.85f;
        [SerializeField] protected int m_CaretWidth = 1;
        [SerializeField] protected bool m_ReadOnly;
        [SerializeField] protected bool m_RichText = true;
        [SerializeField] protected bool m_OnFocusSelectAll = true;
        [SerializeField] protected bool m_ResetOnDeActivation = true;
        [SerializeField] protected bool m_RestoreOriginalTextOnEscape = true;
        [SerializeField] protected bool m_isRichTextEditingAllowed;

        bool m_IsFocused;
        bool m_WasCanceled;
        string m_OriginalText = string.Empty;
        int m_Caret;
        int m_SelectAnchor;
        Keyboard m_SubscribedKeyboard;

        public OnValidateInput onValidateInput { get; set; }

        // ── properties (original names) ───────────────────────────────────

        public string text
        {
            get => m_Text;
            set => SetText(value, sendCallback: true);
        }

        public void SetTextWithoutNotify(string input) => SetText(input, sendCallback: false);

        void SetText(string value, bool sendCallback)
        {
            value ??= string.Empty;
            if (m_CharacterLimit > 0 && value.Length > m_CharacterLimit) value = value.Substring(0, m_CharacterLimit);
            if (m_Text == value) { UpdateLabel(); return; }
            m_Text = value;
            m_Caret = Math.Clamp(m_Caret, 0, m_Text.Length);
            m_SelectAnchor = Math.Clamp(m_SelectAnchor, 0, m_Text.Length);
            UpdateLabel();
            if (sendCallback) m_OnValueChanged?.Invoke(m_Text);
        }

        public TMP_Text textComponent { get => m_TextComponent; set { m_TextComponent = value; UpdateLabel(); } }
        public RectTransform textViewport { get => m_TextViewport; set => m_TextViewport = value; }
        public Graphic placeholder { get => m_Placeholder; set { m_Placeholder = value; UpdateLabel(); } }
        public int characterLimit
        {
            get => m_CharacterLimit;
            set { m_CharacterLimit = Math.Max(0, value); if (m_CharacterLimit > 0 && m_Text.Length > m_CharacterLimit) text = m_Text.Substring(0, m_CharacterLimit); }
        }
        public int lineLimit { get => m_LineLimit; set => m_LineLimit = value; }
        public bool readOnly { get => m_ReadOnly; set => m_ReadOnly = value; }
        public bool richText { get => m_RichText; set => m_RichText = value; }
        public bool onFocusSelectAll { get => m_OnFocusSelectAll; set => m_OnFocusSelectAll = value; }
        public bool resetOnDeActivation { get => m_ResetOnDeActivation; set => m_ResetOnDeActivation = value; }
        public bool restoreOriginalTextOnEscape { get => m_RestoreOriginalTextOnEscape; set => m_RestoreOriginalTextOnEscape = value; }
        public bool shouldHideMobileInput { get => m_HideMobileInput; set => m_HideMobileInput = value; }
        public char asteriskChar { get => m_AsteriskChar; set { m_AsteriskChar = value; UpdateLabel(); } }
        public float caretBlinkRate { get => m_CaretBlinkRate; set => m_CaretBlinkRate = value; }
        public int caretWidth { get => m_CaretWidth; set => m_CaretWidth = value; }
        public Color caretColor { get => m_CustomCaretColor ? m_CaretColor : (m_TextComponent != null ? m_TextComponent.color : m_CaretColor); set => m_CaretColor = value; }
        public bool customCaretColor { get => m_CustomCaretColor; set => m_CustomCaretColor = value; }
        public Color selectionColor { get => m_SelectionColor; set => m_SelectionColor = value; }
        public float pointSize { get => m_GlobalPointSize; set => m_GlobalPointSize = value; }
        public string regexValue { get => m_RegexValue; set => m_RegexValue = value; }
        public bool isFocused => m_IsFocused;
        public bool wasCanceled => m_WasCanceled;
        public bool multiLine => m_LineType != LineType.SingleLine;

        public SubmitEvent onEndEdit { get => m_OnEndEdit; set => m_OnEndEdit = value; }
        public SubmitEvent onSubmit { get => m_OnSubmit; set => m_OnSubmit = value; }
        public SelectionEvent onSelect { get => m_OnSelect; set => m_OnSelect = value; }
        public SelectionEvent onDeselect { get => m_OnDeselect; set => m_OnDeselect = value; }
        public TextSelectionEvent onTextSelection { get => m_OnTextSelection; set => m_OnTextSelection = value; }
        public TextSelectionEvent onEndTextSelection { get => m_OnEndTextSelection; set => m_OnEndTextSelection = value; }
        public OnChangeEvent onValueChanged { get => m_OnValueChanged; set => m_OnValueChanged = value; }
        public TouchScreenKeyboardEvent onTouchScreenKeyboardStatusChanged { get => m_OnTouchScreenKeyboardStatusChanged; set => m_OnTouchScreenKeyboardStatusChanged = value; }

        public int caretPosition
        {
            get => m_Caret;
            set { m_Caret = Math.Clamp(value, 0, m_Text.Length); m_SelectAnchor = m_Caret; }
        }
        public int stringPosition { get => caretPosition; set => caretPosition = value; }
        public int selectionAnchorPosition { get => m_SelectAnchor; set => m_SelectAnchor = Math.Clamp(value, 0, m_Text.Length); }
        public int selectionFocusPosition { get => m_Caret; set => m_Caret = Math.Clamp(value, 0, m_Text.Length); }
        public int selectionStringAnchorPosition { get => selectionAnchorPosition; set => selectionAnchorPosition = value; }
        public int selectionStringFocusPosition { get => selectionFocusPosition; set => selectionFocusPosition = value; }

        public ContentType contentType
        {
            get => m_ContentType;
            set { m_ContentType = value; EnforceContentType(); }
        }

        public LineType lineType
        {
            get => m_LineType;
            set { m_LineType = value; if (m_ContentType != ContentType.Standard && m_ContentType != ContentType.Autocorrected && m_ContentType != ContentType.Custom) m_LineType = LineType.SingleLine; }
        }

        public InputType inputType { get => m_InputType; set { m_InputType = value; m_ContentType = ContentType.Custom; UpdateLabel(); } }

        public CharacterValidation characterValidation
        {
            get => m_CharacterValidation;
            set { m_CharacterValidation = value; m_ContentType = ContentType.Custom; }
        }

        void EnforceContentType()
        {
            switch (m_ContentType)
            {
                case ContentType.Standard: m_InputType = InputType.Standard; m_CharacterValidation = CharacterValidation.None; break;
                case ContentType.Autocorrected: m_InputType = InputType.AutoCorrect; m_CharacterValidation = CharacterValidation.None; break;
                case ContentType.IntegerNumber: m_LineType = LineType.SingleLine; m_InputType = InputType.Standard; m_CharacterValidation = CharacterValidation.Integer; break;
                case ContentType.DecimalNumber: m_LineType = LineType.SingleLine; m_InputType = InputType.Standard; m_CharacterValidation = CharacterValidation.Decimal; break;
                case ContentType.Alphanumeric: m_LineType = LineType.SingleLine; m_InputType = InputType.Standard; m_CharacterValidation = CharacterValidation.Alphanumeric; break;
                case ContentType.Name: m_LineType = LineType.SingleLine; m_InputType = InputType.Standard; m_CharacterValidation = CharacterValidation.Name; break;
                case ContentType.EmailAddress: m_LineType = LineType.SingleLine; m_InputType = InputType.Standard; m_CharacterValidation = CharacterValidation.EmailAddress; break;
                case ContentType.Password: m_LineType = LineType.SingleLine; m_InputType = InputType.Password; m_CharacterValidation = CharacterValidation.None; break;
                case ContentType.Pin: m_LineType = LineType.SingleLine; m_InputType = InputType.Password; m_CharacterValidation = CharacterValidation.Digit; break;
            }
            UpdateLabel();
        }

        // ── lifecycle / focus ─────────────────────────────────────────────

        protected override void OnEnable()
        {
            base.OnEnable();
            UpdateLabel();
        }

        protected override void OnDisable()
        {
            DeactivateInputField();
            base.OnDisable();
        }

        public void ActivateInputField()
        {
            if (m_IsFocused || !IsInteractable()) return;
            m_IsFocused = true;
            m_WasCanceled = false;
            m_OriginalText = m_Text;
            if (m_OnFocusSelectAll) { m_SelectAnchor = 0; m_Caret = m_Text.Length; }
            else { m_Caret = m_Text.Length; m_SelectAnchor = m_Caret; }
            SubscribeKeyboard(Keyboard.current);
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != gameObject)
                EventSystem.current.SetSelectedGameObject(gameObject);
            m_OnSelect?.Invoke(m_Text);
            UpdateLabel();
        }

        public void DeactivateInputField(bool clearSelection = false)
        {
            if (!m_IsFocused) return;
            m_IsFocused = false;
            SubscribeKeyboard(null);
            if (clearSelection || m_ResetOnDeActivation) m_SelectAnchor = m_Caret;
            m_OnEndEdit?.Invoke(m_Text);
            UpdateLabel();
        }

        void SubscribeKeyboard(Keyboard keyboard)
        {
            if (m_SubscribedKeyboard == keyboard) return;
            if (m_SubscribedKeyboard != null) m_SubscribedKeyboard.onTextInput -= OnTextInput;
            m_SubscribedKeyboard = keyboard;
            if (m_SubscribedKeyboard != null) m_SubscribedKeyboard.onTextInput += OnTextInput;
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            ActivateInputField();
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            DeactivateInputField();
            m_OnDeselect?.Invoke(m_Text);
            base.OnDeselect(eventData);
        }

        public void OnPointerClick(PointerEventData eventData) => ActivateInputField();

        public void OnSubmit(BaseEventData eventData)
        {
            if (!m_IsFocused) ActivateInputField();
        }

        // ── editing ───────────────────────────────────────────────────────

        protected virtual void LateUpdate()
        {
            if (!m_IsFocused) return;
            var kb = Keyboard.current;
            if (kb != m_SubscribedKeyboard) SubscribeKeyboard(kb);
            if (kb == null) return;

            bool shift = kb.shiftKey.isPressed;
            if (kb[Key.Escape].wasPressedThisFrame)
            {
                m_WasCanceled = true;
                if (m_RestoreOriginalTextOnEscape) text = m_OriginalText;
                DeactivateInputField();
                return;
            }
            if (kb[Key.Enter].wasPressedThisFrame || kb[Key.NumpadEnter].wasPressedThisFrame)
            {
                if (m_LineType == LineType.MultiLineNewline) Insert('\n');
                else
                {
                    m_OnSubmit?.Invoke(m_Text);
                    DeactivateInputField();
                    return;
                }
            }
            if (m_ReadOnly) return;
            if (kb[Key.Backspace].wasPressedThisFrame) Backspace();
            if (kb[Key.Delete].wasPressedThisFrame) ForwardDelete();
            if (kb[Key.LeftArrow].wasPressedThisFrame) MoveCaret(m_Caret - 1, shift);
            if (kb[Key.RightArrow].wasPressedThisFrame) MoveCaret(m_Caret + 1, shift);
            if (kb[Key.Home].wasPressedThisFrame) MoveTextStart(shift);
            if (kb[Key.End].wasPressedThisFrame) MoveTextEnd(shift);
        }

        void OnTextInput(char c)
        {
            if (!m_IsFocused || m_ReadOnly) return;
            if (char.IsControl(c) && c != '\n' && c != '\t') return;
            if (c == '\n' && m_LineType != LineType.MultiLineNewline) return;
            Insert(c);
        }

        bool HasSelection => m_SelectAnchor != m_Caret;

        void DeleteSelection()
        {
            int start = Math.Min(m_SelectAnchor, m_Caret), end = Math.Max(m_SelectAnchor, m_Caret);
            m_Text = m_Text.Remove(start, end - start);
            m_Caret = m_SelectAnchor = start;
        }

        void Insert(char c)
        {
            if (HasSelection) DeleteSelection();
            if (m_CharacterLimit > 0 && m_Text.Length >= m_CharacterLimit) { UpdateLabel(); return; }

            char validated = onValidateInput != null
                ? onValidateInput(m_Text, m_Caret, c)
                : Validate(m_Text, m_Caret, c);
            if (validated == '\0') { UpdateLabel(); return; }

            m_Text = m_Text.Insert(m_Caret, validated.ToString());
            m_Caret++;
            m_SelectAnchor = m_Caret;
            UpdateLabel();
            m_OnValueChanged?.Invoke(m_Text);
        }

        void Backspace()
        {
            if (HasSelection) DeleteSelection();
            else if (m_Caret > 0) { m_Text = m_Text.Remove(m_Caret - 1, 1); m_Caret--; m_SelectAnchor = m_Caret; }
            else return;
            UpdateLabel();
            m_OnValueChanged?.Invoke(m_Text);
        }

        void ForwardDelete()
        {
            if (HasSelection) DeleteSelection();
            else if (m_Caret < m_Text.Length) m_Text = m_Text.Remove(m_Caret, 1);
            else return;
            UpdateLabel();
            m_OnValueChanged?.Invoke(m_Text);
        }

        void MoveCaret(int position, bool extend)
        {
            m_Caret = Math.Clamp(position, 0, m_Text.Length);
            if (!extend) m_SelectAnchor = m_Caret;
        }

        public void MoveTextStart(bool shift) => MoveCaret(0, shift);
        public void MoveTextEnd(bool shift) => MoveCaret(m_Text.Length, shift);
        public void MoveToStartOfLine(bool shift, bool ctrl) => MoveTextStart(shift);
        public void MoveToEndOfLine(bool shift, bool ctrl) => MoveTextEnd(shift);
        public void ProcessEvent(object e) { }
        public void ForceLabelUpdate() => UpdateLabel();
        public void Rebuild(CanvasUpdate update) { }
        public void SetGlobalPointSize(float pointSize) { m_GlobalPointSize = pointSize; if (m_TextComponent != null) m_TextComponent.fontSize = pointSize; }

        /// <summary>
        /// The original's per-character validation rules. Returns <c>'\0'</c> to reject.
        /// </summary>
        protected char Validate(string text, int pos, char ch) => ValidateChar(m_CharacterValidation, m_RegexValue, text, pos, ch);

        /// <summary>The per-character rules, shared with the legacy <see cref="InputField"/>.</summary>
        internal static char ValidateChar(CharacterValidation rule, string regex, string text, int pos, char ch)
        {
            switch (rule)
            {
                case CharacterValidation.None:
                    return ch;
                case CharacterValidation.Digit:
                    return ch >= '0' && ch <= '9' ? ch : '\0';
                case CharacterValidation.Integer:
                case CharacterValidation.Decimal:
                {
                    bool minusAllowed = pos == 0 && text.IndexOf('-') < 0;
                    if (ch >= '0' && ch <= '9') return pos == 0 && text.StartsWith("-") ? '\0' : ch;
                    if (ch == '-' && minusAllowed) return ch;
                    if (rule == CharacterValidation.Decimal && (ch == '.' || ch == ',') && text.IndexOf('.') < 0 && text.IndexOf(',') < 0)
                        return ch;
                    return '\0';
                }
                case CharacterValidation.Alphanumeric:
                    return char.IsLetterOrDigit(ch) && ch < 128 ? ch : '\0';
                case CharacterValidation.Name:
                {
                    char prev = pos > 0 ? text[pos - 1] : ' ';
                    if (char.IsLetter(ch))
                        return prev == ' ' || prev == '-' ? char.ToUpperInvariant(ch) : char.ToLowerInvariant(ch);
                    if (ch == '\'') return prev != ' ' && prev != '\'' && text.IndexOf('\'') < 0 ? ch : '\0';
                    if (ch == ' ' || ch == '-') return pos > 0 && prev != ' ' && prev != '-' && prev != '\'' ? ch : '\0';
                    return '\0';
                }
                case CharacterValidation.EmailAddress:
                {
                    if (char.IsLetterOrDigit(ch) && ch < 128) return ch;
                    if (ch == '@') return text.IndexOf('@') < 0 ? ch : '\0';
                    const string allowed = "!#$%&'*+-/=?^_`{|}~";
                    if (allowed.IndexOf(ch) >= 0) return ch;
                    if (ch == '.')
                    {
                        char prev = pos > 0 ? text[pos - 1] : ' ';
                        char next = pos < text.Length ? text[pos] : ' ';
                        return prev != '.' && next != '.' ? ch : '\0';
                    }
                    return '\0';
                }
                case CharacterValidation.Regex:
                    return string.IsNullOrEmpty(regex) || System.Text.RegularExpressions.Regex.IsMatch(ch.ToString(), regex) ? ch : '\0';
                default:
                    return ch;
            }
        }

        /// <summary>Pushes the (masked) text to the label and toggles the placeholder, as the original does every edit.</summary>
        protected void UpdateLabel()
        {
            if (m_TextComponent != null)
            {
                string shown = m_InputType == InputType.Password ? new string(m_AsteriskChar, m_Text.Length) : m_Text;
                // TMP appends a zero-width space so an empty field still has a line to place the caret on.
                m_TextComponent.text = shown.Length == 0 ? "​" : shown;
            }
            if (m_Placeholder != null)
                m_Placeholder.enabled = string.IsNullOrEmpty(m_Text);
        }
    }
}
