using System;
using System.Collections.Generic;
using CosmicShore.Engine.Events;

namespace CosmicShore.Engine
{
    /// <summary>
    /// Legacy dynamic font asset (original contract: UnityEngine.Font). Arc E: the
    /// content bridge fills <see cref="sourcePath"/> with the project's .ttf/.otf so a
    /// render backend can rasterize it.
    /// </summary>
    public class Font : Object
    {
        public string sourcePath;
        public int fontSize = 16;
    }

    /// <summary>Capsule-shaped collider (data; the trigger pass treats it as its bounds).</summary>
    public class CapsuleCollider : Collider
    {
        public Vector3 center = Vector3.zero;
        public float radius = 0.5f;
        public float height = 2f;
        public int direction = 1;
    }
}

namespace CosmicShore.Engine.UI
{
    /// <summary>Legacy uGUI Text font settings (original serialized layout: m_FontData).</summary>
    [Serializable]
    public class FontData
    {
        [SerializeField] public Font m_Font;
        [SerializeField] public int m_FontSize = 14;
        [SerializeField] public int m_FontStyle;
        [SerializeField] public bool m_BestFit;
        [SerializeField] public int m_MinSize = 10;
        [SerializeField] public int m_MaxSize = 40;
        [SerializeField] public int m_Alignment;
        [SerializeField] public bool m_AlignByGeometry;
        [SerializeField] public bool m_RichText = true;
        [SerializeField] public int m_HorizontalOverflow;
        [SerializeField] public int m_VerticalOverflow;
        [SerializeField] public float m_LineSpacing = 1f;
    }

    /// <summary>Legacy (non-TMP) uGUI text (original contract: UnityEngine.UI.Text).</summary>
    public class Text : MaskableGraphic
    {
        [SerializeField] FontData m_FontData = new();
        [SerializeField] string m_Text = string.Empty;

        public virtual string text
        {
            get => m_Text;
            set { if (m_Text == value) return; m_Text = value ?? string.Empty; SetVerticesDirty(); }
        }

        public FontData fontData => m_FontData;
        public Font font { get => m_FontData.m_Font; set => m_FontData.m_Font = value; }
        public int fontSize { get => m_FontData.m_FontSize; set => m_FontData.m_FontSize = value; }
        public TextAnchor alignment { get => (TextAnchor)m_FontData.m_Alignment; set => m_FontData.m_Alignment = (int)value; }
    }

    /// <summary>Radio-group container for Toggles (original contract: ToggleGroup).</summary>
    public class ToggleGroup : MonoBehaviour
    {
        [SerializeField] bool m_AllowSwitchOff;
        public bool allowSwitchOff { get => m_AllowSwitchOff; set => m_AllowSwitchOff = value; }
    }

    /// <summary>Scroll bar (original contract: Scrollbar) — data surface for the scroll views the menu authors.</summary>
    public class Scrollbar : Selectable
    {
        public enum Direction { LeftToRight = 0, RightToLeft = 1, BottomToTop = 2, TopToBottom = 3 }

        [SerializeField] RectTransform m_HandleRect;
        [SerializeField] Direction m_Direction;
        [SerializeField] float m_Value;
        [SerializeField] float m_Size = 0.2f;
        [SerializeField] int m_NumberOfSteps;
        public UnityEvent<float> onValueChanged = new();

        public RectTransform handleRect { get => m_HandleRect; set => m_HandleRect = value; }
        public Direction direction { get => m_Direction; set => m_Direction = value; }
        public float value { get => m_Value; set { m_Value = Mathf.Clamp01(value); onValueChanged.Invoke(m_Value); } }
        public float size { get => m_Size; set => m_Size = Mathf.Clamp01(value); }
        public int numberOfSteps { get => m_NumberOfSteps; set => m_NumberOfSteps = value; }
    }

    /// <summary>Keeps a RectTransform at an aspect ratio (original contract: AspectRatioFitter).</summary>
    [RequireComponent(typeof(RectTransform))]
    public class AspectRatioFitter : MonoBehaviour
    {
        public enum AspectMode { None = 0, WidthControlsHeight = 1, HeightControlsWidth = 2, FitInParent = 3, EnvelopeParent = 4 }
        [SerializeField] AspectMode m_AspectMode;
        [SerializeField] float m_AspectRatio = 1f;
        public AspectMode aspectMode { get => m_AspectMode; set => m_AspectMode = value; }
        public float aspectRatio { get => m_AspectRatio; set => m_AspectRatio = value; }
    }

    /// <summary>Per-event listener table (original contract: EventTrigger).</summary>
    public class EventTrigger : MonoBehaviour
    {
        [Serializable]
        public class TriggerEvent : UnityEvent<BaseEventData> { }

        [Serializable]
        public class Entry
        {
            public int eventID;
            public TriggerEvent callback = new();
        }

        [SerializeField] List<Entry> m_Delegates = new();
        public List<Entry> triggers { get => m_Delegates; set => m_Delegates = value; }
    }

    /// <summary>Drop-shadow mesh effect (original contract: Shadow).</summary>
    public class Shadow : MonoBehaviour
    {
        [SerializeField] Color m_EffectColor = new(0f, 0f, 0f, 0.5f);
        [SerializeField] Vector2 m_EffectDistance = new(1f, -1f);
        [SerializeField] bool m_UseGraphicAlpha = true;
        public Color effectColor { get => m_EffectColor; set => m_EffectColor = value; }
        public Vector2 effectDistance { get => m_EffectDistance; set => m_EffectDistance = value; }
        public bool useGraphicAlpha { get => m_UseGraphicAlpha; set => m_UseGraphicAlpha = value; }
    }

    /// <summary>Outline mesh effect (original contract: Outline — a four-way Shadow).</summary>
    public class Outline : Shadow
    {
    }
}
