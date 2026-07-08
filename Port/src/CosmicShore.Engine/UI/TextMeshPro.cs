namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// TextMeshPro alignment flags (original TMPro numeric values: horizontal bits
    /// 1..32 OR'd with vertical bits 256..8192). Subset ported code uses.
    /// </summary>
    public enum TextAlignmentOptions
    {
        TopLeft = 257,
        Top = 258,
        TopRight = 260,
        Left = 513,
        Center = 514,
        Right = 516,
        BottomLeft = 1025,
        Bottom = 1026,
        BottomRight = 1028,
    }

    /// <summary>Font asset placeholder — identity only until the text-rendering phase.</summary>
    public class TMP_FontAsset : ScriptableObject
    {
    }

    /// <summary>Project-wide TMP settings surface (original: TMP_Settings asset statics).</summary>
    public static class TMP_Settings
    {
        public static TMP_FontAsset defaultFontAsset { get; set; }
    }

    /// <summary>
    /// The TMPro shim: headless data holder for TextMeshPro text. Ported code reads
    /// and writes text/color/size/alignment; a render backend draws from the same
    /// state in the presentation phase. Substitution: `using TMPro;` →
    /// `using CosmicShore.Engine.UI;`.
    /// </summary>
    public abstract class TMP_Text : Behaviour
    {
        public string text = string.Empty;
        public Color color = Color.white;
        public float fontSize = 36f;
        public TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft;
        public TMP_FontAsset font;
    }

    /// <summary>World-space (3D) TextMeshPro component.</summary>
    public class TextMeshPro : TMP_Text
    {
    }

    /// <summary>Canvas (UGUI) TextMeshPro component.</summary>
    /// <summary>
    /// Data-only input-field shim (original contract: TMPro.TMP_InputField — the slice
    /// ported controllers read/write: the entered text plus interactability via Behaviour).
    /// A render/input layer binds it later; tests set <see cref="text"/> directly.
    /// </summary>
    public class TMP_InputField : Behaviour
    {
        public string text = string.Empty;
    }

    public class TextMeshProUGUI : TMP_Text
    {
    }
}
